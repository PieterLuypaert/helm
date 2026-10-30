namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Rules of the herdr menu that need no keypad, so tests can hold them.
    public static class FolderKeys
    {
        // "act:3:wW:tQ": a key's index and the tab it was drawn for, so a press acts on the issue its key
        // showed even when the page has just followed the screen to another one.
        public static String Keyed(String prefix, Int32 index, String tabId) => $"{prefix}{index}:{tabId}";

        // Reads a name made by Keyed. Tab ids hold colons themselves, so only the first one after the
        // index splits.
        public static Boolean TryParseKeyed(String param, String prefix, out Int32 index, out String tabId)
        {
            index = -1;
            tabId = null;
            if (param == null || !param.StartsWith(prefix))
            {
                return false;
            }

            var rest = param[prefix.Length..];
            var colon = rest.IndexOf(':');
            if (colon < 0 || !Int32.TryParse(rest[..colon], out index))
            {
                index = -1;
                return false;
            }

            tabId = rest[(colon + 1)..];
            return tabId.Length > 0;
        }

        // The overview's order: the one it had when you opened it, without closed tabs, and with new tabs
        // after it in their sorted order. With no earlier order, the sorted one.
        public static List<String> KeepOrder(IReadOnlyList<String> earlier, IReadOnlyList<String> sorted)
        {
            if (earlier == null)
            {
                return sorted.ToList();
            }

            var open = sorted.ToHashSet();
            var kept = earlier.Where(open.Contains).ToList();
            var known = kept.ToHashSet();
            return kept.Concat(sorted.Where(id => !known.Contains(id))).ToList();
        }
    }
}
