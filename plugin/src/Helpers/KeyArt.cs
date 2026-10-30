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

        // A key on your own page, beside your app keys: a big icon, or a count over a small icon, in its
        // colour on a black tile. Logi writes the key's short label under it.
        Tile,
    }

    // How one key looks. Logi Options+ shows Title as the key's label under its image, so the image
    // itself is the icon alone. Framed marks the project or tab on screen; Badge is a count at the top right;
    // Count, on a tile, is a number drawn large over a small icon.
    public readonly record struct KeyLook(String Title, String Icon, BitmapColor Accent, KeyStyle Style = KeyStyle.Normal, Boolean Framed = false, String Badge = null, String Count = null);

    // Draws every key the plugin shows. In the menu: an icon on a clear background, with no text in the
    // image, because Logi already writes the label under it. On your own page: a tile, like an app icon.
    public static class KeyArt
    {
        public static BitmapImage Draw(KeyLook look, PluginImageSize imageSize)
        {
            using var builder = new BitmapBuilder(imageSize);
            var w = builder.Width;
            var h = builder.Height;
            builder.Clear(look.Style == KeyStyle.Tile ? TileGround : BitmapColor.Transparent);
            if (look.Style == KeyStyle.Tile)
            {
                DrawTile(builder, look, w, h);
                return builder.ToImage();
            }

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

            DrawBadge(builder, look, w, h);
            return builder.ToImage();
        }

        // Black, like the keypad around it.
        private static readonly BitmapColor TileGround = AgentStatus.Hex(0x000000);

        private static void DrawTile(BitmapBuilder builder, KeyLook look, Int32 w, Int32 h)
        {
            if (look.Count == null)
            {
                var size = (Int32)(w * 0.62f);
                var icon = Icons.Render(look.Icon ?? Icons.Dot, look.Accent, size);
                if (icon != null)
                {
                    builder.DrawImage(icon, (w - size) / 2, (h - size) / 2, size, size, BitmapRotation.None);
                }
            }
            else
            {
                // The count large in the upper part, the icon small under it.
                var fontSize = (Int32)(h * 0.46f);
                builder.DrawText(look.Count, 0, (Int32)(h * 0.13f), w, (Int32)(h * 0.5f), look.Accent, fontSize: fontSize);
                var size = (Int32)(w * 0.28f);
                var icon = Icons.Render(look.Icon ?? Icons.Dot, look.Accent, size);
                if (icon != null)
                {
                    builder.DrawImage(icon, (w - size) / 2, (Int32)(h * 0.64f), size, size, BitmapRotation.None);
                }
            }

            DrawBadge(builder, look, w, h);
        }

        private static void DrawBadge(BitmapBuilder builder, KeyLook look, Int32 w, Int32 h)
        {
            if (look.Badge == null)
            {
                return;
            }

            var radius = Math.Max(9, h / 7);
            var bx = w - radius - 1;
            var by = radius + 1;
            builder.FillCircle(bx, by, radius, look.Accent);

            // The default font is too big for the badge, so the count gets a size of its own.
            builder.DrawText(look.Badge, bx - radius, by - radius + radius / 4, radius * 2, radius * 2, AgentStatus.Ground, fontSize: radius * 5 / 4);
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
