using Loupedeck.HerdrPlugin;
using Xunit;

public class StatusTests
{
    private static PrInfo Pr(String state = "OPEN", Boolean draft = false, String review = "", String checks = "passing") =>
        new(1, "https://github.com/o/r/pull/1", state, draft, review, checks);

    [Theory]
    [InlineData("blocked", true, "waiting")]
    [InlineData("working", true, "working")]
    [InlineData("idle", true, "go?")]
    [InlineData("done", true, "go?")]
    [InlineData("done", false, "done")]
    [InlineData("idle", false, "idle")]
    [InlineData("weird", false, "unknown")]
    public void Without_a_pull_request_the_agent_decides(String raw, Boolean hasPlan, String expected) =>
        Assert.Equal(expected, AgentStatus.From(raw, hasPlan, null));

    [Fact]
    public void The_agent_wins_over_its_pull_request()
    {
        Assert.Equal("waiting", AgentStatus.From("blocked", false, Pr(checks: "failing")));
        Assert.Equal("working", AgentStatus.From("working", false, Pr(checks: "failing")));
    }

    [Fact]
    public void A_plan_counts_only_before_there_is_a_pull_request() =>
        Assert.Equal("ready", AgentStatus.From("idle", true, Pr(review: "APPROVED")));

    [Theory]
    [InlineData("failing", "APPROVED", false, "idle", "CI failed")]
    [InlineData("passing", "CHANGES_REQUESTED", false, "idle", "changes")]
    [InlineData("failing", "CHANGES_REQUESTED", false, "idle", "CI failed")]
    [InlineData("passing", "APPROVED", false, "done", "done")]
    [InlineData("passing", "APPROVED", false, "idle", "ready")]
    [InlineData("none", "APPROVED", false, "idle", "ready")]
    [InlineData("pending", "APPROVED", false, "idle", "review")]
    [InlineData("passing", "", false, "idle", "review")]
    [InlineData("passing", "", true, "idle", "draft")]
    public void An_open_pull_request_reads_as_expected(String checks, String review, Boolean draft, String raw, String expected) =>
        Assert.Equal(expected, AgentStatus.From(raw, false, Pr(draft: draft, review: review, checks: checks)));

    [Fact]
    public void A_merged_or_closed_pull_request_is_finished()
    {
        Assert.Equal("merged", AgentStatus.From("idle", false, Pr(state: "MERGED", checks: "failing")));
        Assert.Equal("closed", AgentStatus.From("done", false, Pr(state: "CLOSED")));
    }

    [Fact]
    public void What_needs_you_ranks_waiting_first_and_leaves_ready_out()
    {
        var statuses = new[] { "ready", "done", "changes", "CI failed", "go?", "waiting" };
        Assert.Equal(new[] { "waiting", "go?", "CI failed", "changes", "done", "ready" }, statuses.OrderBy(AgentStatus.Rank));
        Assert.True(AgentStatus.NeedsYou("CI failed"));
        Assert.False(AgentStatus.NeedsYou("ready"));
        Assert.False(AgentStatus.NeedsYou("working"));
    }

    [Fact]
    public void A_snapshot_lists_all_tabs_most_urgent_first_across_projects()
    {
        Issue Tab(String id, Int32 number, String status) =>
            new(id, number, "w", "b", id, status, "p" + id, "/tmp", null, false, false);
        var snapshot = new Snapshot(new[]
        {
            new Project("w1", 1, "a", true, new[] { Tab("t1", 1, "idle"), Tab("t2", 2, "working"), Tab("t3", 3, "waiting") }),
            new Project("w2", 2, "b", false, new[] { Tab("t4", 1, "ready"), Tab("t5", 2, "go?") }),
        }, false, "t1");
        Assert.Equal(new[] { "t3", "t5", "t4", "t2", "t1" }, snapshot.AllTabs().Select(x => x.Tab.TabId));
        Assert.Equal("b", snapshot.AllTabs()[1].Project.Name);
    }

    [Fact]
    public void A_snapshot_lists_what_needs_you_most_urgent_first()
    {
        Issue Tab(String id, Int32 number, String status) =>
            new(id, number, "w", "b", id, status, "p" + id, "/tmp", null, false, false);
        var snapshot = new Snapshot(new[]
        {
            new Project("w1", 1, "a", true, new[] { Tab("t1", 1, "done"), Tab("t2", 2, "working"), Tab("t3", 3, "waiting") }),
            new Project("w2", 2, "b", false, new[] { Tab("t4", 1, "CI failed"), Tab("t5", 2, "ready") }),
        }, false, "t1");
        Assert.Equal(new[] { "t3", "t4", "t1" }, snapshot.NeedsYou().Select(x => x.Tab.TabId));
        Assert.Equal("t3", snapshot.MostUrgent().Tab.TabId);
    }
}
