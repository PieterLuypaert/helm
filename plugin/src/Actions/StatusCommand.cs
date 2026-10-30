namespace Loupedeck.HerdrPlugin
{
    using System;

    // A key for your normal keypad page: how many issues you have across every herdr project, with a
    // count of those that need you. Pressing it opens the menu on an overview of all of them, most
    // urgent first; you pick the one to look at.
    public class StatusCommand : PluginDynamicCommand
    {
        public StatusCommand()
            : base(displayName: "Herdr all issues", description: "All your herdr issues across projects, most urgent first; press it for the overview", groupName: "Herdr")
        {
            HerdrState.Changed += _ => this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            Look(HerdrState.Current).Title;

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(Look(HerdrState.Current), imageSize);

        protected override void RunCommand(String actionParameter)
        {
            HerdrState.RequestNavigation(HerdrState.AllIssues, null);
            this.OpenFolder();
        }

        // Opens the Herdr folder on the keypad. Logi's generic action API is not documented for
        // this, so a failure is only logged: the Projects key still opens the menu by hand.
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

        // A list in the colour of the most urgent issue, with the count of those that need you in a badge.
        private static KeyLook Look(Snapshot snapshot)
        {
            if (snapshot.Offline)
            {
                return new KeyLook("herdr offline", Icons.Offline, AgentStatus.Red);
            }

            var all = snapshot.AllTabs();
            var needsYou = snapshot.NeedsYou();
            var title = all.Count == 1 ? "1 issue" : $"{all.Count} issues";
            return needsYou.Count > 0
                ? new KeyLook(title, Icons.List, AgentStatus.Color(needsYou[0].Tab.Status), Badge: needsYou.Count.ToString())
                : new KeyLook(title, Icons.List, AgentStatus.Text);
        }
    }
}
