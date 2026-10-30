namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // helm's status words for an issue, from its agent and its pull request, in herdr's palette.
    public static class AgentStatus
    {
        public const String Waiting = "waiting";
        public const String Go = "go?";
        public const String CiFailed = "CI failed";
        public const String Changes = "changes";
        public const String Done = "done";
        public const String Ready = "ready";
        public const String Working = "working";
        public const String Review = "review";
        public const String Draft = "draft";
        public const String Idle = "idle";
        public const String Merged = "merged";
        public const String Closed = "closed";
        public const String Unknown = "unknown";

        // Most urgent first: what waits on you, then what is ready to merge, then the rest.
        private static readonly String[] Urgency = { Waiting, Go, CiFailed, Changes, Done, Ready, Working, Review, Draft, Idle, Merged, Closed, Unknown };

        // What needs you now: the All issues key counts these in its badge.
        private static readonly String[] Yours = { Waiting, Go, CiFailed, Changes, Done };

        // herdr's palette, plus a blue for ready. Each colour stands for what you do next: red answer or
        // fix, orange decide, green look at the result, blue merge, yellow wait, grey nothing for you.
        public static readonly BitmapColor Red = Hex(0xff8080);
        public static readonly BitmapColor Accent = Hex(0xf36a1b);
        public static readonly BitmapColor Yellow = Hex(0xffc799);
        public static readonly BitmapColor Green = Hex(0x99ffe4);
        public static readonly BitmapColor Blue = Hex(0x82aaff);
        public static readonly BitmapColor Overlay = Hex(0x5c5c5c);
        public static readonly BitmapColor Ground = Hex(0x24282d);
        public static readonly BitmapColor Text = Hex(0xebf0ec);
        public static readonly BitmapColor Subtext = Hex(0xafb6c2);

        // The agent wins while it asks or works; then a plan waiting for go (only before there is a pull
        // request); then the pull request: failing checks and requested changes before finished work,
        // and finished work before a pull request that only waits on a review or a merge.
        public static String From(String raw, Boolean hasPlan, PrInfo pr)
        {
            if (raw == "blocked")
            {
                return Waiting;
            }

            if (raw == "working")
            {
                return Working;
            }

            if (hasPlan && pr == null)
            {
                return Go;
            }

            if (pr != null)
            {
                if (pr.State == "MERGED")
                {
                    return Merged;
                }

                if (pr.State == "CLOSED")
                {
                    return Closed;
                }

                if (pr.Checks == "failing")
                {
                    return CiFailed;
                }

                if (pr.ReviewDecision == "CHANGES_REQUESTED")
                {
                    return Changes;
                }

                if (raw == "done")
                {
                    return Done;
                }

                if (pr.IsDraft)
                {
                    return Draft;
                }

                return pr.ReviewDecision == "APPROVED" && pr.Checks is "passing" or "none" ? Ready : Review;
            }

            return raw switch
            {
                "done" => Done,
                "idle" => Idle,
                _ => Unknown,
            };
        }

        public static Int32 Rank(String status)
        {
            var index = Array.IndexOf(Urgency, status);
            return index < 0 ? Urgency.Length : index;
        }

        public static String MostUrgent(IEnumerable<String> statuses) =>
            statuses.OrderBy(Rank).FirstOrDefault() ?? Idle;

        public static Boolean NeedsYou(String status) => Yours.Contains(status);

        public static BitmapColor Color(String status) => status switch
        {
            Waiting or CiFailed or Changes => Red,
            Go => Accent,
            Done => Green,
            Ready => Blue,
            Working => Yellow,
            _ => Overlay,
        };

        // One icon per colour, so a key reads at a glance: what needs you, a plan to decide, finished
        // work, ready to merge, busy, or quiet. The exact status shows on the issue's own page.
        public static String Icon(String status) => status switch
        {
            Waiting or CiFailed or Changes => Icons.Alert,
            Go => Icons.Plan,
            Done => Icons.Check,
            Ready => Icons.Merge,
            Working => Icons.Working,
            _ => Icons.Sleep,
        };

        public static BitmapColor Hex(UInt32 rgb) => new((Int32)(rgb >> 16) & 0xFF, (Int32)(rgb >> 8) & 0xFF, (Int32)rgb & 0xFF);
    }
}
