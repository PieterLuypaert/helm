namespace Loupedeck.HerdrPlugin
{
    using System;

    // This class contains the plugin-level logic of the Loupedeck plugin.

    public class HerdrPlugin : Plugin
    {
        // Gets a value indicating whether this is an API-only plugin.
        public override Boolean UsesApplicationApiOnly => true;

        // Gets a value indicating whether this is a Universal plugin or an Application plugin.
        public override Boolean HasNoApplication => true;

        // Initializes a new instance of the plugin class.
        public HerdrPlugin()
        {
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);
        }

        // This method is called when the plugin is loaded.
        public override void Load()
        {
            // One reader of herdr for the whole plugin: the folder and the All issues key share it. A change
            // in a pull request makes it read herdr again, so the keys show it at once.
            PullRequests.Changed += HerdrState.Refresh;
            HerdrState.Start();
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
            HerdrState.Stop();
            PullRequests.Stop();
            PullRequests.Changed -= HerdrState.Refresh;
        }
    }
}
