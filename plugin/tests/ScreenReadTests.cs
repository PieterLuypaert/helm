using System.Text.RegularExpressions;
using Loupedeck.HerdrPlugin;
using Xunit;

// Reading agents' screens at most once per agent state, without taking a failed read for an answer.
public class ScreenReadTests
{
    private static readonly Regex Plan = new(@"^\s*(Single PR:|PR 1:)", RegexOptions.Multiline);

    private static String Fixture(String name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name));

    // A reader that answers from a queue of screens; null stands for a failed read.
    private sealed class FakeScreen
    {
        private readonly Queue<String> _screens;

        public FakeScreen(params String[] screens) => this._screens = new Queue<String>(screens);

        public Int32 Reads { get; private set; }

        public String Read(String pane, Int32 lines)
        {
            this.Reads++;
            return this._screens.Count > 1 ? this._screens.Dequeue() : this._screens.Peek();
        }
    }

    [Fact]
    public void A_plan_is_read_once_per_agent_state()
    {
        var screen = new FakeScreen("Single PR: add search — 3 files — ~80");
        var reads = new ScreenReads(screen.Read);
        Assert.True(reads.HasPlan("p1", 5, Plan));
        Assert.True(reads.HasPlan("p1", 5, Plan));
        Assert.Equal(1, screen.Reads);
    }

    [Fact]
    public void A_failed_read_does_not_hide_a_plan()
    {
        var screen = new FakeScreen(null, "Single PR: add search — 3 files — ~80");
        var reads = new ScreenReads(screen.Read);
        Assert.False(reads.HasPlan("p1", 5, Plan));
        Assert.True(reads.HasPlan("p1", 5, Plan));
    }

    [Fact]
    public void A_question_is_read_once_per_agent_state_and_again_after_an_answer()
    {
        var screen = new FakeScreen(Fixture("permission-numbered.txt"));
        var reads = new ScreenReads(screen.Read);
        Assert.NotNull(reads.QuestionOf("p1", 7));
        Assert.NotNull(reads.QuestionOf("p1", 7));
        Assert.Equal(1, screen.Reads);

        reads.ForgetQuestion("p1");
        Assert.NotNull(reads.QuestionOf("p1", 7));
        Assert.Equal(2, screen.Reads);
    }

    [Fact]
    public void A_failed_read_is_tried_again_on_the_next_poll()
    {
        var screen = new FakeScreen(null, Fixture("permission-numbered.txt"));
        var reads = new ScreenReads(screen.Read);
        Assert.Null(reads.QuestionOf("p1", 7));
        Assert.NotNull(reads.QuestionOf("p1", 7));
    }

    [Fact]
    public void An_unreadable_question_is_read_once_more_and_then_left_alone()
    {
        var now = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var screen = new FakeScreen("What should the new file be called?\n> ");
        var reads = new ScreenReads(screen.Read, () => now);
        Assert.Null(reads.QuestionOf("p1", 7));
        now = now.AddSeconds(2);
        Assert.Null(reads.QuestionOf("p1", 7));
        Assert.Equal(1, screen.Reads);

        now = now.AddSeconds(2);
        Assert.Null(reads.QuestionOf("p1", 7));
        Assert.Equal(2, screen.Reads);

        now = now.AddMinutes(5);
        Assert.Null(reads.QuestionOf("p1", 7));
        Assert.Equal(2, screen.Reads);

        Assert.Null(reads.QuestionOf("p1", 8));
        Assert.Equal(3, screen.Reads);
    }

    [Fact]
    public void A_question_still_drawing_shows_on_the_retry()
    {
        var now = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var screen = new FakeScreen("drawing…", Fixture("permission-numbered.txt"));
        var reads = new ScreenReads(screen.Read, () => now);
        Assert.Null(reads.QuestionOf("p1", 7));
        now = now.Add(ScreenReads.Retry);
        Assert.NotNull(reads.QuestionOf("p1", 7));
    }
}
