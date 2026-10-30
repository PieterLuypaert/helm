namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    // One choice in a question an agent asks, as its screen shows it. Number is set for a numbered
    // choice such as "1. Yes", and null for one you reach with the arrow keys.
    public sealed record QuestionOption(Int32 Index, String Number, String Text);

    // A question an agent asks on screen, such as Claude Code's permission prompt: its title, the
    // command or path it is about, its choices, and which choice is selected now.
    public sealed record Question(String Title, String Detail, IReadOnlyList<QuestionOption> Options, Int32 Selected)
    {
        private static readonly Regex DoNotAskAgain = new(@"don.t ask again", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Boolean Numbered => this.Options.All(o => o.Number != null);

        // The choices as they read, to check the question did not change before answering it.
        public String Signature => String.Join("|", this.Options.Select(o => $"{o.Number}:{o.Text}"));

        // The keys that pick a choice: its digit when the choices are numbered, which picks it at once;
        // otherwise the arrow keys from the selected choice to it, then Enter.
        public IReadOnlyList<String> KeysFor(Int32 index)
        {
            var option = this.Options[index];
            if (this.Numbered)
            {
                return new[] { option.Number };
            }

            var steps = index - this.Selected;
            return Enumerable.Repeat(steps > 0 ? "down" : "up", Math.Abs(steps)).Append("enter").ToList();
        }

        // A label short enough for a key: "Always" for "Yes, and don't ask again for: …".
        public static String ShortLabel(QuestionOption option)
        {
            var text = option.Text.Split(" · ")[0].Trim();
            if (DoNotAskAgain.IsMatch(text))
            {
                return "Always";
            }

            if (text.Contains("auto mode", StringComparison.OrdinalIgnoreCase))
            {
                return "Auto mode";
            }

            if (text.StartsWith("No, and tell", StringComparison.OrdinalIgnoreCase))
            {
                return "No, explain";
            }

            if (text.Length <= 12)
            {
                return text;
            }

            var comma = text.IndexOf(',');
            return comma > 0 ? text[..comma] : text;
        }
    }

    // Reads a question from an agent's screen. It recognises a question by its footer, "Esc to cancel"
    // or "Enter to confirm", with a selected choice (❯) just above it; anything else is no question.
    public static class Questions
    {
        private const Char Pointer = '❯';
        private static readonly Regex NumberedText = new(@"^(\d+)\.\s+(.*)$", RegexOptions.Compiled);

        public static Question Parse(String screen)
        {
            if (String.IsNullOrEmpty(screen))
            {
                return null;
            }

            var lines = screen.Replace("\r", "").Split('\n');
            var footer = Array.FindLastIndex(lines, l => l.Contains("Esc to cancel") || l.Contains("Enter to confirm"));
            if (footer < 0)
            {
                return null;
            }

            // The selected choice: the nearest pointer above the footer.
            var selectedLine = -1;
            for (var i = footer - 1; i >= Math.Max(0, footer - 15); i--)
            {
                if (lines[i].TrimStart().StartsWith(Pointer))
                {
                    selectedLine = i;
                    break;
                }
            }

            if (selectedLine < 0)
            {
                return null;
            }

            // Every choice's text starts in the same column as the selected one's.
            var pointerAt = lines[selectedLine].IndexOf(Pointer);
            var column = pointerAt + 1;
            while (column < lines[selectedLine].Length && lines[selectedLine][column] == ' ')
            {
                column++;
            }

            Boolean IsChoice(String line) => line.Length > column && line[..column].Trim().Length == 0 && line[column] != ' ';
            Boolean IsContinuation(String line) => line.Length > column + 1 && line[..(column + 1)].Trim().Length == 0;

            var first = selectedLine;
            while (first - 1 >= 0 && IsChoice(lines[first - 1]))
            {
                first--;
            }

            var last = selectedLine;
            while (last + 1 < footer && (IsChoice(lines[last + 1]) || IsContinuation(lines[last + 1])) && lines[last + 1].Trim().Length > 0)
            {
                last++;
            }

            var options = new List<QuestionOption>();
            var selected = -1;
            for (var i = first; i <= last; i++)
            {
                var line = lines[i];
                if (i != selectedLine && !IsChoice(line) && options.Count > 0)
                {
                    var previous = options[^1];
                    options[^1] = previous with { Text = previous.Text + " " + line.Trim() };
                    continue;
                }

                var text = line[column..].Trim();
                var match = NumberedText.Match(text);
                if (i == selectedLine)
                {
                    selected = options.Count;
                }

                options.Add(new QuestionOption(options.Count, match.Success ? match.Groups[1].Value : null, match.Success ? match.Groups[2].Value.Trim() : text));
            }

            if (options.Count < 2 || selected < 0)
            {
                return null;
            }

            // The title is the first line under the last full rule above the choices; the detail, when
            // the question has one, sits between two dashed rules, such as the command to run.
            var rule = Array.FindLastIndex(lines, first, l => l.TrimStart().StartsWith("────"));
            String title = null;
            String detail = null;
            if (rule >= 0)
            {
                title = lines.Skip(rule + 1).Take(first - rule - 1).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0)?.TrimEnd(':');
                var dashed = Array.FindIndex(lines, rule, first - rule, l => l.TrimStart().StartsWith("╌╌╌"));
                if (dashed >= 0)
                {
                    detail = lines.Skip(dashed + 1).Take(first - dashed - 1).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("╌"));
                }
            }

            return new Question(title, detail, options, selected);
        }
    }
}
