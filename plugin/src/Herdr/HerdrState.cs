namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    // What herdr looks like right now: every project with its tabs in the quarterdeck's status words,
    // and the tab that is on screen.
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

        // The tab that needs you most: waiting, then go?, then done; the first project and tab win a tie.
        public (Project Project, Issue Tab) MostUrgent() =>
            this.Projects.SelectMany(p => p.Tabs.Select(t => (Project: p, Tab: t)))
                .Where(x => AgentStatus.NeedsYou(x.Tab.Status))
                .OrderBy(x => AgentStatus.Rank(x.Tab.Status))
                .ThenBy(x => x.Project.Number)
                .ThenBy(x => x.Tab.Number)
                .FirstOrDefault();

        public Int32 Count(String status) => this.Projects.Sum(p => p.Count(status));

        public Boolean Equals(Snapshot other) =>
            other != null && other.Offline == this.Offline && other.FocusedTabId == this.FocusedTabId
            && other.Projects.SequenceEqual(this.Projects);

        public override Int32 GetHashCode() => HashCode.Combine(this.Offline, this.FocusedTabId, this.Projects.Count);
    }

    // Reads herdr every two seconds for the whole plugin, so the folder and the status key always
    // agree, and tells them when anything changed.
    public static class HerdrState
    {
        // The plan format the global CLAUDE.md asks for, as the quarterdeck matches it.
        private static readonly Regex PlanLine = new(@"^\s*(Single PR:|PR 1:)", RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Object Gate = new();
        private static readonly Dictionary<String, (Int64 Seq, Boolean HasPlan)> Plans = new();
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

            var seen = new HashSet<String>();
            var withStatus = projects.Select(p => p with
            {
                Tabs = p.Tabs.Select(t =>
                {
                    if (t.PaneId != null)
                    {
                        seen.Add(t.PaneId);
                    }

                    return t with { Status = AgentStatus.From(t.Status, HasPlan(t)) };
                }).ToList(),
            }).ToList();

            foreach (var gone in Plans.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                Plans.Remove(gone);
            }

            var focused = withStatus.SelectMany(p => p.Tabs).FirstOrDefault(t => t.Focused)?.TabId;
            return new Snapshot(withStatus, false, focused);
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

            var hasPlan = PlanLine.IsMatch(HerdrClient.ReadRecent(tab.PaneId, 30));
            Plans[tab.PaneId] = (tab.StateSeq, hasPlan);
            return hasPlan;
        }
    }
}
