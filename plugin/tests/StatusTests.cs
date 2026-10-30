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
    public void A_navigation_request_is_used_once_and_expires()
    {
        var now = new DateTime(2026, 10, 30, 20, 0, 0, DateTimeKind.Utc);
        HerdrState.RequestNavigation(HerdrState.AllIssues, null, now);
        Assert.Equal(HerdrState.AllIssues, HerdrState.TakeNavigationRequest(now.AddSeconds(1))?.WorkspaceId);
        Assert.Null(HerdrState.TakeNavigationRequest(now.AddSeconds(1)));

        HerdrState.RequestNavigation(HerdrState.AllIssues, null, now);
        Assert.Null(HerdrState.TakeNavigationRequest(now.AddSeconds(30)));
    }

    [Fact]
    public void Pull_requests_do_not_start_reading_from_a_track_after_stop()
    {
        PullRequests.Stop();
        PullRequests.Track(new[] { ("/tmp/helm-test-worktree", "feat/test") });
        Assert.False(PullRequests.IsRunning);
    }

    [Fact]
    public void A_key_name_carries_the_tab_it_was_drawn_for()
    {
        var name = FolderKeys.Keyed("act:", 3, "wW:tQ");
        Assert.True(FolderKeys.TryParseKeyed(name, "act:", out var index, out var tabId));
        Assert.Equal((3, "wW:tQ"), (index, tabId));
        Assert.False(FolderKeys.TryParseKeyed("act:3", "act:", out _, out _));
        Assert.False(FolderKeys.TryParseKeyed(name, "ans:", out _, out _));
    }

    [Fact]
    public void Empty_keys_keep_back_on_the_last_key()
    {
        var page = FolderKeys.Fill(new[] { "header", "act:0" }, 8, "empty:").Append("back").ToList();
        Assert.Equal(9, page.Count);
        Assert.Equal("back", page[8]);
        Assert.Equal(new[] { "empty:0", "empty:5" }, new[] { page[2], page[7] });
        Assert.Equal(8, FolderKeys.Fill(Enumerable.Range(0, 8).Select(i => "k" + i).ToList(), 8, "empty:").Count);
    }

    [Fact]
    public void The_overview_keeps_its_order_drops_closed_tabs_and_adds_new_ones_last()
    {
        Assert.Equal(new[] { "b", "a" }, FolderKeys.KeepOrder(null, new[] { "b", "a" }));
        Assert.Equal(new[] { "a", "c", "d" }, FolderKeys.KeepOrder(new[] { "a", "b", "c" }, new[] { "d", "c", "a" }));
    }

    [Fact]
    public void Main_tabs_are_not_counted_as_issues_and_come_last_unless_they_need_you()
    {
        Issue Tab(String id, Int32 number, String status, Boolean main = false) =>
            new(id, number, "w", "b", id, status, "p" + id, "/tmp", null, false, main);
        var snapshot = new Snapshot(new[]
        {
            new Project("w1", 1, "a", true, new[] { Tab("m1", 1, "working", main: true), Tab("t2", 2, "idle") }),
            new Project("w2", 2, "b", false, new[] { Tab("m2", 1, "waiting", main: true), Tab("t3", 2, "ready") }),
        }, false, "m1");
        Assert.Equal(new[] { "m2", "t3", "t2", "m1" }, snapshot.AllTabs().Select(x => x.Tab.TabId));
        Assert.Equal(2, snapshot.IssueCount());
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
    }
}
