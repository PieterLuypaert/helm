namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    // A key for your normal keypad page: how many issues need you across every herdr project, and how
    // many are ready to merge. Pressing it takes herdr and the keypad to the most urgent issue; pressing
    // it again within ten seconds walks on to the next one.
    public class StatusCommand : PluginDynamicCommand
    {
        private static readonly TimeSpan CycleWindow = TimeSpan.FromSeconds(10);

        private readonly Object _lock = new();
        private String _cycleTabId;
        private DateTime _cycleUntil = DateTime.MinValue;

        public StatusCommand()
            : base(displayName: "Herdr status", description: "How many herdr issues need you; press it for the most urgent, again for the next", groupName: "Herdr")
        {
            HerdrState.Changed += _ => this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.Look(HerdrState.Current).Title.Replace('\n', ' ');

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(this.Look(HerdrState.Current), imageSize);

        protected override void RunCommand(String actionParameter)
        {
            var list = HerdrState.Current.NeedsYou();
            if (list.Count == 0)
            {
                lock (this._lock)
                {
                    this._cycleUntil = DateTime.MinValue;
                }

                HerdrState.RequestNavigation(null, null);
                this.OpenFolder();
                return;
            }

            (Project Project, Issue Tab) target;
            lock (this._lock)
            {
                var ids = list.Select(x => x.Tab.TabId).ToList();
                var index = StatusCycle.Next(ids, this._cycleTabId, DateTime.UtcNow < this._cycleUntil);
                target = list[index];
                this._cycleTabId = target.Tab.TabId;
                this._cycleUntil = DateTime.UtcNow + CycleWindow;
            }

            HerdrState.RequestNavigation(target.Project.WorkspaceId, target.Tab.TabId);
            Task.Run(() => Screen.ShowTab(target.Tab.TabId));
            this.OpenFolder();
            this.ActionImageChanged();
            Task.Delay(CycleWindow + TimeSpan.FromMilliseconds(200)).ContinueWith(_ => this.ActionImageChanged());
        }

        // Opens the Herdr folder on the keypad. Logi's generic action API is not documented for
        // this, so a failure is only logged: the folder still opens on the right issue by hand.
        private void OpenFolder()
        {
            var name = IssuesFolder.ActionName;
            if (name == null)
            {
                return;
            }

            try
            {
                this.Plugin.ExecuteGenericAction(name, null, 0);
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"Could not open the folder {name}");
            }
        }

        private KeyLook Look(Snapshot snapshot)
        {
            if (snapshot.Offline)
            {
                return new KeyLook("herdr offline", null, AgentStatus.Red);
            }

            var list = snapshot.NeedsYou();
            var ready = snapshot.Count(AgentStatus.Ready);

            // While you walk through the list, the key says where you are.
            lock (this._lock)
            {
                if (DateTime.UtcNow < this._cycleUntil)
                {
                    var position = list.FindIndex(x => x.Tab.TabId == this._cycleTabId);
                    if (position >= 0)
                    {
                        var tab = list[position].Tab;
                        return new KeyLook($"{position + 1} of {list.Count}", tab.ShortName, AgentStatus.Color(tab.Status), KeyStyle.Highlight);
                    }
                }
            }

            if (list.Count > 0)
            {
                var urgent = list[0].Tab;
                return new KeyLook($"{list.Count} need you", ready > 0 ? $"{ready} ready" : urgent.ShortName, AgentStatus.Color(urgent.Status), KeyStyle.Highlight);
            }

            if (ready > 0)
            {
                return new KeyLook($"{ready} ready", "to merge", AgentStatus.Teal);
            }

            var working = snapshot.Count(AgentStatus.Working);
            return new KeyLook("all clear", working > 0 ? $"{working} working" : "idle", working > 0 ? AgentStatus.Yellow : AgentStatus.Overlay);
        }
    }

    // Which issue the status key goes to: the first one, or, within the window after a press, the one
    // after the issue it went to last. An issue that no longer needs you starts the walk over.
    public static class StatusCycle
    {
        public static Int32 Next(System.Collections.Generic.IReadOnlyList<String> ids, String lastId, Boolean withinWindow)
        {
            if (ids.Count == 0 || !withinWindow || lastId == null)
            {
                return 0;
            }

            var position = -1;
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == lastId)
                {
                    position = i;
                    break;
                }
            }

            return position < 0 ? 0 : (position + 1) % ids.Count;
        }
    }
}
