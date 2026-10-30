namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    // The keypad's ticket menu. Level 1 shows every ticket tab of the herdr workspace you are in;
    // pressing one focuses that tab and turns the folder into level 2, the actions for that ticket.
    public class TicketsFolder : PluginDynamicFolder
    {
        private const String TicketPrefix = "tab:";
        private const String ActionPrefix = "act:";
        private const String HeaderParam = "header";
        private const String BackParam = "back";
        private const String OfflineParam = "offline";

        private static readonly BitmapColor Ground = new(16, 16, 16);
        private static readonly BitmapColor Text = new(235, 235, 235);
        private static readonly BitmapColor Dim = new(150, 150, 150);

        private readonly Object _lock = new();
        private List<Ticket> _tickets = new();
        private Boolean _offline;
        private String _selectedTabId;
        private Timer _timer;
        private Int32 _refreshing;

        public TicketsFolder()
        {
            this.DisplayName = "Herdr tickets";
            this.Description = "Your herdr ticket tabs, and actions for the one you pick";
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

        public override String GetButtonDisplayName(PluginImageSize imageSize) => "Herdr\ntickets";

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            lock (this._lock)
            {
                var selected = this.Selected();
                if (selected != null)
                {
                    var names = new List<String> { this.CreateCommandName(HeaderParam) };
                    var actions = KeypadConfig.Current.Actions;
                    for (var i = 0; i < actions.Count; i++)
                    {
                        names.Add(this.CreateCommandName(ActionPrefix + i));
                    }

                    names.Add(this.CreateCommandName(BackParam));
                    return names;
                }

                if (this._offline)
                {
                    return new[] { this.CreateCommandName(OfflineParam), PluginDynamicFolder.NavigateUpActionName };
                }

                return this._tickets.Select(t => this.CreateCommandName(TicketPrefix + t.TabId))
                                    .Append(PluginDynamicFolder.NavigateUpActionName)
                                    .ToList();
            }
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.Describe(actionParameter).Title.Replace('\n', ' ');

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var (title, subtitle, accent) = this.Describe(actionParameter);
            using var builder = new BitmapBuilder(imageSize);
            builder.Clear(Ground);
            var w = builder.Width;
            var h = builder.Height;
            var stripe = Math.Max(4, h / 12);
            builder.FillRectangle(0, 0, w, stripe, accent);
            var pad = Math.Max(3, w / 20);
            var titleHeight = subtitle == null ? h - stripe - pad : (h - stripe) * 2 / 3;
            builder.DrawText(Wrap(title), pad, stripe, w - 2 * pad, titleHeight, Text);
            if (subtitle != null)
            {
                builder.DrawText(subtitle, pad, stripe + titleHeight, w - 2 * pad, h - stripe - titleHeight - pad, accent);
            }

            return builder.ToImage();
        }

        public override void RunCommand(String actionParameter)
        {
            if (actionParameter == null)
            {
                return;
            }

            Ticket selected;
            lock (this._lock)
            {
                selected = this.Selected();
            }

            if (actionParameter.StartsWith(TicketPrefix))
            {
                var tabId = actionParameter[TicketPrefix.Length..];
                lock (this._lock)
                {
                    this._selectedTabId = tabId;
                }

                this.ButtonActionNamesChanged();
                Task.Run(() => FocusTicket(tabId));
                return;
            }

            if (actionParameter == BackParam)
            {
                lock (this._lock)
                {
                    this._selectedTabId = null;
                }

                this.ButtonActionNamesChanged();
                return;
            }

            if (actionParameter == HeaderParam && selected != null)
            {
                Task.Run(() => FocusTicket(selected.TabId));
                return;
            }

            if (actionParameter == OfflineParam)
            {
                this.Refresh();
                return;
            }

            if (actionParameter.StartsWith(ActionPrefix) && selected != null
                && Int32.TryParse(actionParameter[ActionPrefix.Length..], out var index))
            {
                var actions = KeypadConfig.Current.Actions;
                if (index >= 0 && index < actions.Count)
                {
                    var action = actions[index];
                    Task.Run(() => RunAction(action, selected));
                }
            }
        }

        private static void FocusTicket(String tabId)
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

        // Reads herdr again and tells the keypad what changed: the list of keys, or only some images.
        private void Refresh()
        {
            if (Interlocked.Exchange(ref this._refreshing, 1) == 1)
            {
                return;
            }

            try
            {
                var fresh = HerdrClient.ReadTickets();
                Boolean namesChanged;
                List<String> imagesChanged;
                lock (this._lock)
                {
                    var offline = fresh == null;
                    fresh ??= new List<Ticket>();
                    var oldSelected = this.Selected();
                    namesChanged = offline != this._offline
                        || !fresh.Select(t => t.TabId).SequenceEqual(this._tickets.Select(t => t.TabId));
                    var old = this._tickets.ToDictionary(t => t.TabId);
                    imagesChanged = fresh.Where(t => !old.TryGetValue(t.TabId, out var prev) || prev != t)
                                         .Select(t => TicketPrefix + t.TabId).ToList();
                    this._offline = offline;
                    this._tickets = fresh;

                    var newSelected = this.Selected();
                    if (this._selectedTabId != null && newSelected == null)
                    {
                        // The ticket tab was closed: fall back to the overview.
                        this._selectedTabId = null;
                        namesChanged = true;
                    }
                    else if (newSelected != null && newSelected != oldSelected)
                    {
                        imagesChanged.Add(HeaderParam);
                    }
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

        private Ticket Selected() => this._selectedTabId == null ? null : this._tickets.FirstOrDefault(t => t.TabId == this._selectedTabId);

        private (String Title, String Subtitle, BitmapColor Accent) Describe(String actionParameter)
        {
            lock (this._lock)
            {
                if (actionParameter == null)
                {
                    return ("", null, Dim);
                }

                if (actionParameter == OfflineParam)
                {
                    return ("herdr\nniet bereikbaar", null, StatusColor("blocked"));
                }

                if (actionParameter == BackParam)
                {
                    return ("terug", null, Dim);
                }

                if (actionParameter == HeaderParam)
                {
                    var selected = this.Selected();
                    return selected == null ? ("", null, Dim) : (selected.ShortName, StatusLabel(selected.Status), StatusColor(selected.Status));
                }

                if (actionParameter.StartsWith(TicketPrefix))
                {
                    var ticket = this._tickets.FirstOrDefault(t => t.TabId == actionParameter[TicketPrefix.Length..]);
                    return ticket == null ? ("", null, Dim) : (ticket.ShortName, StatusLabel(ticket.Status), StatusColor(ticket.Status));
                }

                if (actionParameter.StartsWith(ActionPrefix) && Int32.TryParse(actionParameter[ActionPrefix.Length..], out var index))
                {
                    var actions = KeypadConfig.Current.Actions;
                    if (index >= 0 && index < actions.Count)
                    {
                        var action = actions[index];
                        var accent = ParseHex(action.Color) ?? Dim;
                        return (action.Label ?? action.Value ?? "?", null, accent);
                    }
                }

                return ("?", null, Dim);
            }
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
            _ => new BitmapColor(110, 110, 110),
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

        // Breaks a label into at most three short lines so it fits a key.
        private static String Wrap(String text, Int32 width = 11, Int32 maxLines = 3)
        {
            if (text.Contains('\n'))
            {
                return text;
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
