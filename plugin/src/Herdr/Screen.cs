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
                BringToFront(config.TerminalBundleId);
            }
        }

        // Activates the terminal only when another app is in front: switching apps can make Logi close
        // the keypad's menu, and herdr already shows the tab when the terminal is in front.
        private static void BringToFront(String bundleId)
        {
            if (!String.IsNullOrWhiteSpace(bundleId) && HerdrClient.FrontmostBundleId() != bundleId)
            {
                HerdrClient.ActivateApp(bundleId);
            }
        }

        // Shows herdr itself, like opening an app: the terminal comes to the front when a herdr window is
        // open, and otherwise a new iTerm window starts herdr. Asked for by a key, so it opens herdr even
        // when openHerdrWhenClosed is off.
        public static void OpenHerdr()
        {
            var config = KeypadConfig.Current;
            if (config.TerminalBundleId == ItermBundleId && !HerdrClient.HasAttachedClient())
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
