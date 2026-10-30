namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum KeyStyle
    {
        // Dark key with a coloured stripe on top.
        Normal,

        // Filled with its colour: the action that fits the issue right now.
        Highlight,

        // Greyed out: an action that does nothing in the issue's current state.
        Dim,
    }

    // How one key looks. Framed marks the project or tab that is on screen.
    public readonly record struct KeyLook(String Title, String Subtitle, BitmapColor Accent, KeyStyle Style = KeyStyle.Normal, Boolean Framed = false);

    // A key drawn around an icon, for the keys you place on your own page: a line icon in its colour,
    // a label under it, and an optional count in a coloured badge at the top right.
    public readonly record struct IconKey(String Icon, BitmapColor Color, String Label, String Badge = null, BitmapColor BadgeColor = default);

    // Draws every key the plugin shows, in one style.
    public static class KeyArt
    {
        public static BitmapImage Draw(IconKey key, PluginImageSize imageSize)
        {
            using var builder = new BitmapBuilder(imageSize);
            var w = builder.Width;
            var h = builder.Height;
            builder.Clear(AgentStatus.Ground);

            var iconSize = h * 2 / 5;
            var iconTop = h / 6;
            var icon = Icons.Render(key.Icon, key.Color, iconSize);
            if (icon != null)
            {
                builder.DrawImage(icon, (w - iconSize) / 2, iconTop, iconSize, iconSize, BitmapRotation.None);
            }

            var labelTop = iconTop + iconSize + h / 20;
            builder.DrawText(Wrap(key.Label, 12, 2), 2, labelTop, w - 4, h - labelTop - 2, AgentStatus.Text);

            if (key.Badge != null)
            {
                var radius = Math.Max(9, h / 8);
                var cx = w - radius - h / 12;
                var cy = radius + h / 14;
                builder.FillCircle(cx, cy, radius, key.BadgeColor);

                // The default font is too big for the badge, so the count gets a size of its own.
                builder.DrawText(key.Badge, cx - radius, cy - radius + radius / 4, radius * 2, radius * 2, AgentStatus.Ground, fontSize: radius * 5 / 4);
            }

            return builder.ToImage();
        }

        public static BitmapImage Draw(KeyLook look, PluginImageSize imageSize)
        {
            using var builder = new BitmapBuilder(imageSize);
            var w = builder.Width;
            var h = builder.Height;
            var ground = look.Style == KeyStyle.Highlight ? look.Accent : AgentStatus.Ground;
            var ink = look.Style switch
            {
                KeyStyle.Highlight => AgentStatus.Ground,
                KeyStyle.Dim => AgentStatus.Overlay,
                _ => AgentStatus.Text,
            };
            var subInk = look.Style switch
            {
                KeyStyle.Highlight => AgentStatus.Ground,
                KeyStyle.Dim => AgentStatus.Overlay,
                _ => look.Accent,
            };

            builder.Clear(ground);
            var stripe = Math.Max(4, h / 12);
            if (look.Style != KeyStyle.Highlight)
            {
                builder.FillRectangle(0, 0, w, stripe, look.Style == KeyStyle.Dim ? AgentStatus.Overlay : look.Accent);
            }

            if (look.Framed)
            {
                for (var i = 0; i < 3; i++)
                {
                    builder.DrawRectangle(i, i, w - 1 - 2 * i, h - 1 - 2 * i, AgentStatus.Text);
                }
            }

            var pad = Math.Max(4, w / 18);
            var titleHeight = look.Subtitle == null ? h - stripe - pad : (h - stripe) * 3 / 5;
            builder.DrawText(Wrap(look.Title), pad, stripe, w - 2 * pad, titleHeight, ink);
            if (look.Subtitle != null)
            {
                builder.DrawText(Wrap(look.Subtitle, 13, 2), pad, stripe + titleHeight, w - 2 * pad, h - stripe - titleHeight - pad, subInk);
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
