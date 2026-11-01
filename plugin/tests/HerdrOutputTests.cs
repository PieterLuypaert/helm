using Loupedeck.HerdrPlugin;
using Xunit;

// The plugin reading herdr's real output, captured with fixtures/herdr/capture.py and made generic.
public class HerdrOutputTests
{
    private static String Fixture(String name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "herdr", name));

    private static List<Project> Projects() => HerdrClient.ParseProjects(Fixture("workspaces.json"), Fixture("tabs.json"), Fixture("agents.json"));

    [Fact]
    public void The_fixtures_come_from_the_tested_herdr_version()
    {
        Assert.Equal(HerdrClient.TestedVersion, Fixture("VERSION").Trim());
    }

    [Fact]
    public void Every_workspace_is_a_project_in_herdr_s_order()
    {
        var projects = Projects();
        Assert.NotNull(projects);
        Assert.Equal(new[] { "project-a", "project-b", "project-c", "project-d" }, projects.Select(p => p.Name));
        Assert.Equal(new[] { 1, 2, 3, 4 }, projects.Select(p => p.Number));
        Assert.True(projects[0].Focused);
    }

    [Fact]
    public void A_project_lists_its_main_tab_first_then_its_issues_by_number()
    {
        var tabs = Projects()[0].Tabs;
        Assert.Equal(8, tabs.Count);
        Assert.True(tabs[0].IsMain);
        Assert.Equal("main", tabs[0].ShortName);
        Assert.All(tabs.Skip(1), t => Assert.False(t.IsMain));
        Assert.Equal(new[] { 23, 24, 25, 26, 28, 29, 32 }, tabs.Skip(1).Select(t => t.Number));
    }

    [Fact]
    public void An_issue_tab_reads_its_branch_without_the_port_and_a_short_name()
    {
        var issue = Projects()[0].Tabs[1];
        Assert.Equal("feat/alpha-bravo-charlie-3-delta-and-echo-foxtrot", issue.Branch);
        Assert.Equal(
            new[] { "3 delta echo", "4 golf hotel", "5 kilo lima", "6 november alpha", "7 papa india", "8 quebec india", "9 romeo sierra" },
            Projects()[0].Tabs.Skip(1).Select(t => t.ShortName));
    }

    [Fact]
    public void An_issue_tab_carries_its_agent_pane_folder_status_and_state()
    {
        var issue = Projects()[0].Tabs.Single(t => t.TabId == "wW:tR");
        Assert.Equal("wW:p1R", issue.PaneId);
        Assert.Contains("/worktrees/", issue.Cwd);
        Assert.Equal("idle", issue.Status);
        Assert.Equal(28, issue.StateSeq);
        Assert.Equal("wW", issue.WorkspaceId);
    }

    [Fact]
    public void The_focused_tab_is_the_one_on_screen()
    {
        var focused = Projects().SelectMany(p => p.Tabs).Where(t => t.Focused).Select(t => t.TabId);
        Assert.Equal(new[] { "wW:t0" }, focused);
    }

    [Fact]
    public void A_project_with_only_a_main_tab_and_one_without_an_agent()
    {
        var projects = Projects();
        Assert.Equal(new[] { "main" }, projects[1].Tabs.Select(t => t.ShortName));
        Assert.True(projects[1].Tabs[0].IsMain);
        Assert.Empty(projects[3].Tabs);
    }

    [Fact]
    public void Output_that_does_not_parse_reads_as_unreachable()
    {
        Assert.Null(HerdrClient.ParseProjects("not json", Fixture("tabs.json"), Fixture("agents.json")));
    }
}
