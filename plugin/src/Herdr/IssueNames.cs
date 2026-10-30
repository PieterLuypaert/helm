namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Short key labels for issue branches: drop the type prefix (feat/, fix/) and the leading words most
    // issues share, then keep the issue's number and at most two words that carry meaning, so
    // "feat/shop-slice-3-invite-and-accept-members" reads "3 invite accept".
    public static class IssueNames
    {
        private const Int32 Words = 2;

        private static readonly HashSet<String> Filler = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "the", "of", "to", "in", "on", "for", "per", "with", "from", "by", "or", "at", "is",
        };

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

                var rest = t.Skip(skip).ToList();
                if (rest.Count == 0)
                {
                    return "?";
                }

                var number = rest[0].All(Char.IsDigit) ? rest[0] : null;
                var words = rest.Skip(number == null ? 0 : 1).Where(w => !Filler.Contains(w)).Take(Words);
                var name = String.Join(' ', number == null ? words : words.Prepend(number));
                return name.Length > 0 ? name : String.Join(' ', rest);
            }).ToList();
        }
    }
}
