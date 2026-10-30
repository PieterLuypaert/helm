namespace Loupedeck.HerdrPlugin
{
    using System;

    // The computer's side of a key press: herdr on the right tab, in front.
    public static class Screen
    {
        private const String ItermBundleId = "com.googlecode.iterm2";

        // Focuses the tab in herdr, then shows it: the terminal comes to the front, or, with no herdr
        // window open, a new iTerm window attaches to herdr, which opens on the tab just focused.
        public static void ShowTab(String tabId)
        {
            HerdrClient.FocusTab(tabId);
            var config = KeypadConfig.Current;
            if (config.OpenHerdrWhenClosed && config.TerminalBundleId == ItermBundleId && !HerdrClient.HasAttachedClient())
            {
                HerdrClient.OpenHerdrInIterm(ItermBundleId);
            }
            else
            {
                HerdrClient.ActivateApp(config.TerminalBundleId);
            }
        }
    }
}
