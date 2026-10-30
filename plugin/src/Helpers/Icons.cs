namespace Loupedeck.HerdrPlugin
{
    using System;

    // Line icons for the three herdr keys, as SVG on a 24 by 24 grid. Stack, the bells and Terminal
    // come from Tabler Icons (https://tabler.io/icons), MIT licence, copyright (c) 2020-2024 Paweł Kuna;
    // see THIRD_PARTY_NOTICES.md. Helm, a ship's wheel, is drawn for helm in the same style.
    public static class Icons
    {
        public const String Stack = "<path d=\"M12 4l-8 4l8 4l8 -4l-8 -4\"/><path d=\"M4 12l8 4l8 -4\"/><path d=\"M4 16l8 4l8 -4\"/>";
        public const String Bell = "<path d=\"M10 5a2 2 0 1 1 4 0a7 7 0 0 1 4 6v3a4 4 0 0 0 2 3h-16a4 4 0 0 0 2 -3v-3a7 7 0 0 1 4 -6\"/><path d=\"M9 17v1a3 3 0 0 0 6 0v-1\"/>";
        public const String BellRinging = "<path d=\"M10 5a2 2 0 0 1 4 0a7 7 0 0 1 4 6v3a4 4 0 0 0 2 3h-16a4 4 0 0 0 2 -3v-3a7 7 0 0 1 4 -6\"/><path d=\"M9 17v1a3 3 0 0 0 6 0v-1\"/><path d=\"M21 6.727a11.05 11.05 0 0 0 -2.794 -3.727\"/><path d=\"M3 6.727a11.05 11.05 0 0 1 2.792 -3.727\"/>";
        public const String BellOff = "<path d=\"M9.346 5.353c.21 -.129 .428 -.246 .654 -.353a2 2 0 1 1 4 0a7 7 0 0 1 4 6v3m-1 3h-13a4 4 0 0 0 2 -3v-3a6.996 6.996 0 0 1 1.273 -3.707\"/><path d=\"M9 17v1a3 3 0 0 0 6 0v-1\"/><path d=\"M3 3l18 18\"/>";
        public const String Terminal = "<path d=\"M8 9l3 3l-3 3\"/><path d=\"M13 15l3 0\"/><path d=\"M3 4m0 2a2 2 0 0 1 2 -2h14a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2z\"/>";
        public const String Helm = "<circle cx=\"12\" cy=\"12\" r=\"6.2\"/><circle cx=\"12\" cy=\"12\" r=\"2.2\"/><path d=\"M14.20 12.00L21.60 12.00 M13.56 13.56L18.79 18.79 M12.00 14.20L12.00 21.60 M10.44 13.56L5.21 18.79 M9.80 12.00L2.40 12.00 M10.44 10.44L5.21 5.21 M12.00 9.80L12.00 2.40 M13.56 10.44L18.79 5.21\"/>";

        // A complete SVG of one icon in one colour, drawn as two-pixel lines on the 24 by 24 grid.
        public static String Svg(String body, BitmapColor color) =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">"
            + $"<g fill=\"none\" stroke=\"#{color.R:x2}{color.G:x2}{color.B:x2}\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">"
            + body + "</g></svg>";

        // The icon as an image of the given size. The SDK's SVG reader honours stroke colours through
        // VectorImage; BitmapImage.FromSvg draws everything white.
        public static BitmapImage Render(String body, BitmapColor color, Int32 size)
        {
            var vector = new VectorImage();
            return vector.Load(Svg(body, color)) ? vector.ToBitmap(size, size) : null;
        }
    }
}
