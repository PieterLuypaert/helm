namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Short key labels for issue branches: drop the type prefix (feat/, fix/) and the
    // leading words most issues share, so "feat/shop-slice-3-sign-up" reads "3 sign up".
    public static class IssueNames
    {
        public static List<String> Shorten(IReadOnlyList<String> branches)
        {
            var tokens = branches.Select(b => b[(b.LastIndexOf('/') + 1)..].Split('-', StringSplitOptions.RemoveEmptyEntries)).ToList();
            var counts = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in tokens.Select(t => t.Distinct(StringComparer.OrdinalIgnoreCase)))
            {
                foreach (var token in set)
                {
                    counts[token] = counts.GetValueOrDefault(token) + 1;
                }
            }

            Boolean IsCommon(String token) => branches.Count >= 2 && counts.GetValueOrDefault(token) * 2 >= branches.Count;

            return tokens.Select(t =>
            {
                var skip = 0;
                while (skip < t.Length - 1 && IsCommon(t[skip]))
                {
                    skip++;
                }

                return t.Length == 0 ? "?" : String.Join(' ', t.Skip(skip));
            }).ToList();
        }
    }
}
