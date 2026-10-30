namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    // The keypad's herdr menu, three levels deep: every project (herdr workspace) with a summary of its
    // agents; a project's main tab and ticket tabs; and the actions for the tab you picked.
    public class TicketsFolder : PluginDynamicFolder
    {
        private const String ProjectPrefix = "proj:";
        private const String TabPrefix = "tab:";
        private const String ActionPrefix = "act:";
        private const String HeaderParam = "header";
        private const String BackToProjectParam = "back:project";
        private const String BackToProjectsParam = "back:projects";
        private const String OfflineParam = "offline";
        private const String MoreParam = "more";

        // Nine keys: with more items than fit, seven items, "meer" and the way back share a page.
        private const Int32 Keys = 9;

        private static readonly BitmapColor Ground = new(16, 16, 16);
        private static readonly BitmapColor Text = new(235, 235, 235);
        private static readonly BitmapColor Dim = new(110, 110, 110);

        private readonly Object _lock = new();
        private List<Project> _projects = new();
        private Boolean _offline;
        private String _projectId;
        private String _tabId;
        private Int32 _page;
        private List<String> _shownParams = new();
        private Dictionary<String, (String, String, BitmapColor)> _shownLooks = new();
        private Timer _timer;
        private Int32 _refreshing;

        public TicketsFolder()
        {
            this.DisplayName = "Herdr";
            this.Description = "Your herdr projects, their tickets, and actions for the one you pick";
            this.GroupName = "Herdr";
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.None;

        public override Boolean Activate()
        {
            this.Refresh();
            this._timer = new Timer(_ => this.Refresh(), null, 2000, 2000);
            return true;
        }

        public override Boolean Deactivate()
        {
            this._timer?.Dispose();
            this._timer = null;
            return true;
        }

        public override String GetButtonDisplayName(PluginImageSize imageSize) => "Herdr";

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
            this.Look(actionParameter).Title.Replace('\n', ' ');

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var (title, subtitle, accent) = this.Look(actionParameter);
            using var builder = new BitmapBuilder(imageSize);
            builder.Clear(Ground);
            var w = builder.Width;
            var h = builder.Height;
            var stripe = Math.Max(4, h / 12);
            builder.FillRectangle(0, 0, w, stripe, accent);
            var pad = Math.Max(3, w / 20);
            var titleHeight = subtitle == null ? h - stripe - pad : (h - stripe) * 3 / 5;
            builder.DrawText(Wrap(title), pad, stripe, w - 2 * pad, titleHeight, Text);
            if (subtitle != null)
            {
                builder.DrawText(Wrap(subtitle, 13, 2), pad, stripe + titleHeight, w - 2 * pad, h - stripe - titleHeight - pad, accent);
            }

            return builder.ToImage();
        }

        public override void RunCommand(String actionParameter)
        {
            if (actionParameter == null)
            {
                return;
            }

            Project project;
            Ticket tab;
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
                Task.Run(() => FocusTab(id));
            }
            else if (actionParameter == BackToProjectParam)
            {
                this.Navigate(project?.WorkspaceId, null);
            }
            else if (actionParameter == BackToProjectsParam)
            {
                this.Navigate(null, null);
            }
            else if (actionParameter == HeaderParam && tab != null)
            {
                Task.Run(() => FocusTab(tab.TabId));
            }
            else if (actionParameter == MoreParam)
            {
                lock (this._lock)
                {
                    this._page++;
                }

                this.Navigate(project?.WorkspaceId, tab?.TabId, keepPage: true);
            }
            else if (actionParameter == OfflineParam)
            {
                this.Refresh();
            }
            else if (actionParameter.StartsWith(ActionPrefix) && tab != null
                     && Int32.TryParse(actionParameter[ActionPrefix.Length..], out var index))
            {
                var actions = KeypadConfig.Current.Actions;
                if (index >= 0 && index < actions.Count)
                {
                    var action = actions[index];
                    Task.Run(() => RunAction(action, tab));
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

        private static void FocusTab(String tabId)
        {
            HerdrClient.FocusTab(tabId);
            HerdrClient.ActivateApp(KeypadConfig.Current.TerminalBundleId);
        }

        private static void RunAction(TicketAction action, Ticket ticket)
        {
            PluginLog.Info($"{action.Label?.Replace('\n', ' ')} on {ticket.Branch}");
            switch (action.Type?.ToLowerInvariant())
            {
                case "prompt" when ticket.PaneId != null:
                    HerdrClient.Prompt(ticket.PaneId, action.Value ?? "");
                    break;
                case "keys" when ticket.PaneId != null:
                    HerdrClient.SendKeys(ticket.PaneId, (action.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    break;
                case "shell":
                    HerdrClient.Shell(action.Value ?? "", ticket.Cwd);
                    break;
                default:
                    PluginLog.Warning($"Skipped action {action.Label}: type {action.Type} or no agent pane");
                    break;
            }
        }

        // Reads herdr again and tells the keypad what changed: the set of keys, or only some of their images.
        private void Refresh()
        {
            if (Interlocked.Exchange(ref this._refreshing, 1) == 1)
            {
                return;
            }

            try
            {
                var fresh = HerdrClient.ReadProjects();
                Boolean namesChanged;
                List<String> imagesChanged;
                lock (this._lock)
                {
                    this._offline = fresh == null;
                    this._projects = fresh ?? new List<Project>();

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
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Refresh failed");
            }
            finally
            {
                Interlocked.Exchange(ref this._refreshing, 0);
            }
        }

        // Records which keys are on the keypad and how they look, to diff against on the next refresh.
        private void Remember()
        {
            this._shownParams = this.CurrentParams();
            this._shownLooks = this._shownParams.ToDictionary(p => p, p => this.Look(p));
        }

        private Int32 Level() => this._tabId != null ? 2 : this._projectId != null ? 1 : 0;

        private Project SelectedProject() => this._projects.FirstOrDefault(p => p.WorkspaceId == this._projectId);

        private Ticket SelectedTab() => this.SelectedProject()?.Tabs.FirstOrDefault(t => t.TabId == this._tabId);

        private List<String> CurrentParams()
        {
            if (this._offline)
            {
                return new List<String> { OfflineParam };
            }

            switch (this.Level())
            {
                case 2:
                    var names = new List<String> { HeaderParam };
                    names.AddRange(Enumerable.Range(0, KeypadConfig.Current.Actions.Count).Select(i => ActionPrefix + i));
                    names.Add(BackToProjectParam);
                    return names;
                case 1:
                    return this.Paged(this.SelectedProject().Tabs.Select(t => TabPrefix + t.TabId).ToList()).Append(BackToProjectsParam).ToList();
                default:
                    return this.Paged(this._projects.Select(p => ProjectPrefix + p.WorkspaceId).ToList());
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

            var pages = this.PageCount(items.Count);
            this._page %= pages;
            return items.Skip(this._page * (Keys - 2)).Take(Keys - 2).Append(MoreParam).ToList();
        }

        private Int32 PageCount(Int32 items) => items <= Keys - 1 ? 1 : (items + Keys - 3) / (Keys - 2);

        private (String Title, String Subtitle, BitmapColor Accent) Look(String param)
        {
            lock (this._lock)
            {
                if (param == null)
                {
                    return ("", null, Dim);
                }

                if (param == OfflineParam)
                {
                    return ("herdr\nniet bereikbaar", null, StatusColor("blocked"));
                }

                if (param == MoreParam)
                {
                    var count = this.Level() == 1 ? this.SelectedProject()?.Tabs.Count ?? 0 : this._projects.Count;
                    return ("meer", $"{this._page + 1}/{this.PageCount(count)}", Dim);
                }

                if (param == BackToProjectsParam)
                {
                    return ("terug", "projecten", Dim);
                }

                if (param == BackToProjectParam)
                {
                    return ("terug", this.SelectedProject()?.Name, Dim);
                }

                if (param == HeaderParam)
                {
                    var tab = this.SelectedTab();
                    return tab == null ? ("", null, Dim) : (tab.ShortName, StatusLabel(tab.Status), StatusColor(tab.Status));
                }

                if (param.StartsWith(ProjectPrefix))
                {
                    var project = this._projects.FirstOrDefault(p => p.WorkspaceId == param[ProjectPrefix.Length..]);
                    return project == null ? ("", null, Dim) : (project.Name, Summary(project), StatusColor(project.Status));
                }

                if (param.StartsWith(TabPrefix))
                {
                    var tab = this.SelectedProject()?.Tabs.FirstOrDefault(t => t.TabId == param[TabPrefix.Length..]);
                    return tab == null ? ("", null, Dim) : (tab.ShortName, StatusLabel(tab.Status), StatusColor(tab.Status));
                }

                if (param.StartsWith(ActionPrefix) && Int32.TryParse(param[ActionPrefix.Length..], out var index))
                {
                    var actions = KeypadConfig.Current.Actions;
                    if (index >= 0 && index < actions.Count)
                    {
                        var action = actions[index];
                        return (action.Label ?? action.Value ?? "?", null, ParseHex(action.Color) ?? Dim);
                    }
                }

                return ("?", null, Dim);
            }
        }

        // "1 wacht, 2 klaar": what a project's agents need, most urgent first.
        private static String Summary(Project project)
        {
            var parts = new List<String>();
            if (project.Count("blocked") > 0)
            {
                parts.Add($"{project.Count("blocked")} wacht");
            }

            if (project.Count("done") > 0)
            {
                parts.Add($"{project.Count("done")} klaar");
            }

            if (project.Count("working") > 0)
            {
                parts.Add($"{project.Count("working")} bezig");
            }

            return parts.Count > 0 ? String.Join(", ", parts) : "rustig";
        }

        private static String StatusLabel(String status) => status switch
        {
            "blocked" => "wacht op jou",
            "done" => "klaar",
            "working" => "bezig",
            "idle" => "idle",
            _ => "?",
        };

        // The quarterdeck's colours: red needs you, orange is done, yellow is working.
        private static BitmapColor StatusColor(String status) => status switch
        {
            "blocked" => new BitmapColor(229, 72, 77),
            "done" => new BitmapColor(255, 138, 61),
            "working" => new BitmapColor(245, 196, 81),
            _ => Dim,
        };

        private static BitmapColor? ParseHex(String hex)
        {
            hex = hex?.Trim().TrimStart('#');
            if (hex == null || hex.Length != 6 || !UInt32.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                return null;
            }

            return new BitmapColor((Int32)(rgb >> 16) & 0xFF, (Int32)(rgb >> 8) & 0xFF, (Int32)rgb & 0xFF);
        }

        // Breaks a label into at most a few short lines so it fits a key.
        private static String Wrap(String text, Int32 width = 11, Int32 maxLines = 3)
        {
            if (String.IsNullOrEmpty(text) || text.Contains('\n'))
            {
                return text ?? "";
            }

            var lines = new List<String>();
            var line = "";
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = line.Length == 0 ? word : line + " " + word;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }

            if (lines.Count > maxLines)
            {
                lines = lines.Take(maxLines).ToList();
                lines[^1] = lines[^1].Length >= width ? lines[^1][..(width - 1)] + "…" : lines[^1] + "…";
            }

            return String.Join('\n', lines);
        }
    }
}
