namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    // One action on the issue page. Type is "prompt" (text plus Enter to the issue's agent),
    // "keys" (key presses, space separated, such as "esc" or "enter") or "shell" (a command run
    // in the issue's folder, with $ISSUE_CWD set). When lists the issue statuses in which the key
    // lights up; OnlyWhen, when set, the statuses in which it works at all (it is greyed out otherwise).
    // Statuses are the quarterdeck's: waiting, go?, done, working, idle.
    public sealed class IssueAction
    {
        public String Label { get; set; }
        public String Type { get; set; }
        public String Value { get; set; }
        public String Color { get; set; }
        public List<String> When { get; set; } = new();
        public List<String> OnlyWhen { get; set; }
    }

    public sealed class KeypadConfig
    {
        public static readonly String Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "herdr-keypad", "config.json");

        // The app that runs herdr, brought to the front when you pick an issue. Empty leaves focus alone.
        public String TerminalBundleId { get; set; } = "com.googlecode.iterm2";

        // With no herdr window open, picking a tab opens iTerm with herdr in it instead of a bare shell.
        // Works with iTerm only; other terminals are only brought to the front.
        public Boolean OpenHerdrWhenClosed { get; set; } = true;

        public List<IssueAction> Actions { get; set; } = new()
        {
            new() { Label = "go", Type = "prompt", Value = "go", Color = "#f36a1b", When = new() { "go?" } },
            new() { Label = "toestaan", Type = "keys", Value = "enter", Color = "#ff8080", When = new() { "waiting" }, OnlyWhen = new() { "waiting" } },
            new() { Label = "review\nand fix", Type = "prompt", Value = "/review-and-fix", When = new() { "done" } },
            new() { Label = "code\nreview", Type = "prompt", Value = "/code-review" },
            new() { Label = "commit", Type = "prompt", Value = "Commit the work in several logical commits, each with its own conventional-commit message." },
            new() { Label = "stop", Type = "keys", Value = "esc" },
            new() { Label = "editor", Type = "shell", Value = "\"$HOME/.local/bin/hopen\" \"$ISSUE_CWD\"" },
        };

        private static DateTime _loadedStamp = DateTime.MinValue;
        private static KeypadConfig _cached = new();

        // Reads the config file again whenever it changed, so edits apply without a rebuild.
        public static KeypadConfig Current
        {
            get
            {
                try
                {
                    if (!File.Exists(Path))
                    {
                        return _cached = new KeypadConfig();
                    }

                    // The file is usually a symlink into the helm repo: watch the file it points to.
                    var info = new FileInfo(Path);
                    var target = info.LinkTarget != null ? info.ResolveLinkTarget(true) as FileInfo ?? info : info;
                    var stamp = target.LastWriteTimeUtc;
                    if (stamp != _loadedStamp)
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                        _cached = JsonSerializer.Deserialize<KeypadConfig>(File.ReadAllText(Path), options) ?? new KeypadConfig();
                        _loadedStamp = stamp;
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, $"Could not read {Path}, using the defaults");
                    _cached = new KeypadConfig();
                }

                return _cached;
            }
        }
    }
}
