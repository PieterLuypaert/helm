namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Threading.Tasks;

    // An app key for herdr itself: it opens herdr on screen, in iTerm, the way your other app keys open
    // their apps. The Issues key opens the herdr menu on the keypad instead.
    public class OpenHerdrCommand : PluginDynamicCommand
    {
        public OpenHerdrCommand()
            : base(displayName: "Open herdr", description: "Opens herdr on screen: brings it to the front in iTerm, or starts it in a new iTerm window", groupName: "Herdr")
        {
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => "herdr";

        // Drawn like an app key, grey rather than herdr's accent, so it does not look like the Issues key.
        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            KeyArt.Draw(new KeyLook("herdr", "in iTerm", AgentStatus.Subtext), imageSize);

        protected override void RunCommand(String actionParameter) => Task.Run(Screen.OpenHerdr);
    }
}
