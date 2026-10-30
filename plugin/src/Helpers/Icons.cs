namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Line icons for every key, as SVG on a 24 by 24 grid. All but Helm come from Tabler Icons
    // (https://tabler.io/icons), MIT licence, copyright (c) 2020-2024 Paweł Kuna; see THIRD_PARTY_NOTICES.md.
    // Each comment names the Tabler icon. Helm, a ship's wheel, is drawn for helm in the same style.
    public static class Icons
    {
        // Tabler: stack-2
        public const String Stack = "<path d=\"M12 4l-8 4l8 4l8 -4l-8 -4\"/><path d=\"M4 12l8 4l8 -4\"/><path d=\"M4 16l8 4l8 -4\"/>";

        // Tabler: bell
        public const String Bell = "<path d=\"M10 5a2 2 0 1 1 4 0a7 7 0 0 1 4 6v3a4 4 0 0 0 2 3h-16a4 4 0 0 0 2 -3v-3a7 7 0 0 1 4 -6\"/><path d=\"M9 17v1a3 3 0 0 0 6 0v-1\"/>";

        // Tabler: bell-ringing
        public const String BellRinging = "<path d=\"M10 5a2 2 0 0 1 4 0a7 7 0 0 1 4 6v3a4 4 0 0 0 2 3h-16a4 4 0 0 0 2 -3v-3a7 7 0 0 1 4 -6\"/><path d=\"M9 17v1a3 3 0 0 0 6 0v-1\"/><path d=\"M21 6.727a11.05 11.05 0 0 0 -2.794 -3.727\"/><path d=\"M3 6.727a11.05 11.05 0 0 1 2.792 -3.727\"/>";

        // Tabler: bell-off
        public const String BellOff = "<path d=\"M9.346 5.353c.21 -.129 .428 -.246 .654 -.353a2 2 0 1 1 4 0a7 7 0 0 1 4 6v3m-1 3h-13a4 4 0 0 0 2 -3v-3a6.996 6.996 0 0 1 1.273 -3.707\"/><path d=\"M9 17v1a3 3 0 0 0 6 0v-1\"/><path d=\"M3 3l18 18\"/>";

        // Tabler: terminal-2
        public const String Terminal = "<path d=\"M8 9l3 3l-3 3\"/><path d=\"M13 15l3 0\"/><path d=\"M3 4m0 2a2 2 0 0 1 2 -2h14a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2z\"/>";

        // Tabler: folder
        public const String Folder = "<path d=\"M5 4h4l3 3h7a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-11a2 2 0 0 1 2 -2\"/>";

        // Tabler: home
        public const String Home = "<path d=\"M5 12l-2 0l9 -9l9 9l-2 0\"/><path d=\"M5 12v7a2 2 0 0 0 2 2h10a2 2 0 0 0 2 -2v-7\"/><path d=\"M9 21v-6a2 2 0 0 1 2 -2h2a2 2 0 0 1 2 2v6\"/>";

        // Tabler: message-question
        public const String Question = "<path d=\"M8 9h8\"/><path d=\"M8 13h6\"/><path d=\"M14 18h-1l-5 3v-3h-2a3 3 0 0 1 -3 -3v-8a3 3 0 0 1 3 -3h12a3 3 0 0 1 3 3v4.5\"/><path d=\"M19 22v.01\"/><path d=\"M19 19a2.003 2.003 0 0 0 .914 -3.782a1.98 1.98 0 0 0 -2.414 .483\"/>";

        // Tabler: list-check
        public const String Plan = "<path d=\"M3.5 5.5l1.5 1.5l2.5 -2.5\"/><path d=\"M3.5 11.5l1.5 1.5l2.5 -2.5\"/><path d=\"M3.5 17.5l1.5 1.5l2.5 -2.5\"/><path d=\"M11 6l9 0\"/><path d=\"M11 12l9 0\"/><path d=\"M11 18l9 0\"/>";

        // Tabler: loader-2
        public const String Working = "<path d=\"M12 3a9 9 0 1 0 9 9\"/>";

        // Tabler: check
        public const String Check = "<path d=\"M5 12l5 5l10 -10\"/>";

        // Tabler: checks
        public const String Checks = "<path d=\"M7 12l5 5l10 -10\"/><path d=\"M2 12l5 5m5 -5l5 -5\"/>";

        // Tabler: git-merge
        public const String Merge = "<path d=\"M7 18m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0\"/><path d=\"M7 6m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0\"/><path d=\"M17 12m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0\"/><path d=\"M7 8l0 8\"/><path d=\"M7 8a4 4 0 0 0 4 4h4\"/>";

        // Tabler: alert-triangle
        public const String Alert = "<path d=\"M12 9v4\"/><path d=\"M10.363 3.591l-8.106 13.534a1.914 1.914 0 0 0 1.636 2.871h16.214a1.914 1.914 0 0 0 1.636 -2.87l-8.106 -13.536a1.914 1.914 0 0 0 -3.274 0z\"/><path d=\"M12 16h.01\"/>";

        // Tabler: message-report
        public const String Report = "<path d=\"M18 4a3 3 0 0 1 3 3v8a3 3 0 0 1 -3 3h-5l-5 3v-3h-2a3 3 0 0 1 -3 -3v-8a3 3 0 0 1 3 -3h12z\"/><path d=\"M12 8v3\"/><path d=\"M12 14v.01\"/>";

        // Tabler: eye
        public const String Eye = "<path d=\"M10 12a2 2 0 1 0 4 0a2 2 0 0 0 -4 0\"/><path d=\"M21 12c-2.4 4 -5.4 6 -9 6c-3.6 0 -6.6 -2 -9 -6c2.4 -4 5.4 -6 9 -6c3.6 0 6.6 2 9 6\"/>";

        // Tabler: pencil
        public const String Pencil = "<path d=\"M4 20h4l10.5 -10.5a2.828 2.828 0 1 0 -4 -4l-10.5 10.5v4\"/><path d=\"M13.5 6.5l4 4\"/>";

        // Tabler: zzz
        public const String Sleep = "<path d=\"M4 12h6l-6 8h6\"/><path d=\"M14 4h6l-6 8h6\"/>";

        // Tabler: circle-x
        public const String CircleX = "<path d=\"M12 12m-9 0a9 9 0 1 0 18 0a9 9 0 1 0 -18 0\"/><path d=\"M10 10l4 4m0 -4l-4 4\"/>";

        // Tabler: help-circle
        public const String Help = "<path d=\"M3 12a9 9 0 1 0 18 0a9 9 0 0 0 -18 0\"/><path d=\"M12 16v.01\"/><path d=\"M12 13a2 2 0 0 0 .914 -3.782a1.98 1.98 0 0 0 -2.414 .483\"/>";

        // Tabler: player-play
        public const String Play = "<path d=\"M7 4v16l13 -8z\"/>";

        // Tabler: tool
        public const String Tool = "<path d=\"M7 10h3v-3l-3.5 -3.5a6 6 0 0 1 8 8l6 6a2 2 0 0 1 -3 3l-6 -6a6 6 0 0 1 -8 -8l3.5 3.5\"/>";

        // Tabler: lifebuoy
        public const String Lifebuoy = "<path d=\"M12 12m-4 0a4 4 0 1 0 8 0a4 4 0 1 0 -8 0\"/><path d=\"M12 12m-9 0a9 9 0 1 0 18 0a9 9 0 1 0 -18 0\"/><path d=\"M15 15l3.35 3.35\"/><path d=\"M9 15l-3.35 3.35\"/><path d=\"M5.65 5.65l3.35 3.35\"/><path d=\"M18.35 5.65l-3.35 3.35\"/>";

        // Tabler: git-commit
        public const String Commit = "<path d=\"M12 12m-3 0a3 3 0 1 0 6 0a3 3 0 1 0 -6 0\"/><path d=\"M12 3l0 6\"/><path d=\"M12 15l0 6\"/>";

        // Tabler: player-stop
        public const String Stop = "<path d=\"M5 5m0 2a2 2 0 0 1 2 -2h10a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-10a2 2 0 0 1 -2 -2z\"/>";

        // Tabler: git-pull-request
        public const String PullRequest = "<path d=\"M6 18m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0\"/><path d=\"M6 6m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0\"/><path d=\"M18 18m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0\"/><path d=\"M6 8l0 8\"/><path d=\"M11 6h5a2 2 0 0 1 2 2v8\"/><path d=\"M14 9l-3 -3l3 -3\"/>";

        // Tabler: code
        public const String Code = "<path d=\"M7 8l-4 4l4 4\"/><path d=\"M17 8l4 4l-4 4\"/><path d=\"M14 4l-4 16\"/>";

        // Tabler: arrow-back-up
        public const String Back = "<path d=\"M9 14l-4 -4l4 -4\"/><path d=\"M5 10h11a4 4 0 1 1 0 8h-1\"/>";

        // Tabler: dots
        public const String Dots = "<path d=\"M5 12m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0\"/><path d=\"M12 12m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0\"/><path d=\"M19 12m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0\"/>";

        // Tabler: x
        public const String X = "<path d=\"M18 6l-12 12\"/><path d=\"M6 6l12 12\"/>";

        // Tabler: refresh
        public const String Refresh = "<path d=\"M20 11a8.1 8.1 0 0 0 -15.5 -2m-.5 -4v4h4\"/><path d=\"M4 13a8.1 8.1 0 0 0 15.5 2m.5 4v-4h-4\"/>";

        // Tabler: ban
        public const String Ban = "<path d=\"M12 12m-9 0a9 9 0 1 0 18 0a9 9 0 1 0 -18 0\"/><path d=\"M5.7 5.7l12.6 12.6\"/>";

        // Tabler: robot
        public const String Robot = "<path d=\"M6 4m0 2a2 2 0 0 1 2 -2h8a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-8a2 2 0 0 1 -2 -2z\"/><path d=\"M12 2v2\"/><path d=\"M9 12v9\"/><path d=\"M15 12v9\"/><path d=\"M5 16l4 -2\"/><path d=\"M15 14l4 2\"/><path d=\"M9 18h6\"/><path d=\"M10 8v.01\"/><path d=\"M14 8v.01\"/>";

        // Tabler: plug-connected-x
        public const String Offline = "<path d=\"M20 16l-4 4\"/><path d=\"M7 12l5 5l-1.5 1.5a3.536 3.536 0 1 1 -5 -5l1.5 -1.5z\"/><path d=\"M17 12l-5 -5l1.5 -1.5a3.536 3.536 0 1 1 5 5l-1.5 1.5z\"/><path d=\"M3 21l2.5 -2.5\"/><path d=\"M18.5 5.5l2.5 -2.5\"/><path d=\"M10 11l-2 2\"/><path d=\"M13 14l-2 2\"/><path d=\"M16 16l4 4\"/>";

        // Tabler: device-desktop
        public const String Screen = "<path d=\"M3 5a1 1 0 0 1 1 -1h16a1 1 0 0 1 1 1v10a1 1 0 0 1 -1 1h-16a1 1 0 0 1 -1 -1v-10z\"/><path d=\"M7 20h10\"/><path d=\"M9 16v4\"/><path d=\"M15 16v4\"/>";

        // Tabler: circle-dot
        public const String Dot = "<path d=\"M12 12m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0\"/><path d=\"M12 12m-9 0a9 9 0 1 0 18 0a9 9 0 1 0 -18 0\"/>";

        // A ship's wheel, drawn for helm in Tabler's style.
        public const String Helm = "<circle cx=\"12\" cy=\"12\" r=\"6.2\"/><circle cx=\"12\" cy=\"12\" r=\"2.2\"/><path d=\"M14.20 12.00L21.60 12.00 M13.56 13.56L18.79 18.79 M12.00 14.20L12.00 21.60 M10.44 13.56L5.21 18.79 M9.80 12.00L2.40 12.00 M10.44 10.44L5.21 5.21 M12.00 9.80L12.00 2.40 M13.56 10.44L18.79 5.21\"/>";

        // Every icon by its name, ignoring case, so the action config can pick one: "icon": "play".
        private static readonly Dictionary<String, String> ByName = typeof(Icons)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(String))
            .ToDictionary(f => f.Name, f => (String)f.GetRawConstantValue(), StringComparer.OrdinalIgnoreCase);

        public static String Named(String name) =>
            name != null && ByName.TryGetValue(name.Replace("-", ""), out var body) ? body : null;

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
