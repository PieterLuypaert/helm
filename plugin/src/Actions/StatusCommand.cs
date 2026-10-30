namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Threading.Tasks;

    // A key for your normal keypad page: what needs you across every herdr project, in the
    // quarterdeck's words. Pressing it takes herdr and the keypad to the most urgent issue.
    public class StatusCommand : PluginDynamicCommand
    {
        public StatusCommand()
            : base(displayName: "Herdr status", description: "What needs you across all herdr projects; press it for the most urgent issue", groupName: "Herdr")
        {
            HerdrState.Changed += _ => this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            Look(HerdrState.Current).Title.Replace('\n', ' ');

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(Look(HerdrState.Current), imageSize);

        protected override void RunCommand(String actionParameter)
        {
            var (project, tab) = HerdrState.Current.MostUrgent();
            if (tab != null)
            {
                HerdrState.RequestNavigation(project.WorkspaceId, tab.TabId);
                Task.Run(() => Screen.ShowTab(tab.TabId));
            }
            else
            {
                HerdrState.RequestNavigation(null, null);
            }

            this.OpenFolder();
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

        private static KeyLook Look(Snapshot snapshot)
        {
            if (snapshot.Offline)
            {
                return new KeyLook("herdr offline", null, AgentStatus.Red);
            }

            var waiting = snapshot.Count(AgentStatus.Waiting);
            var go = snapshot.Count(AgentStatus.Go);
            var done = snapshot.Count(AgentStatus.Done);
            var working = snapshot.Count(AgentStatus.Working);
            var (_, urgent) = snapshot.MostUrgent();
            if (urgent == null)
            {
                return new KeyLook("all clear", working > 0 ? $"{working} working" : "idle", working > 0 ? AgentStatus.Yellow : AgentStatus.Overlay);
            }

            var parts = new[] { (waiting, AgentStatus.Waiting), (go, AgentStatus.Go), (done, AgentStatus.Done) };
            var title = "";
            var subtitle = "";
            foreach (var (count, word) in parts)
            {
                if (count == 0)
                {
                    continue;
                }

                if (title.Length == 0)
                {
                    title = $"{count} {word}";
                }
                else
                {
                    subtitle = subtitle.Length == 0 ? $"{count} {word}" : $"{subtitle}, {count} {word}";
                }
            }

            return new KeyLook(title, subtitle.Length > 0 ? subtitle : urgent.ShortName, AgentStatus.Color(urgent.Status), KeyStyle.Highlight);
        }
    }
}
