namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    // The keypad's herdr menu: it draws the keys MenuState lists and passes your presses to it. MenuState
    // holds where the menu is and every rule for moving through it; this class talks to the Logi Plugin
    // Service, herdr and the agents.
    public class IssuesFolder : PluginDynamicFolder
    {
        private const String ProjectPrefix = MenuState.ProjectPrefix;
        private const String TabPrefix = MenuState.TabPrefix;
        private const String ActionPrefix = MenuState.ActionPrefix;
        private const String HeaderParam = MenuState.HeaderParam;
        private const String EmptyPrefix = MenuState.EmptyPrefix;
        private const String BackToProjectParam = MenuState.BackToProjectParam;
        private const String BackToProjectsParam = MenuState.BackToProjectsParam;
        private const String OfflineParam = MenuState.OfflineParam;
        private const String MoreParam = MenuState.MoreParam;
        private const String AnswerPrefix = MenuState.AnswerPrefix;
        private const String OnScreenParam = MenuState.OnScreenParam;

        private static readonly TimeSpan FlashTime = TimeSpan.FromMilliseconds(1500);

        private readonly Object _lock = new();
        private readonly Dictionary<String, (KeyLook Look, Int32 Id)> _flashes = new();
        private Int32 _flashCount;
        private readonly MenuState _menu = new();
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
                    this._menu.OpenOnProjects();
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
        // A short word under the tile, like the other two herdr keys on your page.
        public override String GetButtonDisplayName(PluginImageSize imageSize) => "projects";

        public override BitmapImage GetButtonImage(PluginImageSize imageSize) =>
            KeyArt.Draw(new KeyLook("Projects", Icons.Stack, AgentStatus.Accent, KeyStyle.Tile), imageSize);

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            lock (this._lock)
            {
                // Logi may ask before the menu opens: read herdr's latest state rather than an old one.
                if (!this._active)
                {
                    this._menu.Use(HerdrState.Current);
                }

                // Logi adds its own back key on the first key of every page; it leaves the menu.
                return this._menu.KeysOnPage(KeypadConfig.Current.Actions).Select(p => this.CreateCommandName(p)).ToList();
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

            Issue tab;
            lock (this._lock)
            {
                tab = this._menu.SelectedTab;
            }

            if (actionParameter.StartsWith(ProjectPrefix))
            {
                var id = actionParameter[ProjectPrefix.Length..];
                this.Navigate(m => m.PressProject(id));
                Task.Run(() => HerdrClient.FocusWorkspace(id));
            }
            else if (actionParameter.StartsWith(TabPrefix))
            {
                var id = actionParameter[TabPrefix.Length..];
                this.Navigate(m => m.PressTab(id));
                Task.Run(() => Screen.ShowTab(id));
            }
            else if (actionParameter == BackToProjectParam)
            {
                this.Navigate(m => m.UpToList());
            }
            else if (actionParameter == BackToProjectsParam)
            {
                this.Navigate(m => m.UpToProjects());
            }
            else if (actionParameter == MoreParam)
            {
                this.Navigate(m => m.More());
            }
            else if (FolderKeys.TryParseKeyed(actionParameter, AnswerPrefix, out var choice, out var askingId))
            {
                Issue asking;
                lock (this._lock)
                {
                    asking = this._menu.Snapshot.FindTab(askingId).Tab;
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
                    target = this._menu.Snapshot.FindTab(targetId).Tab;
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
            if (!MenuState.Fits(action, tab))
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

                // The next question may come with the same agent state: read it afresh, never this one again.
                HerdrState.ForgetQuestion(tab.PaneId);
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

            this._menu.OpenOn(request.Value.WorkspaceId, request.Value.TabId);
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
                this._menu.See(snapshot, followScreen);

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

        // Moves the menu by one of MenuState's steps and shows the new page.
        private void Navigate(Action<MenuState> step)
        {
            lock (this._lock)
            {
                step(this._menu);
                this.Remember();
            }

            this.ButtonActionNamesChanged();
        }

        // Records which keys are on the keypad and how they look, to diff against on the next change.
        private void Remember()
        {
            this._shownParams = this._menu.KeysOnPage(KeypadConfig.Current.Actions);
            this._shownLooks = this._shownParams.ToDictionary(p => p, p => this.Look(p));
        }

        // How each key looks: its icon, and in Title the label Logi writes under it.
        private KeyLook Look(String param)
        {
            lock (this._lock)
            {
                if (param != null && this._flashes.TryGetValue(param, out var flash))
                {
                    return flash.Look;
                }

                var nav = AgentStatus.Subtext;
                switch (param)
                {
                    case null:
                        return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                    case OfflineParam:
                        return new KeyLook("herdr offline", Icons.Offline, AgentStatus.Red);
                    case var empty when empty.StartsWith(EmptyPrefix):
                        // Solid black, not see-through: Logi keeps the previous key's picture under a clear
                        // image, so an empty key showed the issue that sat there on the page before. A
                        // zero-width space: an empty title would make Logi write the folder's name there.
                        return new KeyLook("\u200B", Icons.None, AgentStatus.Overlay, KeyStyle.Tile);
                    case BackToProjectsParam:
                        // One level up, named after where it goes, so it does not read as a second back arrow.
                        return new KeyLook("projects", Icons.Stack, nav);
                    case BackToProjectParam:
                        return this._menu.All ? new KeyLook("all issues", Icons.List, nav) : new KeyLook(this._menu.SelectedProject?.Name ?? "project", Icons.Folder, nav);
                    case MoreParam:
                        return new KeyLook(this._menu.MoreLabel(), Icons.Dots, nav);
                    case OnScreenParam:
                        return new KeyLook("answer on screen", Icons.Screen, nav);
                    case HeaderParam:
                        var selected = this._menu.SelectedTab;
                        if (selected == null)
                        {
                            return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                        }

                        // The issue and its exact status, which its icon groups with others: "7 checkout · CI
                        // failed". While it waits on you, what it asks about instead, such as the command.
                        var about = selected.Status == AgentStatus.Waiting
                            ? selected.Question?.Detail ?? selected.Question?.Title ?? selected.Status
                            : selected.Status;
                        return new KeyLook($"{selected.ShortName} · {about}", AgentStatus.Icon(selected.Status), AgentStatus.Color(selected.Status));
                }

                if (param.StartsWith(ProjectPrefix))
                {
                    var project = this._menu.Snapshot.FindProject(param[ProjectPrefix.Length..]);
                    if (project == null)
                    {
                        return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                    }

                    // Every project the same white folder, with how many issues it holds; the badge alone, in
                    // the colour of its most urgent one, says what needs you.
                    var needsYou = project.Tabs.Where(t => AgentStatus.NeedsYou(t.Status)).ToList();
                    var issues = project.Tabs.Count(t => !t.IsMain);
                    var title = issues switch { 0 => project.Name, 1 => $"{project.Name} · 1 issue", _ => $"{project.Name} · {issues} issues" };
                    return new KeyLook(title, Icons.Folder, AgentStatus.Text,
                        Badge: needsYou.Count > 0 ? needsYou.Count.ToString() : null,
                        BadgeColor: needsYou.Count > 0 ? AgentStatus.Color(AgentStatus.MostUrgent(needsYou.Select(t => t.Status))) : null);
                }

                if (param.StartsWith(TabPrefix))
                {
                    var (owner, tab) = this._menu.Snapshot.FindTab(param[TabPrefix.Length..]);
                    if (tab == null)
                    {
                        return new KeyLook("", Icons.Dot, AgentStatus.Overlay);
                    }

                    // The main tab keeps its house while it rests; any other state shows like an issue's.
                    var icon = tab.IsMain && tab.Status == AgentStatus.Idle ? Icons.Home : AgentStatus.Icon(tab.Status);

                    // In the overview of all issues, a main tab goes by its project's name, and an issue beside
                    // other projects' issues says whose it is.
                    var several = this._menu.Snapshot.Projects.Count(p => p.Tabs.Any(t => !t.IsMain)) > 1;
                    var label = !this._menu.All ? tab.ShortName : tab.IsMain ? owner.Name : several ? $"{tab.ShortName} · {owner.Name}" : tab.ShortName;
                    return new KeyLook(label, icon, AgentStatus.Color(tab.Status));
                }

                if (FolderKeys.TryParseKeyed(param, AnswerPrefix, out var choiceIndex, out var askingId))
                {
                    var asking = this._menu.Snapshot.FindTab(askingId).Tab;
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
                    var selectedTab = this._menu.Snapshot.FindTab(targetId).Tab;
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

        // Lit up where it is the step that fits the issue's status.
        private static KeyStyle StyleFor(IssueAction action, Issue tab) =>
            action.When != null && action.When.Contains(tab?.Status ?? AgentStatus.Unknown) ? KeyStyle.Highlight : KeyStyle.Normal;

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
