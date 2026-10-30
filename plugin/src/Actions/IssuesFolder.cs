namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    // The keypad's herdr menu, three levels deep: every project (herdr workspace); a project's main tab
    // and issue tabs; and the actions for the tab you picked. The keypad and the screen show the same
    // issue: a key press moves herdr, and on an issue page the keypad follows the tab herdr shows.
    public class IssuesFolder : PluginDynamicFolder
    {
        private const String ProjectPrefix = "proj:";
        private const String TabPrefix = "tab:";
        private const String ActionPrefix = "act:";
        private const String HeaderParam = "header";
        private const String BackToProjectParam = "back:project";
        private const String BackToProjectsParam = "back:projects";
        private const String OfflineParam = "offline";
        private const String MoreParam = "more";
        private const String AnswerPrefix = "ans:";
        private const String OnScreenParam = "on-screen";

        // Nine keys: with more items than fit, seven items, "meer" and the way back share a page.
        private const Int32 Keys = 9;

        private static readonly TimeSpan FlashTime = TimeSpan.FromMilliseconds(1500);

        private readonly Object _lock = new();
        private readonly Dictionary<String, KeyLook> _flashes = new();
        private Snapshot _snapshot = Snapshot.Empty;
        private String _projectId;
        private String _tabId;
        private Int32 _page;
        private String _lastSeenFocus;
        private Boolean _active;
        private List<String> _shownParams = new();
        private Dictionary<String, KeyLook> _shownLooks = new();

        public IssuesFolder()
        {
            this.DisplayName = "Herdr issues";
            this.Description = "Your herdr projects, their issues, and actions for the one you pick";
            this.GroupName = "Herdr";
        }

        // The action name that opens this folder, for the status key.
        public static String ActionName { get; private set; }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.None;

        public override Boolean Load()
        {
            ActionName = this.Name;
            PluginLog.Info($"Herdr folder action name: {this.Name}");
            HerdrState.NavigationRequested += this.OnNavigationRequested;
            return true;
        }

        public override Boolean Unload()
        {
            HerdrState.NavigationRequested -= this.OnNavigationRequested;
            return true;
        }

        public override Boolean Activate()
        {
            Boolean requested;
            lock (this._lock)
            {
                this._active = true;
                requested = this.ApplyNavigationRequest();
            }

            HerdrState.Changed += this.OnChanged;

            // Opening the folder lands on the tab on screen, unless the status key just picked one.
            this.Update(HerdrState.Current, followScreen: !requested);
            return true;
        }

        public override Boolean Deactivate()
        {
            HerdrState.Changed -= this.OnChanged;
            lock (this._lock)
            {
                this._active = false;
            }

            return true;
        }

        // The key that opens this folder, in herdr's accent: it opens the herdr menu on the keypad,
        // unlike Open herdr, which opens herdr itself on screen.
        public override String GetButtonDisplayName(PluginImageSize imageSize) => "Issues";

        public override BitmapImage GetButtonImage(PluginImageSize imageSize) =>
            KeyArt.Draw(new KeyLook("Issues", "herdr menu", AgentStatus.Accent), imageSize);

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            lock (this._lock)
            {
                var names = this.CurrentParams().Select(p => this.CreateCommandName(p)).ToList();
                if (this.Level() == 0)
                {
                    names.Add(PluginDynamicFolder.NavigateUpActionName);
                }

                return names;
            }
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.Look(actionParameter).Title?.Replace('\n', ' ') ?? "";

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(this.Look(actionParameter), imageSize);

        public override void RunCommand(String actionParameter)
        {
            if (actionParameter == null)
            {
                return;
            }

            Project project;
            Issue tab;
            lock (this._lock)
            {
                project = this.SelectedProject();
                tab = this.SelectedTab();
            }

            if (actionParameter.StartsWith(ProjectPrefix))
            {
                var id = actionParameter[ProjectPrefix.Length..];
                this.Navigate(id, null);
                Task.Run(() => HerdrClient.FocusWorkspace(id));
            }
            else if (actionParameter.StartsWith(TabPrefix))
            {
                var id = actionParameter[TabPrefix.Length..];
                this.Navigate(project?.WorkspaceId, id);
                Task.Run(() => Screen.ShowTab(id));
            }
            else if (actionParameter == BackToProjectParam)
            {
                this.Navigate(project?.WorkspaceId, null);
            }
            else if (actionParameter == BackToProjectsParam)
            {
                this.Navigate(null, null);
            }
            else if (actionParameter == MoreParam)
            {
                lock (this._lock)
                {
                    this._page++;
                }

                this.Navigate(project?.WorkspaceId, tab?.TabId, keepPage: true);
            }
            else if (actionParameter.StartsWith(AnswerPrefix) && tab?.Question != null
                     && Int32.TryParse(actionParameter[AnswerPrefix.Length..], out var choice))
            {
                this.Answer(actionParameter, tab, choice);
            }
            else if (actionParameter == OnScreenParam && tab != null)
            {
                Task.Run(() => Screen.ShowTab(tab.TabId));
            }
            else if (actionParameter == HeaderParam && tab != null)
            {
                Task.Run(() => Screen.ShowTab(tab.TabId));
            }
            else if (actionParameter == OfflineParam)
            {
                Task.Run(HerdrState.Refresh);
            }
            else if (actionParameter.StartsWith(ActionPrefix) && tab != null
                     && Int32.TryParse(actionParameter[ActionPrefix.Length..], out var index))
            {
                var actions = KeypadConfig.Current.Actions;
                if (index >= 0 && index < actions.Count)
                {
                    this.RunAction(actionParameter, actions[index], tab);
                }
            }
        }

        // Sends the action to the issue's agent and shows how it went: on the key, and in herdr.
        private void RunAction(String param, IssueAction action, Issue tab)
        {
            var label = (action.Label ?? action.Value ?? "action").Replace('\n', ' ');
            if (StyleFor(action, tab) == KeyStyle.Dim)
            {
                this.Flash(param, new KeyLook("not now", tab.Status, AgentStatus.Overlay, KeyStyle.Highlight));
                return;
            }

            Task.Run(() =>
            {
                PluginLog.Info($"{label} on {tab.Branch}");
                var result = action.Type?.ToLowerInvariant() switch
                {
                    "prompt" when tab.PaneId != null => HerdrClient.Prompt(tab.PaneId, action.Value ?? ""),
                    "keys" when tab.PaneId != null => HerdrClient.SendKeys(tab.PaneId, (action.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                    "shell" => HerdrClient.Shell(action.Value ?? "", tab.Cwd),
                    "open-pr" when tab.Pr?.Url != null => HerdrClient.RunTool(null, "/usr/bin/open", tab.Pr.Url),
                    _ => new CommandResult(false, null, $"unknown type {action.Type}"),
                };

                this.Flash(param, result.Ok
                    ? new KeyLook("sent", null, AgentStatus.Green, KeyStyle.Highlight)
                    : new KeyLook(result.ShortReason, null, AgentStatus.Red, KeyStyle.Highlight));
                HerdrClient.Notify(
                    result.Ok ? $"{label}: {tab.ShortName}" : $"{label} not sent: {tab.ShortName}",
                    result.Ok ? "Sent from the keypad" : $"Not sent: {result.ShortReason}");
                HerdrState.Refresh();
            });
        }

        // Answers the agent's question with the choice on the key. It reads the question again first and
        // sends nothing when the choices changed, so a key never answers a question you did not see.
        private void Answer(String param, Issue tab, Int32 choice)
        {
            var shown = tab.Question;
            if (choice < 0 || choice >= shown.Options.Count || tab.PaneId == null)
            {
                return;
            }

            var label = Question.ShortLabel(shown.Options[choice]);
            Task.Run(() =>
            {
                var now = Questions.Parse(HerdrClient.ReadRecent(tab.PaneId, 40));
                if (now == null || now.Signature != shown.Signature)
                {
                    this.Flash(param, new KeyLook("changed", "look again", AgentStatus.Overlay, KeyStyle.Highlight));
                    HerdrState.Refresh();
                    return;
                }

                PluginLog.Info($"Answer {label} on {tab.Branch}");
                var result = HerdrClient.SendKeys(tab.PaneId, now.KeysFor(choice).ToArray());
                this.Flash(param, result.Ok
                    ? new KeyLook("sent", null, AgentStatus.Green, KeyStyle.Highlight)
                    : new KeyLook(result.ShortReason, null, AgentStatus.Red, KeyStyle.Highlight));
                HerdrClient.Notify(
                    result.Ok ? $"{label}: {tab.ShortName}" : $"{label} not sent: {tab.ShortName}",
                    result.Ok ? "Answered from the keypad" : $"Not sent: {result.ShortReason}");
                HerdrState.Refresh();
            });
        }

        // Shows a look on one key for a moment, then the key's own look again.
        private void Flash(String param, KeyLook look)
        {
            lock (this._lock)
            {
                this._flashes[param] = look;
            }

            this.CommandImageChanged(param);
            Task.Delay(FlashTime).ContinueWith(_ =>
            {
                lock (this._lock)
                {
                    this._flashes.Remove(param);
                }

                this.CommandImageChanged(param);
            });
        }

        private void OnNavigationRequested()
        {
            Boolean active;
            lock (this._lock)
            {
                active = this._active;
                if (active)
                {
                    this.ApplyNavigationRequest();
                }
            }

            if (active)
            {
                this.Update(HerdrState.Current, followScreen: false);
                this.ButtonActionNamesChanged();
            }
        }

        // Takes a pending request from the status key; true when there was one. Call under the lock.
        private Boolean ApplyNavigationRequest()
        {
            var request = HerdrState.TakeNavigationRequest();
            if (request == null)
            {
                return false;
            }

            this._projectId = request.Value.WorkspaceId;
            this._tabId = request.Value.TabId;
            this._page = 0;
            return true;
        }

        private void OnChanged(Snapshot snapshot) => this.Update(snapshot, followScreen: false);

        // Takes a new snapshot of herdr, follows the screen where that applies, and tells the keypad
        // what changed: the set of keys, or only some of their images.
        private void Update(Snapshot snapshot, Boolean followScreen)
        {
            Boolean namesChanged;
            List<String> imagesChanged;
            lock (this._lock)
            {
                this._snapshot = snapshot;

                // On an issue page, the keypad follows the tab herdr shows. It reacts to a change on the
                // screen only, so a key press is not undone while herdr still shows the previous tab.
                var focus = snapshot.FocusedTabId;
                var screenMoved = focus != this._lastSeenFocus;
                this._lastSeenFocus = focus;
                if ((screenMoved || followScreen) && this.Level() == 2 && focus != null && focus != this._tabId)
                {
                    var (project, tab) = snapshot.FindTab(focus);
                    if (tab != null)
                    {
                        this._projectId = project.WorkspaceId;
                        this._tabId = tab.TabId;
                    }
                }

                // A closed tab drops you back to its project; a closed workspace back to the projects.
                if (this._projectId != null && this.SelectedProject() == null)
                {
                    this._projectId = null;
                    this._tabId = null;
                }
                else if (this._tabId != null && this.SelectedTab() == null)
                {
                    this._tabId = null;
                }

                var oldParams = this._shownParams;
                var oldLooks = this._shownLooks;
                this.Remember();
                namesChanged = !oldParams.SequenceEqual(this._shownParams);
                imagesChanged = this._shownParams.Where(p => !oldLooks.TryGetValue(p, out var look) || !look.Equals(this._shownLooks[p])).ToList();
            }

            if (namesChanged)
            {
                this.ButtonActionNamesChanged();
            }
            else
            {
                foreach (var param in imagesChanged)
                {
                    this.CommandImageChanged(param);
                }
            }
        }

        private void Navigate(String projectId, String tabId, Boolean keepPage = false)
        {
            lock (this._lock)
            {
                this._projectId = projectId;
                this._tabId = tabId;
                if (!keepPage)
                {
                    this._page = 0;
                }

                this.Remember();
            }

            this.ButtonActionNamesChanged();
        }

        // Records which keys are on the keypad and how they look, to diff against on the next change.
        private void Remember()
        {
            this._shownParams = this.CurrentParams();
            this._shownLooks = this._shownParams.ToDictionary(p => p, p => this.Look(p));
        }

        private Int32 Level() => this._tabId != null ? 2 : this._projectId != null ? 1 : 0;

        private Project SelectedProject() => this._snapshot.FindProject(this._projectId);

        private Issue SelectedTab() => this.SelectedProject()?.Tabs.FirstOrDefault(t => t.TabId == this._tabId);

        private List<String> CurrentParams()
        {
            if (this._snapshot.Offline)
            {
                return new List<String> { OfflineParam };
            }

            switch (this.Level())
            {
                case 2:
                    var names = new List<String> { HeaderParam };
                    var tab = this.SelectedTab();
                    if (tab?.Status == AgentStatus.Waiting)
                    {
                        // A waiting agent's page is its question: one key per choice, as its screen shows them.
                        var choices = tab.Question?.Options.Count ?? 0;
                        names.AddRange(Enumerable.Range(0, Math.Min(choices, Keys - 3)).Select(i => AnswerPrefix + i));
                        names.Add(OnScreenParam);
                    }
                    else
                    {
                        names.AddRange(Enumerable.Range(0, KeypadConfig.Current.Actions.Count).Select(i => ActionPrefix + i));
                    }

                    names.Add(BackToProjectParam);
                    return names;
                case 1:
                    return this.Paged(this.SelectedProject().Tabs.Select(t => TabPrefix + t.TabId).ToList()).Append(BackToProjectsParam).ToList();
                default:
                    return this.Paged(this._snapshot.Projects.Select(p => ProjectPrefix + p.WorkspaceId).ToList());
            }
        }

        // The items on the current page, followed by "meer" when they do not all fit beside the way back.
        private List<String> Paged(List<String> items)
        {
            if (items.Count <= Keys - 1)
            {
                this._page = 0;
                return items;
            }

            this._page %= PageCount(items.Count);
            return items.Skip(this._page * (Keys - 2)).Take(Keys - 2).Append(MoreParam).ToList();
        }

        private static Int32 PageCount(Int32 items) => items <= Keys - 1 ? 1 : (items + Keys - 3) / (Keys - 2);

        private KeyLook Look(String param)
        {
            lock (this._lock)
            {
                if (param != null && this._flashes.TryGetValue(param, out var flash))
                {
                    return flash;
                }

                var focus = this._snapshot.FocusedTabId;
                switch (param)
                {
                    case null:
                        return new KeyLook("", null, AgentStatus.Overlay);
                    case OfflineParam:
                        return new KeyLook("herdr offline", "press to retry", AgentStatus.Red);
                    case BackToProjectsParam:
                        return new KeyLook("terug", "projecten", AgentStatus.Overlay);
                    case BackToProjectParam:
                        return new KeyLook("terug", this.SelectedProject()?.Name, AgentStatus.Overlay);
                    case MoreParam:
                        var count = this.Level() == 1 ? this.SelectedProject()?.Tabs.Count ?? 0 : this._snapshot.Projects.Count;
                        return new KeyLook("meer", $"{this._page + 1}/{PageCount(count)}", AgentStatus.Overlay);
                    case HeaderParam:
                        var selected = this.SelectedTab();
                        if (selected == null)
                        {
                            return new KeyLook("", null, AgentStatus.Overlay);
                        }

                        if (selected.Status == AgentStatus.Waiting)
                        {
                            // What the agent asks about, such as the command it wants to run.
                            var about = selected.Question?.Detail ?? selected.Question?.Title ?? "asks you";
                            return new KeyLook(selected.ShortName, about, AgentStatus.Red);
                        }

                        return new KeyLook(selected.ShortName, selected.Pr == null ? selected.Status : $"{selected.Status}\nPR {selected.Pr.Number}", AgentStatus.Color(selected.Status));
                    case OnScreenParam:
                        var hasChoices = this.SelectedTab()?.Question != null;
                        return new KeyLook("answer on\nscreen", hasChoices ? null : "no choices\nfound", AgentStatus.Subtext);
                }

                if (param.StartsWith(ProjectPrefix))
                {
                    var project = this._snapshot.FindProject(param[ProjectPrefix.Length..]);
                    return project == null
                        ? new KeyLook("", null, AgentStatus.Overlay)
                        : new KeyLook(project.Name, Summary(project), AgentStatus.Color(project.Status),
                            Framed: focus != null && project.Tabs.Any(t => t.TabId == focus));
                }

                if (param.StartsWith(TabPrefix))
                {
                    var tab = this.SelectedProject()?.Tabs.FirstOrDefault(t => t.TabId == param[TabPrefix.Length..]);
                    return tab == null
                        ? new KeyLook("", null, AgentStatus.Overlay)
                        : new KeyLook(tab.ShortName, tab.Status, AgentStatus.Color(tab.Status), Framed: tab.TabId == focus);
                }

                if (param.StartsWith(AnswerPrefix) && Int32.TryParse(param[AnswerPrefix.Length..], out var choiceIndex))
                {
                    var asking = this.SelectedTab();
                    var question = asking?.Question;
                    if (question != null && choiceIndex >= 0 && choiceIndex < question.Options.Count)
                    {
                        var option = question.Options[choiceIndex];
                        var label = Question.ShortLabel(option);
                        var title = option.Number != null ? $"{option.Number} {label}" : label;

                        // Yes in green, No in red, anything else neutral, so the answer reads at a glance.
                        var accent = label.StartsWith("No", StringComparison.OrdinalIgnoreCase) ? AgentStatus.Red
                            : label.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) || label == "Always" ? AgentStatus.Green
                            : AgentStatus.Subtext;
                        // The issue's name under every choice, so each key says what it answers and to whom.
                        return new KeyLook(title, asking.ShortName, accent);
                    }
                }

                if (param.StartsWith(ActionPrefix) && Int32.TryParse(param[ActionPrefix.Length..], out var index))
                {
                    var actions = KeypadConfig.Current.Actions;
                    var selectedTab = this.SelectedTab();
                    var status = selectedTab?.Status ?? AgentStatus.Unknown;
                    if (index >= 0 && index < actions.Count)
                    {
                        var action = actions[index];
                        var style = StyleFor(action, selectedTab);
                        var accent = ParseHex(action.Color) ?? (style == KeyStyle.Highlight ? AgentStatus.Color(status) : AgentStatus.Overlay);
                        return new KeyLook(action.Label ?? action.Value ?? "?", null, accent, style);
                    }
                }

                return new KeyLook("?", null, AgentStatus.Overlay);
            }
        }

        // Greyed out where it does nothing, lit up where it fits the issue's status.
        private static KeyStyle StyleFor(IssueAction action, Issue tab)
        {
            var status = tab?.Status ?? AgentStatus.Unknown;
            if (action.Type == "open-pr" && tab?.Pr == null)
            {
                return KeyStyle.Dim;
            }

            if (action.OnlyWhen != null && action.OnlyWhen.Count > 0 && !action.OnlyWhen.Contains(status))
            {
                return KeyStyle.Dim;
            }

            return action.When != null && action.When.Contains(status) ? KeyStyle.Highlight : KeyStyle.Normal;
        }

        // "1 waiting, 1 CI failed": what a project's issues need, most urgent first, at most two parts.
        private static String Summary(Project project)
        {
            var parts = new[] { AgentStatus.Waiting, AgentStatus.Go, AgentStatus.CiFailed, AgentStatus.Changes, AgentStatus.Done, AgentStatus.Ready, AgentStatus.Working }
                .Select(s => (Status: s, Count: project.Count(s)))
                .Where(x => x.Count > 0)
                .Take(2)
                .Select(x => $"{x.Count} {x.Status}")
                .ToList();
            return parts.Count > 0 ? String.Join(", ", parts) : AgentStatus.Idle;
        }

        private static BitmapColor? ParseHex(String hex)
        {
            hex = hex?.Trim().TrimStart('#');
            if (hex == null || hex.Length != 6 || !UInt32.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                return null;
            }

            return AgentStatus.Hex(rgb);
        }
    }
}
