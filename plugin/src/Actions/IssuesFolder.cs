namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    // The keypad's herdr menu, three levels deep: every project (herdr workspace), with the overview of
    // all issues first; a project's main tab and issue tabs, or every issue at once; and the actions for
    // the tab you picked. Each back key goes one level up, so the projects are never more than two presses
    // away. The keypad and the screen show the same issue: a key press moves herdr, and on an issue page
    // the keypad follows the tab herdr shows.
    public class IssuesFolder : PluginDynamicFolder
    {
        private const String ProjectPrefix = "proj:";
        private const String TabPrefix = "tab:";
        private const String ActionPrefix = "act:";
        private const String HeaderParam = "header";
        private const String AllParam = "all";
        private const String BackToProjectParam = "back:project";
        private const String BackToProjectsParam = "back:projects";
        private const String OfflineParam = "offline";
        private const String MoreParam = "more";
        private const String AnswerPrefix = "ans:";
        private const String OnScreenParam = "on-screen";

        // Nine keys: with more items than fit, seven items, "more" and the way back share a page.
        private const Int32 Keys = 9;

        private static readonly TimeSpan FlashTime = TimeSpan.FromMilliseconds(1500);

        private readonly Object _lock = new();
        private readonly Dictionary<String, (KeyLook Look, Int32 Id)> _flashes = new();
        private Int32 _flashCount;
        private Snapshot _snapshot = Snapshot.Empty;
        private String _projectId;
        private String _tabId;
        private Int32 _page;

        // True while you browse the overview of all issues rather than one project.
        private Boolean _all;

        // The overview's order, fixed while you page through it and visit its issues: sorted afresh only
        // when you open it, so a status change does not move keys between pages under your fingers.
        private List<String> _overviewOrder;

        // The page of the list you opened the issue from, for back.
        private Int32 _returnPage;
        private String _lastSeenFocus;
        private Boolean _active;
        private List<String> _shownParams = new();
        private Dictionary<String, KeyLook> _shownLooks = new();

        public IssuesFolder()
        {
            this.DisplayName = "Herdr projects";
            this.Description = "Your herdr projects, their issues, and actions for the one you pick";
            this.GroupName = "Herdr";

            // The folder places its own back keys. Logi reads this property, which defaults to adding a back key
            // of its own on every page, next to ours. The SDK marks it obsolete in favour of GetNavigationArea,
            // but on the MX Keypad only this property removes Logi's key.
#pragma warning disable CS0618
            this.Navigation = PluginDynamicFolderNavigation.None;
#pragma warning restore CS0618
        }

        // The action name that opens this folder, for the All issues key.
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
            lock (this._lock)
            {
                this._active = true;

                // The Projects key always opens on your projects. Only the All issues key opens the folder
                // elsewhere: it asks for the overview just before it opens the folder.
                if (!this.ApplyNavigationRequest())
                {
                    this._projectId = null;
                    this._tabId = null;
                    this._page = 0;
                    this._all = false;
                }
            }

            HerdrState.Changed += this.OnChanged;
            this.Update(HerdrState.Current, followScreen: false);
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
        // A tile like an app icon, without a label: the stack says what it opens.
        public override String GetButtonDisplayName(PluginImageSize imageSize) => "";

        public override BitmapImage GetButtonImage(PluginImageSize imageSize) =>
            KeyArt.Draw(new KeyLook("Projects", Icons.Stack, AgentStatus.Accent, KeyStyle.Tile), imageSize);

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            lock (this._lock)
            {
                // Every page below the projects has our own back key. The projects page gets Logi's own back
                // key, which Logi adds there by itself and which closes the menu; the folder cannot close
                // itself (Close() does nothing on the MX Keypad). While herdr is offline the menu still
                // needs a way out, so the folder asks for Logi's key there.
                var names = this.CurrentParams().Select(p => this.CreateCommandName(p)).ToList();
                if (this._snapshot.Offline && this.Level() != 0)
                {
                    names.Add(PluginDynamicFolder.NavigateUpActionName);
                }

                return names;
            }
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.Look(actionParameter).Title ?? "";

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
                lock (this._lock)
                {
                    this._all = false;
                }

                this.Navigate(id, null);
                Task.Run(() => HerdrClient.FocusWorkspace(id));
            }
            else if (actionParameter == AllParam)
            {
                lock (this._lock)
                {
                    this._all = true;
                    this._overviewOrder = null;
                }

                this.Navigate(null, null);
            }
            else if (actionParameter.StartsWith(TabPrefix))
            {
                var id = actionParameter[TabPrefix.Length..];

                // In the overview a tab can belong to any project: open it in its own.
                Project owner;
                lock (this._lock)
                {
                    owner = this._snapshot.FindTab(id).Project;
                    this._returnPage = this._page;
                }

                this.Navigate(owner?.WorkspaceId ?? project?.WorkspaceId, id);
                Task.Run(() => Screen.ShowTab(id));
            }
            else if (actionParameter == BackToProjectParam)
            {
                // Back where you came from: the overview of all issues, or the issue's project, on the page
                // you opened the issue from.
                Boolean all;
                lock (this._lock)
                {
                    all = this._all;
                    this._page = this._returnPage;
                }

                this.Navigate(all ? null : project?.WorkspaceId, null, keepPage: true);
            }
            else if (actionParameter == BackToProjectsParam)
            {
                lock (this._lock)
                {
                    this._all = false;
                }

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
            else if (FolderKeys.TryParseKeyed(actionParameter, AnswerPrefix, out var choice, out var askingId))
            {
                Issue asking;
                lock (this._lock)
                {
                    asking = this._snapshot.FindTab(askingId).Tab;
                }

                if (asking?.Question != null)
                {
                    this.Answer(actionParameter, asking, choice);
                }
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
            else if (FolderKeys.TryParseKeyed(actionParameter, ActionPrefix, out var index, out var targetId))
            {
                Issue target;
                lock (this._lock)
                {
                    target = this._snapshot.FindTab(targetId).Tab;
                }

                var actions = KeypadConfig.Current.Actions;
                if (target != null && index >= 0 && index < actions.Count)
                {
                    this.RunAction(actionParameter, actions[index], target);
                }
            }
        }

        // Sends the action to the issue's agent and shows how it went: on the key, and in herdr.
        private void RunAction(String param, IssueAction action, Issue tab)
        {
            var label = (action.Label ?? action.Value ?? "action").Replace('\n', ' ');
            if (StyleFor(action, tab) == KeyStyle.Dim)
            {
                this.Flash(param, new KeyLook("not now", Icons.Ban, AgentStatus.Overlay, KeyStyle.Highlight));
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
                    ? new KeyLook("sent", Icons.Check, AgentStatus.Green, KeyStyle.Highlight)
                    : new KeyLook(result.ShortReason, Icons.X, AgentStatus.Red, KeyStyle.Highlight));
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
                    this.Flash(param, new KeyLook("changed, look again", Icons.Refresh, AgentStatus.Overlay, KeyStyle.Highlight));
                    HerdrState.ForgetQuestion(tab.PaneId);
                    HerdrState.Refresh();
                    return;
                }

                PluginLog.Info($"Answer {label} on {tab.Branch}");
                var result = HerdrClient.SendKeys(tab.PaneId, now.KeysFor(choice).ToArray());
                this.Flash(param, result.Ok
                    ? new KeyLook("sent", Icons.Check, AgentStatus.Green, KeyStyle.Highlight)
                    : new KeyLook(result.ShortReason, Icons.X, AgentStatus.Red, KeyStyle.Highlight));
                HerdrClient.Notify(
                    result.Ok ? $"{label}: {tab.ShortName}" : $"{label} not sent: {tab.ShortName}",
                    result.Ok ? "Answered from the keypad" : $"Not sent: {result.ShortReason}");
                HerdrState.Refresh();
            });
        }

        // Shows a look on one key for a moment, then the key's own look again.
        private void Flash(String param, KeyLook look)
        {
            Int32 id;
            lock (this._lock)
            {
                id = ++this._flashCount;
                this._flashes[param] = (look, id);
            }

            this.CommandImageChanged(param);
            Task.Delay(FlashTime).ContinueWith(_ =>
            {
                lock (this._lock)
                {
                    // A newer flash on the same key keeps its own full time.
                    if (this._flashes.TryGetValue(param, out var shown) && shown.Id == id)
                    {
                        this._flashes.Remove(param);
                    }
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

        // Takes a pending request from the All issues key; true when there was one. Call under the lock.
        private Boolean ApplyNavigationRequest()
        {
            var request = HerdrState.TakeNavigationRequest();
            if (request == null)
            {
                return false;
            }

            this._all = request.Value.WorkspaceId == HerdrState.AllIssues;
            this._overviewOrder = null;
            this._projectId = this._all ? null : request.Value.WorkspaceId;
            this._tabId = this._all ? null : request.Value.TabId;
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
                // While herdr cannot be read, the keys say so but keep your place for when it is back.
                var focus = snapshot.FocusedTabId;
                var screenMoved = !snapshot.Offline && focus != this._lastSeenFocus;
                if (!snapshot.Offline)
                {
                    this._lastSeenFocus = focus;
                }

                if ((screenMoved || followScreen) && this.Level() == 2 && focus != null && focus != this._tabId)
                {
                    var (project, tab) = snapshot.FindTab(focus);
                    if (tab != null)
                    {
                        if (!this._all && project.WorkspaceId != this._projectId)
                        {
                            this._returnPage = 0;
                        }

                        this._projectId = project.WorkspaceId;
                        this._tabId = tab.TabId;
                    }
                }

                // A closed tab drops you back to its project; a closed workspace back to the projects.
                if (!snapshot.Offline)
                {
                    if (this._projectId != null && this.SelectedProject() == null)
                    {
                        this._projectId = null;
                        this._tabId = null;
                    }
                    else if (this._tabId != null && this.SelectedTab() == null)
                    {
                        this._tabId = null;
                        if (this._all)
                        {
                            this._projectId = null;
                        }
                    }
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

        private Int32 Level() => this._tabId != null ? 2 : this._all || this._projectId != null ? 1 : 0;

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
                        // Seven keys between the issue and the way back: actions past the seventh do not fit.
                        names.AddRange(Enumerable.Range(0, Math.Min(KeypadConfig.Current.Actions.Count, Keys - 2)).Select(i => FolderKeys.Keyed(ActionPrefix, i, tab.TabId)));
                    }

                    names.Add(BackToProjectParam);
                    return names;
                case 1 when this._all:
                    return this.Paged(this.OverviewOrder().Select(id => TabPrefix + id).ToList()).Append(BackToProjectsParam).ToList();
                case 1:
                    var tabs = this.SelectedProject()?.Tabs ?? Array.Empty<Issue>();
                    return this.Paged(tabs.Select(t => TabPrefix + t.TabId).ToList()).Append(BackToProjectsParam).ToList();
                default:
                    return this.Paged(this._snapshot.Projects.Select(p => ProjectPrefix + p.WorkspaceId).Prepend(AllParam).ToList());
            }
        }

        // Every tab in the overview's fixed order. Call under the lock.
        private List<String> OverviewOrder()
        {
            this._overviewOrder = FolderKeys.KeepOrder(this._overviewOrder, this._snapshot.AllTabs().Select(x => x.Tab.TabId).ToList());
            return this._overviewOrder;
        }

        // The items on the current page, followed by "more" when they do not all fit beside the way back.
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

        // How each key looks: its icon, and in Title the label Logi writes under it.
        private KeyLook Look(String param)
        {
            lock (this._lock)
            {
                if (param != null && this._flashes.TryGetValue(param, out var flash))
                {
                    return flash.Look;
                }

                var focus = this._snapshot.FocusedTabId;
                var nav = AgentStatus.Subtext;
                switch (param)
                {
                    case null:
                        return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                    case OfflineParam:
                        return new KeyLook("herdr offline", Icons.Offline, AgentStatus.Red);
                    case AllParam:
                        return StatusCommand.Look(this._snapshot);
                    case BackToProjectsParam:
                    case BackToProjectParam:
                        return new KeyLook("back", Icons.Back, nav);
                    case MoreParam:
                        var count = this._all ? this.OverviewOrder().Count : this.Level() == 1 ? this.SelectedProject()?.Tabs.Count ?? 0 : this._snapshot.Projects.Count + 1;
                        return new KeyLook($"more {this._page + 1}/{PageCount(count)}", Icons.Dots, nav);
                    case OnScreenParam:
                        return new KeyLook("answer on screen", Icons.Screen, nav);
                    case HeaderParam:
                        var selected = this.SelectedTab();
                        if (selected == null)
                        {
                            return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                        }

                        // The issue, and what it waits on: the command it asks about, or its pull request.
                        var about = selected.Status == AgentStatus.Waiting
                            ? selected.Question?.Detail ?? selected.Question?.Title
                            : selected.Pr != null ? $"PR {selected.Pr.Number}" : null;
                        return new KeyLook(about == null ? selected.ShortName : $"{selected.ShortName} · {about}", AgentStatus.Icon(selected.Status), AgentStatus.Color(selected.Status));
                }

                if (param.StartsWith(ProjectPrefix))
                {
                    var project = this._snapshot.FindProject(param[ProjectPrefix.Length..]);
                    if (project == null)
                    {
                        return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                    }

                    var needsYou = project.Tabs.Count(t => AgentStatus.NeedsYou(t.Status));
                    return new KeyLook(project.Name, Icons.Folder, AgentStatus.Color(project.Status),
                        Framed: focus != null && project.Tabs.Any(t => t.TabId == focus),
                        Badge: needsYou > 0 ? needsYou.ToString() : null);
                }

                if (param.StartsWith(TabPrefix))
                {
                    var (owner, tab) = this._snapshot.FindTab(param[TabPrefix.Length..]);
                    if (tab == null)
                    {
                        return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                    }

                    // The main tab keeps its house while it rests; any other state shows like an issue's.
                    var icon = tab.IsMain && tab.Status == AgentStatus.Idle ? Icons.Home : AgentStatus.Icon(tab.Status);

                    // In the overview of all issues, a main tab or a tab beside other projects' says whose it is.
                    var several = this._snapshot.Projects.Count(p => p.Tabs.Any(t => !t.IsMain)) > 1;
                    var label = this._all && (tab.IsMain || several) ? $"{tab.ShortName} · {owner.Name}" : tab.ShortName;
                    return new KeyLook(label, icon, AgentStatus.Color(tab.Status), Framed: tab.TabId == focus);
                }

                if (FolderKeys.TryParseKeyed(param, AnswerPrefix, out var choiceIndex, out var askingId))
                {
                    var asking = this._snapshot.FindTab(askingId).Tab;
                    var question = asking?.Question;
                    if (question != null && choiceIndex >= 0 && choiceIndex < question.Options.Count)
                    {
                        var option = question.Options[choiceIndex];
                        var label = Question.ShortLabel(option);
                        var choice = option.Number != null ? $"{option.Number} {label}" : label;

                        // Yes in green, No in red, anything else neutral; the label names the issue too, so
                        // each key says what it answers and to whom.
                        var (answerIcon, answerColor) =
                            label.StartsWith("No", StringComparison.OrdinalIgnoreCase) ? (Icons.X, AgentStatus.Red)
                            : label == "Always" ? (Icons.Checks, AgentStatus.Green)
                            : label.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) ? (Icons.Check, AgentStatus.Green)
                            : label == "Auto mode" ? (Icons.Robot, AgentStatus.Subtext)
                            : (Icons.Dot, AgentStatus.Subtext);
                        return new KeyLook($"{choice} · {asking.ShortName}", answerIcon, answerColor);
                    }
                }

                if (FolderKeys.TryParseKeyed(param, ActionPrefix, out var index, out var targetId))
                {
                    var actions = KeypadConfig.Current.Actions;
                    var selectedTab = this._snapshot.FindTab(targetId).Tab;
                    var status = selectedTab?.Status ?? AgentStatus.Unknown;
                    if (index >= 0 && index < actions.Count)
                    {
                        var action = actions[index];
                        var style = StyleFor(action, selectedTab);
                        var accent = style == KeyStyle.Highlight ? ParseHex(action.Color) ?? AgentStatus.Color(status) : AgentStatus.Text;
                        return new KeyLook((action.Label ?? action.Value ?? "?").Replace('\n', ' '), IconFor(action), accent, style);
                    }
                }

                return new KeyLook("?", Icons.Help, AgentStatus.Overlay);
            }
        }

        // An action's icon: the one its config names, else one that fits its type.
        private static String IconFor(IssueAction action) =>
            Icons.Named(action.Icon) ?? action.Type?.ToLowerInvariant() switch
            {
                "open-pr" => Icons.PullRequest,
                "shell" => Icons.Terminal,
                "keys" => Icons.Stop,
                _ => Icons.Play,
            };

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
