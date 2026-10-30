namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    // An issue's pull request as GitHub reports it. State is OPEN, MERGED or CLOSED; Checks is
    // passing, failing, pending or none; ReviewDecision is APPROVED, CHANGES_REQUESTED,
    // REVIEW_REQUIRED or empty.
    public sealed record PrInfo(Int32 Number, String Url, String State, Boolean IsDraft, String ReviewDecision, String Checks);

    // Reads your pull requests through the GitHub CLI, once a minute, for the repos your issue tabs
    // work in. Without gh, or without a GitHub remote, an issue simply has no pull request.
    public static class PullRequests
    {
        private static readonly Regex GitHubRemote = new(@"github\.com[:/]([^/\s]+/[^/\s]+?)(?:\.git)?/?$", RegexOptions.Compiled);
        private static readonly String[] Failing = { "FAILURE", "ERROR", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED", "STARTUP_FAILURE" };
        private static readonly String[] Passing = { "SUCCESS", "NEUTRAL", "SKIPPED" };

        private static readonly Object Gate = new();
        private static HashSet<String> _tracked = new();
        private static Dictionary<String, (String Branch, String Repo)> _checkouts = new();
        private static Dictionary<String, Dictionary<String, PrInfo>> _byRepo = new();
        private static Timer _timer;
        private static Int32 _refreshing;

        // Raised when a pull request changed, so the keys can show it.
        public static event Action Changed;

        public static String GhBinary =>
            new[] { "/opt/homebrew/bin/gh", "/usr/local/bin/gh", "/usr/bin/gh" }.FirstOrDefault(File.Exists);

        // The folders of the issue tabs on screen; new ones are read right away, the rest once a minute.
        public static void Track(IEnumerable<String> cwds)
        {
            var fresh = new HashSet<String>(cwds.Where(c => !String.IsNullOrEmpty(c)));
            Boolean hasNew;
            lock (Gate)
            {
                hasNew = fresh.Any(c => !_tracked.Contains(c));
                _tracked = fresh;
                _timer ??= new Timer(_ => Refresh(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }

            if (hasNew)
            {
                Task.Run(Refresh);
            }
        }

        public static void Stop()
        {
            lock (Gate)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        public static PrInfo Lookup(String cwd)
        {
            if (cwd == null)
            {
                return null;
            }

            lock (Gate)
            {
                return _checkouts.TryGetValue(cwd, out var checkout) && checkout.Branch.Length > 0
                    && _byRepo.TryGetValue(checkout.Repo, out var prs) && prs.TryGetValue(checkout.Branch, out var pr)
                    ? pr
                    : null;
            }
        }

        public static void Refresh()
        {
            if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            {
                return;
            }

            try
            {
                var gh = GhBinary;
                if (gh == null)
                {
                    return;
                }

                String[] cwds;
                lock (Gate)
                {
                    cwds = _tracked.ToArray();
                }

                // The real branch of each worktree: a tab's label can be stale.
                var checkouts = new Dictionary<String, (String Branch, String Repo)>();
                foreach (var cwd in cwds)
                {
                    var branch = HerdrClient.RunTool(cwd, "/usr/bin/git", "-C", cwd, "branch", "--show-current").Output?.Trim() ?? "";
                    var repo = RepoSlug(HerdrClient.RunTool(cwd, "/usr/bin/git", "-C", cwd, "remote", "get-url", "origin").Output ?? "");
                    if (repo != null)
                    {
                        checkouts[cwd] = (branch, repo);
                    }
                }

                var byRepo = new Dictionary<String, Dictionary<String, PrInfo>>();
                foreach (var repo in checkouts.Values.Select(c => c.Repo).Distinct())
                {
                    // Only your own pull requests: a team repo has more open ones than any limit holds.
                    var open = HerdrClient.RunTool(null, gh, "pr", "list", "--repo", repo, "--author", "@me", "--state", "open", "--limit", "50",
                        "--json", "number,url,state,headRefName,isDraft,reviewDecision,statusCheckRollup");
                    var merged = HerdrClient.RunTool(null, gh, "pr", "list", "--repo", repo, "--author", "@me", "--state", "merged", "--limit", "30",
                        "--json", "number,url,state,headRefName");
                    if (!open.Ok)
                    {
                        PluginLog.Warning($"gh pr list for {repo} failed: {open.Error}");
                        lock (Gate)
                        {
                            // Keep what we knew rather than blank the keys on one failed call.
                            byRepo[repo] = _byRepo.GetValueOrDefault(repo) ?? new Dictionary<String, PrInfo>();
                        }

                        continue;
                    }

                    var prs = new Dictionary<String, PrInfo>();
                    foreach (var (branch, pr) in Parse(open.Output).Concat(merged.Ok ? Parse(merged.Output) : Enumerable.Empty<(String, PrInfo)>()))
                    {
                        prs.TryAdd(branch, pr);
                    }

                    byRepo[repo] = prs;
                }

                Boolean changed;
                lock (Gate)
                {
                    changed = !SameContent(_byRepo, byRepo) || !_checkouts.OrderBy(k => k.Key).SequenceEqual(checkouts.OrderBy(k => k.Key));
                    _byRepo = byRepo;
                    _checkouts = checkouts;
                }

                PluginLog.Info($"Pull requests: {byRepo.Sum(r => r.Value.Count)} in {byRepo.Count} repo(s), {checkouts.Count} issue folder(s)");
                if (changed)
                {
                    Changed?.Invoke();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Reading pull requests failed");
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        }

        // "owner/name" from a GitHub remote URL, or null for any other remote.
        public static String RepoSlug(String remoteUrl)
        {
            var match = GitHubRemote.Match(remoteUrl?.Trim() ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }

        // The pull requests in gh's JSON, keyed by their branch, newest first as gh lists them.
        public static List<(String Branch, PrInfo Pr)> Parse(String json)
        {
            var result = new List<(String, PrInfo)>();
            using var doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(json) ? "[]" : json);
            foreach (var pr in doc.RootElement.EnumerateArray())
            {
                result.Add((
                    Str(pr, "headRefName") ?? "",
                    new PrInfo(
                        pr.TryGetProperty("number", out var n) ? n.GetInt32() : 0,
                        Str(pr, "url"),
                        Str(pr, "state") ?? "OPEN",
                        pr.TryGetProperty("isDraft", out var d) && d.ValueKind == JsonValueKind.True,
                        Str(pr, "reviewDecision") ?? "",
                        pr.TryGetProperty("statusCheckRollup", out var roll) ? ChecksOf(roll) : "none")));
            }

            return result;
        }

        // One word for all checks: failing wins, then pending, then passing.
        public static String ChecksOf(JsonElement rollup)
        {
            if (rollup.ValueKind != JsonValueKind.Array)
            {
                return "none";
            }

            var seen = new HashSet<String>();
            foreach (var check in rollup.EnumerateArray())
            {
                var value = (Str(check, "conclusion") is { Length: > 0 } c ? c : Str(check, "state") ?? Str(check, "status") ?? "").ToUpperInvariant();
                seen.Add(Failing.Contains(value) ? "failing" : Passing.Contains(value) ? "passing" : "pending");
            }

            return seen.Contains("failing") ? "failing" : seen.Contains("pending") ? "pending" : seen.Contains("passing") ? "passing" : "none";
        }

        private static Boolean SameContent(Dictionary<String, Dictionary<String, PrInfo>> a, Dictionary<String, Dictionary<String, PrInfo>> b) =>
            a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var other)
                && kv.Value.Count == other.Count && kv.Value.All(p => other.TryGetValue(p.Key, out var q) && q == p.Value));

        private static String Str(JsonElement element, String name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
