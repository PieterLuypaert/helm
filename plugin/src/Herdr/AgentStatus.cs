namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // The quarterdeck's status words and herdr's palette, so an issue reads the same on the keypad,
    // in herdr and in the quarterdeck.
    public static class AgentStatus
    {
        public const String Waiting = "waiting";
        public const String Go = "go?";
        public const String Done = "done";
        public const String Working = "working";
        public const String Idle = "idle";
        public const String Unknown = "unknown";

        // Most urgent first: what waits on you, then a plan waiting for go, then finished work.
        private static readonly String[] Urgency = { Waiting, Go, Done, Working, Idle, Unknown };

        // herdr's palette, as the quarterdeck uses it.
        public static readonly BitmapColor Red = Hex(0xff8080);
        public static readonly BitmapColor Accent = Hex(0xf36a1b);
        public static readonly BitmapColor Yellow = Hex(0xffc799);
        public static readonly BitmapColor Green = Hex(0x99ffe4);
        public static readonly BitmapColor Overlay = Hex(0x5c5c5c);
        public static readonly BitmapColor Ground = Hex(0x24282d);
        public static readonly BitmapColor Text = Hex(0xebf0ec);
        public static readonly BitmapColor Subtext = Hex(0xafb6c2);

        // herdr's raw agent status plus whether the agent's last output holds a plan.
        // A plan counts only while the agent sits still, as in the quarterdeck.
        public static String From(String raw, Boolean hasPlan) => raw switch
        {
            "blocked" => Waiting,
            "working" => Working,
            _ when hasPlan => Go,
            "done" => Done,
            "idle" => Idle,
            _ => Unknown,
        };

        public static Int32 Rank(String status)
        {
            var index = Array.IndexOf(Urgency, status);
            return index < 0 ? Urgency.Length : index;
        }

        public static String MostUrgent(IEnumerable<String> statuses) =>
            statuses.OrderBy(Rank).FirstOrDefault() ?? Idle;

        public static Boolean NeedsYou(String status) => status is Waiting or Go or Done;

        public static BitmapColor Color(String status) => status switch
        {
            Waiting => Red,
            Go => Accent,
            Done => Accent,
            Working => Yellow,
            _ => Overlay,
        };

        public static BitmapColor Hex(UInt32 rgb) => new((Int32)(rgb >> 16) & 0xFF, (Int32)(rgb >> 8) & 0xFF, (Int32)rgb & 0xFF);
    }
}
