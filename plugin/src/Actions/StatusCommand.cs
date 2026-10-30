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

        // Logi keeps a label strip under every plugin key; a short word fills it, like a macro's label.
        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            HerdrState.HasRead && HerdrState.Current.Offline ? "offline" : "issues";

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(TileLook(HerdrState.Current), imageSize);

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

        // On your page: the number of issues large on a black tile, over the list, in the same colour.
        public static KeyLook TileLook(Snapshot snapshot)
        {
            // Right after the plugin loads herdr has not been read yet: the list alone, not "offline", because
            // Logi may draw the key once then and not ask again until something changes.
            if (!HerdrState.HasRead)
            {
                return new KeyLook("issues", Icons.List, AgentStatus.Subtext, KeyStyle.Tile);
            }

            var look = Look(snapshot);
            return snapshot.Offline
                ? look with { Style = KeyStyle.Tile }
                : look with { Style = KeyStyle.Tile, Count = snapshot.IssueCount().ToString() };
        }

        // A list in the colour of the most urgent issue, with the count of those that need you in a badge.
        // The Projects page shows the same key, with its label.
        public static KeyLook Look(Snapshot snapshot)
        {
            if (snapshot.Offline)
            {
                return new KeyLook("herdr offline", Icons.Offline, AgentStatus.Red);
            }

            var issues = snapshot.IssueCount();
            var needsYou = snapshot.NeedsYou();
            var title = issues switch { 0 => "no issues", 1 => "1 issue", _ => $"{issues} issues" };
            return needsYou.Count > 0
                ? new KeyLook(title, Icons.List, AgentStatus.Color(needsYou[0].Tab.Status), Badge: needsYou.Count.ToString())
                : new KeyLook(title, Icons.List, AgentStatus.Text);
        }
    }
}
