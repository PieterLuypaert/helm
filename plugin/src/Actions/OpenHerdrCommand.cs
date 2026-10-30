namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Threading.Tasks;

    // An app key for herdr itself: it opens herdr on screen, in iTerm, the way your other app keys open
    // their apps. The Projects key opens the herdr menu on the keypad instead.
    public class OpenHerdrCommand : PluginDynamicCommand
    {
        public OpenHerdrCommand()
            : base(displayName: "Open herdr", description: "Opens herdr on screen: brings it to the front in iTerm, or starts it in a new iTerm window", groupName: "Herdr")
        {
        }

        // A tile like an app icon, without a label.
        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => "";

        // A grey terminal rather than herdr's orange, so it does not look like the Projects key.
        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(new KeyLook("herdr", Icons.Terminal, AgentStatus.Subtext, KeyStyle.Tile), imageSize);

        protected override void RunCommand(String actionParameter) => Task.Run(Screen.OpenHerdr);
    }
}
