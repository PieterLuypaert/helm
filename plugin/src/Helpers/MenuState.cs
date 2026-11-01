namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Where the keypad's herdr menu is and how it moves, without the Logi Plugin Service, so tests can hold
    // every rule. Three levels: your projects; a project's tabs, or the overview of every issue; and one
    // issue's page. The folder keeps the lock: call everything here under it.
    public sealed class MenuState
    {
        public const String ProjectPrefix = "proj:";
        public const String TabPrefix = "tab:";
        public const String ActionPrefix = "act:";
        public const String AnswerPrefix = "ans:";
        public const String EmptyPrefix = "empty:";
        public const String HeaderParam = "header";
        public const String BackToProjectParam = "back:project";
        public const String BackToProjectsParam = "back:projects";
        public const String OfflineParam = "offline";
        public const String MoreParam = "more";
        public const String OnScreenParam = "on-screen";

        // The keypad has nine keys, and Logi puts its own back key, which leaves the menu, on the first of
        // them on every page, so eight are ours. The last of ours goes one level up; with more items than
        // fit, six items and "more" share a page before it.
        public const Int32 Keys = 8;

        // The overview's order, fixed while you page through it and visit its issues: sorted afresh only
        // when you open it, so a status change does not move keys between pages under your fingers.
        private List<String> _overviewOrder;
        private String _lastSeenFocus;

        public Snapshot Snapshot { get; private set; } = Snapshot.Empty;

        public String ProjectId { get; private set; }

        public String TabId { get; private set; }

        public Int32 Page { get; private set; }

        // True while you browse the overview of all issues rather than one project.
        public Boolean All { get; private set; }

        // The page of the list you opened the issue from, for the key one level up.
        public Int32 ReturnPage { get; private set; }

        public Int32 Level => this.TabId != null ? 2 : this.All || this.ProjectId != null ? 1 : 0;

        public Project SelectedProject => this.Snapshot.FindProject(this.ProjectId);

        public Issue SelectedTab => this.SelectedProject?.Tabs.FirstOrDefault(t => t.TabId == this.TabId);

        // The Projects key always opens on your projects.
        public void OpenOnProjects()
        {
            this.ProjectId = null;
            this.TabId = null;
            this.Page = 0;
            this.All = false;
        }

        // A key asked for a page before the menu opened: the overview, or a project or tab.
        public void OpenOn(String workspaceId, String tabId)
        {
            this.All = workspaceId == HerdrState.AllIssues;
            this._overviewOrder = null;
            this.ProjectId = this.All ? null : workspaceId;
            this.TabId = this.All ? null : tabId;
            this.Page = 0;
        }

        public void PressProject(String workspaceId)
        {
            this.All = false;
            this.GoTo(workspaceId, null);
        }

        // In the overview a tab can belong to any project: it opens in its own.
        public void PressTab(String tabId)
        {
            var owner = this.Snapshot.FindTab(tabId).Project;
            this.ReturnPage = this.Page;
            this.GoTo(owner?.WorkspaceId ?? this.SelectedProject?.WorkspaceId, tabId);
        }

        // Back where you came from: the overview of all issues, or the issue's project, on the page you
        // opened the issue from.
        public void UpToList()
        {
            this.Page = this.ReturnPage;
            this.GoTo(this.All ? null : this.SelectedProject?.WorkspaceId, null, keepPage: true);
        }

        public void UpToProjects()
        {
            this.All = false;
            this.GoTo(null, null);
        }

        public void More()
        {
            this.Page++;
            this.GoTo(this.SelectedProject?.WorkspaceId, this.SelectedTab?.TabId, keepPage: true);
        }

        // Reads herdr's latest state without following the screen, such as just before the menu opens.
        public void Use(Snapshot snapshot) => this.Snapshot = snapshot;

        // Takes a new snapshot of herdr. On an issue page, the keypad follows the tab herdr shows. It reacts
        // to a change on the screen only, so a key press is not undone while herdr still shows the previous
        // tab. While herdr cannot be read, the keys say so but keep your place for when it is back.
        public void See(Snapshot snapshot, Boolean followScreen = false)
        {
            this.Snapshot = snapshot;
            var focus = snapshot.FocusedTabId;
            var screenMoved = !snapshot.Offline && focus != this._lastSeenFocus;
            if (!snapshot.Offline)
            {
                this._lastSeenFocus = focus;
            }

            if ((screenMoved || followScreen) && this.Level == 2 && focus != null && focus != this.TabId)
            {
                var (project, tab) = snapshot.FindTab(focus);
                if (tab != null)
                {
                    if (!this.All && project.WorkspaceId != this.ProjectId)
                    {
                        this.ReturnPage = 0;
                    }

                    this.ProjectId = project.WorkspaceId;
                    this.TabId = tab.TabId;
                }
            }

            // A closed tab drops you back to its project, or the overview; a closed workspace back to the
            // projects.
            if (!snapshot.Offline)
            {
                if (this.ProjectId != null && this.SelectedProject == null)
                {
                    this.ProjectId = null;
                    this.TabId = null;
                }
                else if (this.TabId != null && this.SelectedTab == null)
                {
                    this.TabId = null;
                    if (this.All)
                    {
                        this.ProjectId = null;
                    }
                }
            }
        }

        // The keys on the current page, in order, at most eight: Logi's own back key comes before them.
        public List<String> KeysOnPage(IReadOnlyList<IssueAction> actions)
        {
            if (this.Snapshot.Offline)
            {
                return new List<String> { OfflineParam };
            }

            switch (this.Level)
            {
                case 2:
                    var names = new List<String> { HeaderParam };
                    var tab = this.SelectedTab;
                    if (tab?.Status == AgentStatus.Waiting)
                    {
                        // A waiting agent's page is its question: one key per choice, as its screen shows them.
                        // The screen already shows the issue, so "answer on screen" only comes up when the keys
                        // cannot hold the question: helm cannot read it, or it has more choices than fit.
                        var choices = tab.Question?.Options.Count ?? 0;
                        var fits = choices > 0 && choices <= Keys - 2;
                        names.AddRange(Enumerable.Range(0, fits ? choices : Math.Min(choices, Keys - 3)).Select(i => FolderKeys.Keyed(AnswerPrefix, i, tab.TabId)));
                        if (!fits)
                        {
                            names.Add(OnScreenParam);
                        }
                    }
                    else if (tab != null)
                    {
                        // Only the actions that fit the issue now, at most six between the issue and the way up.
                        names.AddRange(Enumerable.Range(0, actions.Count).Where(i => Fits(actions[i], tab)).Take(Keys - 2).Select(i => FolderKeys.Keyed(ActionPrefix, i, tab.TabId)));
                    }

                    return WithBack(names, BackToProjectParam);
                case 1 when this.All:
                    return WithBack(this.Paged(this.OverviewOrder().Select(id => TabPrefix + id).ToList()), BackToProjectsParam);
                case 1:
                    var tabs = this.SelectedProject?.Tabs ?? Array.Empty<Issue>();
                    return WithBack(this.Paged(tabs.Select(t => TabPrefix + t.TabId).ToList()), BackToProjectsParam);
                default:
                    // Your projects alone; Logi adds its own back key. The overview has its own key on your page.
                    return this.Paged(this.Snapshot.Projects.Select(p => ProjectPrefix + p.WorkspaceId).ToList());
            }
        }

        // "more 2/3": the page you are on and how many the current list has.
        public String MoreLabel()
        {
            var count = this.All ? this.OverviewOrder().Count : this.Level == 1 ? this.SelectedProject?.Tabs.Count ?? 0 : this.Snapshot.Projects.Count;
            return $"more {this.Page + 1}/{PageCount(count)}";
        }

        // Whether an action belongs on the issue's page now: open PR only with a pull request, and an action
        // with onlyWhen only in those statuses. The page shows nothing else, so nothing is greyed out.
        public static Boolean Fits(IssueAction action, Issue tab)
        {
            var status = tab?.Status ?? AgentStatus.Unknown;
            if (action.Type == "open-pr" && tab?.Pr == null)
            {
                return false;
            }

            return action.OnlyWhen == null || action.OnlyWhen.Count == 0 || action.OnlyWhen.Contains(status);
        }

        private void GoTo(String projectId, String tabId, Boolean keepPage = false)
        {
            this.ProjectId = projectId;
            this.TabId = tabId;
            if (!keepPage)
            {
                this.Page = 0;
            }
        }

        private static List<String> WithBack(List<String> keys, String back) => FolderKeys.Fill(keys, Keys - 1, EmptyPrefix).Append(back).ToList();

        // Every tab in the overview's fixed order.
        private List<String> OverviewOrder()
        {
            this._overviewOrder = FolderKeys.KeepOrder(this._overviewOrder, this.Snapshot.AllTabs().Select(x => x.Tab.TabId).ToList());
            return this._overviewOrder;
        }

        // The items on the current page, followed by "more" when they do not all fit beside the way up.
        private List<String> Paged(List<String> items)
        {
            if (items.Count <= Keys - 1)
            {
                this.Page = 0;
                return items;
            }

            // The last page fills up with empty keys, so more stays on the same key on every page.
            this.Page %= PageCount(items.Count);
            var page = items.Skip(this.Page * (Keys - 2)).Take(Keys - 2).ToList();
            return FolderKeys.Fill(page, Keys - 2, EmptyPrefix + "m").Append(MoreParam).ToList();
        }

        private static Int32 PageCount(Int32 items) => items <= Keys - 1 ? 1 : (items + Keys - 3) / (Keys - 2);
    }
}
