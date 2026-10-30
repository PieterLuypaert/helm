using Loupedeck.HerdrPlugin;
using Xunit;

public class KeypadTests
{
    [Fact]
    public void Branch_names_lose_their_prefix_and_shared_leading_words()
    {
        var names = IssueNames.Shorten(new[] { "feat/shop-slice-3-sign-up", "feat/shop-slice-4-search", "fix/login-timeout" });
        Assert.Equal(new[] { "3 sign up", "4 search", "login timeout" }, names);
        Assert.Equal(new[] { "only one" }, IssueNames.Shorten(new[] { "feat/only-one" }));
    }

    [Fact]
    public void Labels_wrap_to_short_lines_and_are_cut_when_too_long()
    {
        Assert.Equal("sign up", KeyArt.Wrap("sign up"));
        Assert.Equal("each member\nin the\nsession", KeyArt.Wrap("each member in the session"));
        Assert.EndsWith("..", KeyArt.Wrap("one two three four five six seven eight nine ten"));
        Assert.Equal("go?\nPR 12", KeyArt.Wrap("go?\nPR 12"));
    }
}
