namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum KeyStyle
    {
        // The icon in its colour.
        Normal,

        // A filled circle in its colour behind a dark icon: the action that fits right now.
        Highlight,

        // Greyed out: an action that does nothing in the issue's current state.
        Dim,
    }

    // How one key looks. Logi Options+ shows Title as the key's label under its image, so the image
    // itself is the icon alone. Framed marks the project or tab on screen; Badge is a count at the top right.
    public readonly record struct KeyLook(String Title, String Icon, BitmapColor Accent, KeyStyle Style = KeyStyle.Normal, Boolean Framed = false, String Badge = null);

    // Draws every key the plugin shows, in one style: an icon on a clear background, like an app icon,
    // with no text in the image, because Logi already writes the label under it.
    public static class KeyArt
    {
        public static BitmapImage Draw(KeyLook look, PluginImageSize imageSize)
        {
            using var builder = new BitmapBuilder(imageSize);
            var w = builder.Width;
            var h = builder.Height;
            builder.Clear(BitmapColor.Transparent);

            var cx = w / 2f;
            var cy = h / 2f;
            var highlight = look.Style == KeyStyle.Highlight;
            if (highlight)
            {
                builder.FillCircle(cx, cy, w * 0.46f, look.Accent);
            }

            if (look.Framed)
            {
                for (var i = 0; i < 3; i++)
                {
                    builder.DrawCircle(cx, cy, w * 0.48f - i, AgentStatus.Text);
                }
            }

            var color = highlight ? AgentStatus.Ground : look.Style == KeyStyle.Dim ? AgentStatus.Overlay : look.Accent;
            var size = (Int32)(w * (highlight || look.Framed ? 0.5f : 0.64f));
            var icon = Icons.Render(look.Icon ?? Icons.Dot, color, size);
            if (icon != null)
            {
                builder.DrawImage(icon, (w - size) / 2, (h - size) / 2, size, size, BitmapRotation.None);
            }

            if (look.Badge != null)
            {
                var radius = Math.Max(9, h / 7);
                var bx = w - radius - 1;
                var by = radius + 1;
                builder.FillCircle(bx, by, radius, look.Accent);

                // The default font is too big for the badge, so the count gets a size of its own.
                builder.DrawText(look.Badge, bx - radius, by - radius + radius / 4, radius * 2, radius * 2, AgentStatus.Ground, fontSize: radius * 5 / 4);
            }

            return builder.ToImage();
        }

        // Breaks a label into a few short lines so it fits a key; plain ASCII so every key font has it.
        public static String Wrap(String text, Int32 width = 11, Int32 maxLines = 3)
        {
            if (String.IsNullOrEmpty(text) || text.Contains('\n'))
            {
                return text ?? "";
            }

            var lines = new List<String>();
            var line = "";
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = line.Length == 0 ? word : line + " " + word;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }

            if (lines.Count > maxLines)
            {
                lines = lines.Take(maxLines).ToList();
                var last = lines[^1];
                lines[^1] = (last.Length > width - 2 ? last[..(width - 2)] : last) + "..";
            }

            return String.Join('\n', lines);
        }
    }
}
