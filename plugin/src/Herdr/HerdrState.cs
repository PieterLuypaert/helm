namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    // What herdr looks like right now: every project with its tabs in helm's status words, and the tab
    // that is on screen.
    public sealed record Snapshot(IReadOnlyList<Project> Projects, Boolean Offline, String FocusedTabId)
    {
        public static readonly Snapshot Empty = new(Array.Empty<Project>(), true, null);

        public Project FindProject(String workspaceId) => this.Projects.FirstOrDefault(p => p.WorkspaceId == workspaceId);

        public (Project Project, Issue Tab) FindTab(String tabId)
        {
            foreach (var project in this.Projects)
            {
                var tab = project.Tabs.FirstOrDefault(t => t.TabId == tabId);
                if (tab != null)
                {
                    return (project, tab);
                }
            }

            return (null, null);
        }

        // Every tab that needs you, most urgent first; the first project and tab win a tie.
        public List<(Project Project, Issue Tab)> NeedsYou() =>
            this.Projects.SelectMany(p => p.Tabs.Select(t => (Project: p, Tab: t)))
                .Where(x => AgentStatus.NeedsYou(x.Tab.Status))
                .OrderBy(x => AgentStatus.Rank(x.Tab.Status))
                .ThenBy(x => x.Project.Number)
                .ThenBy(x => x.Tab.Number)
                .ToList();

        // How many issue tabs there are across all projects; main tabs are not issues.
        public Int32 IssueCount() => this.Projects.Sum(p => p.Tabs.Count(t => !t.IsMain));

        // Every tab across all projects, most urgent first; the first project and tab win a tie. Main tabs
        // come last, unless they need you: they are not issues, but you can still go to them.
        public List<(Project Project, Issue Tab)> AllTabs() =>
            this.Projects.SelectMany(p => p.Tabs.Select(t => (Project: p, Tab: t)))
                .OrderBy(x => x.Tab.IsMain && !AgentStatus.NeedsYou(x.Tab.Status))
                .ThenBy(x => AgentStatus.Rank(x.Tab.Status))
                .ThenBy(x => x.Project.Number)
                .ThenBy(x => x.Tab.Number)
                .ToList();

        public Int32 Count(String status) => this.Projects.Sum(p => p.Count(status));

        public Boolean Equals(Snapshot other) =>
            other != null && other.Offline == this.Offline && other.FocusedTabId == this.FocusedTabId
            && other.Projects.SequenceEqual(this.Projects);

        public override Int32 GetHashCode() => HashCode.Combine(this.Offline, this.FocusedTabId, this.Projects.Count);
    }

    // Reads herdr every two seconds for the whole plugin, so the folder and the All issues key always
    // agree, and tells them when anything changed.
    public static class HerdrState
    {
        private static Regex _planLine;
        private static String _planPattern;

        private static readonly Object Gate = new();
        private static readonly Dictionary<String, (Int64 Seq, Boolean HasPlan)> Plans = new();
        private static readonly Dictionary<String, (Int64 Seq, Question Question)> Asked = new();
        private static Snapshot _current = Snapshot.Empty;
        private static Timer _timer;
        private static Int32 _refreshing;
        private static (String WorkspaceId, String TabId)? _requested;

        public static event Action<Snapshot> Changed;

        // Asks the folder to open on a project or tab, the next time it shows or right away.
        public static event Action NavigationRequested;

        public static Snapshot Current => Volatile.Read(ref _current);

        public static void Start()
        {
            lock (Gate)
            {
                _timer ??= new Timer(_ => Refresh(), null, 0, 2000);
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

        // Asks the folder to open on the overview of all issues.
        public const String AllIssues = "*all*";

        public static void RequestNavigation(String workspaceId, String tabId)
        {
            lock (Gate)
            {
                _requested = (workspaceId, tabId);
            }

            NavigationRequested?.Invoke();
        }

        public static (String WorkspaceId, String TabId)? TakeNavigationRequest()
        {
            lock (Gate)
            {
                var request = _requested;
                _requested = null;
                return request;
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
                var fresh = Read();
                var previous = Interlocked.Exchange(ref _current, fresh);
                if (!fresh.Equals(previous))
                {
                    Changed?.Invoke(fresh);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Reading herdr failed");
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        }

        private static Snapshot Read()
        {
            var projects = HerdrClient.ReadProjects();
            if (projects == null)
            {
                return Snapshot.Empty;
            }

            UsePlanPattern(KeypadConfig.Current.PlanPattern);
            PullRequests.Track(projects.SelectMany(p => p.Tabs).Where(t => !t.IsMain).Select(t => t.Cwd));

            var seen = new HashSet<String>();
            var withStatus = projects.Select(p => p with
            {
                Tabs = p.Tabs.Select(t =>
                {
                    if (t.PaneId != null)
                    {
                        seen.Add(t.PaneId);
                    }

                    var pr = t.IsMain ? null : PullRequests.Lookup(t.Cwd);
                    return t with { Status = AgentStatus.From(t.Status, HasPlan(t), pr), Pr = pr, Question = QuestionOf(t) };
                }).ToList(),
            }).ToList();

            foreach (var gone in Plans.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                Plans.Remove(gone);
            }

            foreach (var gone in Asked.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                Asked.Remove(gone);
            }

            var focused = withStatus.SelectMany(p => p.Tabs).FirstOrDefault(t => t.Focused)?.TabId;
            return new Snapshot(withStatus, false, focused);
        }

        // Reads the question a waiting agent asks, once per state change: its choices go on the keys.
        private static Question QuestionOf(Issue tab)
        {
            if (tab.PaneId == null || tab.Status != "blocked")
            {
                return null;
            }

            if (Asked.TryGetValue(tab.PaneId, out var cached) && cached.Seq == tab.StateSeq)
            {
                return cached.Question;
            }

            var question = Questions.Parse(HerdrClient.ReadRecent(tab.PaneId, 40));
            Asked[tab.PaneId] = (tab.StateSeq, question);
            return question;
        }

        // Compiles the configured plan pattern once; a pattern that does not compile falls back to the default.
        private static void UsePlanPattern(String pattern)
        {
            pattern = String.IsNullOrWhiteSpace(pattern) ? new KeypadConfig().PlanPattern : pattern;
            if (pattern == _planPattern)
            {
                return;
            }

            try
            {
                _planLine = new Regex(pattern, RegexOptions.Multiline);
            }
            catch (ArgumentException ex)
            {
                PluginLog.Warning(ex, $"planPattern does not compile, using the default: {pattern}");
                _planLine = new Regex(new KeypadConfig().PlanPattern, RegexOptions.Multiline);
            }

            _planPattern = pattern;
            Plans.Clear();
        }

        // Reads an idle or finished agent's last lines once per state change, to see whether a plan waits.
        private static Boolean HasPlan(Issue tab)
        {
            if (tab.PaneId == null || tab.Status is "blocked" or "working")
            {
                return false;
            }

            if (Plans.TryGetValue(tab.PaneId, out var cached) && cached.Seq == tab.StateSeq)
            {
                return cached.HasPlan;
            }

            var hasPlan = _planLine.IsMatch(HerdrClient.ReadRecent(tab.PaneId, 30));
            Plans[tab.PaneId] = (tab.StateSeq, hasPlan);
            return hasPlan;
        }
    }
}
