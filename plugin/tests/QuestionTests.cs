using Loupedeck.HerdrPlugin;
using Xunit;

public class QuestionTests
{
    private static String Fixture(String name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name));

    [Fact]
    public void A_permission_prompt_reads_with_its_numbered_choices()
    {
        var q = Questions.Parse(Fixture("permission-numbered.txt"));
        Assert.NotNull(q);
        Assert.Equal("Bash command", q.Title);
        Assert.Equal("touch helm-probe.txt", q.Detail);
        Assert.True(q.Numbered);
        Assert.Equal(0, q.Selected);
        Assert.Equal(new[] { "1", "2", "3", "4" }, q.Options.Select(o => o.Number));
        Assert.Equal("Yes", q.Options[0].Text);
        Assert.Equal("No", q.Options[3].Text);
        Assert.Equal(new[] { "Yes", "Always", "Auto mode", "No" }, q.Options.Select(Question.ShortLabel));
    }

    [Fact]
    public void A_numbered_choice_is_picked_by_its_own_digit_not_by_its_position()
    {
        var q = Questions.Parse(Fixture("permission-numbered.txt"));
        // "No" is the fourth choice here, not the third: the digit comes from the screen.
        Assert.Equal(new[] { "4" }, q.KeysFor(3));
        Assert.Equal(new[] { "1" }, q.KeysFor(0));
    }

    [Fact]
    public void A_question_without_numbers_is_answered_with_the_arrow_keys()
    {
        var q = Questions.Parse(Fixture("trust-first-selected.txt"));
        Assert.NotNull(q);
        Assert.False(q.Numbered);
        Assert.Equal("Accessing workspace", q.Title);
        Assert.Equal(new[] { "No, exit", "Yes, I trust this folder" }, q.Options.Select(o => o.Text));
        Assert.Equal(0, q.Selected);
        Assert.Equal(new[] { "down", "enter" }, q.KeysFor(1));
        Assert.Equal(new[] { "enter" }, q.KeysFor(0));
        Assert.Equal(new[] { "No, exit", "Yes" }, q.Options.Select(Question.ShortLabel));
    }

    [Fact]
    public void The_arrow_keys_start_from_the_choice_that_is_selected_now()
    {
        var q = Questions.Parse(Fixture("trust-second-selected.txt"));
        Assert.Equal(1, q.Selected);
        Assert.Equal(new[] { "up", "enter" }, q.KeysFor(0));
    }

    [Fact]
    public void A_question_that_changed_has_another_signature()
    {
        var before = Questions.Parse(Fixture("trust-first-selected.txt"));
        var after = Questions.Parse(Fixture("permission-numbered.txt"));
        Assert.NotEqual(before.Signature, after.Signature);
        Assert.Equal(before.Signature, Questions.Parse(Fixture("trust-second-selected.txt")).Signature);
    }

    [Theory]
    [InlineData("")]
    [InlineData("❯ just a prompt line\n\nsome output")]
    [InlineData("Esc to cancel")]
    public void A_screen_without_a_question_has_none(String screen) => Assert.Null(Questions.Parse(screen));
}
