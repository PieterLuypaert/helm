using Loupedeck.HerdrPlugin;
using Xunit;

// The herdr menu's navigation rules, without a keypad.
public class MenuStateTests
{
    private static readonly IssueAction[] Actions =
    {
        new() { Label = "go", Type = "prompt", Value = "go", When = new() { "go?" }, OnlyWhen = new() { "go?", "idle", "done" } },
        new() { Label = "stop", Type = "keys", Value = "esc", OnlyWhen = new() { "working" } },
        new() { Label = "open PR", Type = "open-pr" },
        new() { Label = "editor", Type = "shell", Value = "hopen" },
    };

    private static Issue Tab(String id, Int32 number, String workspace, String status = "idle", Boolean main = false, Question question = null) =>
        new(id, number, workspace, "feat/" + id, id, status, "p" + id, "/tmp/" + id, null, false, main, Question: question);

    // Project w1 holds a main tab and nine issues, so its list has two pages; w2 holds a main tab and one issue.
    private static Snapshot Herdr(String focus = null, Boolean withoutT4 = false, Boolean withoutW2 = false)
    {
        var w1 = new List<Issue> { Tab("m1", 1, "w1", main: true) };
        for (var i = 1; i <= 9; i++)
        {
            if (!(withoutT4 && i == 4))
            {
                w1.Add(Tab("t" + i, i + 1, "w1", i == 3 ? "waiting" : "idle"));
            }
        }

        var projects = new List<Project> { new("w1", 1, "one", true, w1) };
        if (!withoutW2)
        {
            projects.Add(new Project("w2", 2, "two", false, new[] { Tab("m2", 1, "w2", main: true), Tab("u1", 2, "w2", "working") }));
        }

        return new Snapshot(projects, false, focus);
    }

    private static MenuState Menu(Snapshot snapshot)
    {
        var menu = new MenuState();
        menu.See(snapshot);
        menu.OpenOnProjects();
        return menu;
    }

    private static List<String> Keys(MenuState menu) => menu.KeysOnPage(Actions);

    [Fact]
    public void Projects_opens_on_your_projects_with_no_back_key_of_ours()
    {
        var menu = Menu(Herdr());
        Assert.Equal(0, menu.Level);
        Assert.Equal(new[] { "proj:w1", "proj:w2" }, Keys(menu));
    }

    [Fact]
    public void The_issues_tile_opens_the_overview_freshly_sorted()
    {
        var menu = Menu(Herdr());
        menu.OpenOn(HerdrState.AllIssues, null);
        var keys = Keys(menu);
        Assert.True(menu.All);
        Assert.Equal(new[] { "tab:t3", "tab:u1" }, keys.Take(2));
        Assert.Equal(MenuState.MoreParam, keys[6]);
        Assert.Equal(MenuState.BackToProjectsParam, keys[7]);
    }

    [Fact]
    public void Every_page_holds_eight_keys_with_more_and_the_way_up_on_fixed_keys()
    {
        var menu = Menu(Herdr());
        menu.PressProject("w1");
        var first = Keys(menu);
        Assert.Equal(8, first.Count);
        Assert.Equal(new[] { MenuState.MoreParam, MenuState.BackToProjectsParam }, first.Skip(6));

        menu.More();
        var second = Keys(menu);
        Assert.Equal(8, second.Count);
        Assert.Equal(new[] { "tab:t6", "tab:t7", "tab:t8", "tab:t9" }, second.Take(4));
        Assert.All(second.Skip(4).Take(2), k => Assert.StartsWith(MenuState.EmptyPrefix, k));
        Assert.Equal(new[] { MenuState.MoreParam, MenuState.BackToProjectsParam }, second.Skip(6));
    }

    [Fact]
    public void An_issue_page_shows_the_actions_that_fit_named_after_its_tab()
    {
        var menu = Menu(Herdr());
        menu.PressProject("w1");
        menu.PressTab("t1");
        var keys = Keys(menu);
        Assert.Equal(2, menu.Level);
        Assert.Equal(MenuState.HeaderParam, keys[0]);
        Assert.Equal(new[] { "act:0:t1", "act:3:t1" }, keys.Skip(1).Take(2));
        Assert.Equal(8, keys.Count);
        Assert.Equal(MenuState.BackToProjectParam, keys[7]);
    }

    [Fact]
    public void A_question_fits_six_choices_and_more_go_to_the_screen()
    {
        var six = new Question("q", null, Enumerable.Range(1, 6).Select(i => new QuestionOption(i - 1, i.ToString(), "c" + i)).ToList(), 0);
        var seven = new Question("q", null, Enumerable.Range(1, 7).Select(i => new QuestionOption(i - 1, i.ToString(), "c" + i)).ToList(), 0);
        Snapshot With(Question q) => new(new[] { new Project("w1", 1, "one", true, new[] { Tab("t1", 1, "w1", "waiting", question: q) }) }, false, null);

        var menu = Menu(With(six));
        menu.PressProject("w1");
        menu.PressTab("t1");
        Assert.Equal(6, Keys(menu).Count(k => k.StartsWith(MenuState.AnswerPrefix)));
        Assert.DoesNotContain(MenuState.OnScreenParam, Keys(menu));

        menu.See(With(seven));
        Assert.Equal(5, Keys(menu).Count(k => k.StartsWith(MenuState.AnswerPrefix)));
        Assert.Contains(MenuState.OnScreenParam, Keys(menu));
        Assert.Equal(8, Keys(menu).Count);
    }

    [Fact]
    public void Up_from_an_issue_returns_to_the_page_it_was_opened_from()
    {
        var menu = Menu(Herdr());
        menu.PressProject("w1");
        menu.More();
        Keys(menu);
        menu.PressTab("t8");
        menu.UpToList();
        Assert.Equal(1, menu.Level);
        Assert.Equal("w1", menu.ProjectId);
        Assert.Equal(1, menu.Page);

        menu.UpToProjects();
        Assert.Equal(0, menu.Level);
    }

    [Fact]
    public void Up_from_an_issue_opened_in_the_overview_returns_to_the_overview()
    {
        var menu = Menu(Herdr());
        menu.OpenOn(HerdrState.AllIssues, null);
        menu.PressTab("u1");
        Assert.Equal("w2", menu.ProjectId);
        menu.UpToList();
        Assert.True(menu.All);
        Assert.Equal(1, menu.Level);
        Assert.Null(menu.ProjectId);
    }

    [Fact]
    public void An_issue_page_follows_the_screen_also_into_another_project()
    {
        var menu = Menu(Herdr(focus: "t1"));
        menu.PressProject("w1");
        menu.PressTab("t1");
        menu.See(Herdr(focus: "u1"));
        Assert.Equal("u1", menu.TabId);
        Assert.Equal("w2", menu.ProjectId);
        Assert.Equal(0, menu.ReturnPage);
    }

    [Fact]
    public void List_pages_do_not_follow_the_screen()
    {
        var menu = Menu(Herdr(focus: "t1"));
        menu.PressProject("w1");
        menu.See(Herdr(focus: "t5"));
        Assert.Null(menu.TabId);

        menu.OpenOn(HerdrState.AllIssues, null);
        menu.See(Herdr(focus: "t6"));
        Assert.Null(menu.TabId);
    }

    [Fact]
    public void A_stale_focus_right_after_a_press_does_not_undo_it()
    {
        var menu = Menu(Herdr(focus: "t4"));
        menu.PressProject("w1");
        menu.PressTab("t9");
        menu.See(Herdr(focus: "t4"));
        Assert.Equal("t9", menu.TabId);
    }

    [Fact]
    public void A_closed_tab_drops_back_to_its_list_and_a_closed_workspace_to_the_projects()
    {
        var menu = Menu(Herdr());
        menu.PressProject("w1");
        menu.PressTab("t4");
        menu.See(Herdr(withoutT4: true));
        Assert.Equal(1, menu.Level);
        Assert.Equal("w1", menu.ProjectId);

        menu.OpenOn(HerdrState.AllIssues, null);
        menu.PressTab("u1");
        menu.See(Herdr(withoutW2: true));
        Assert.Equal(1, menu.Level);
        Assert.True(menu.All);

        menu.PressProject("w1");
        menu.See(new Snapshot(Array.Empty<Project>(), false, null));
        Assert.Equal(0, menu.Level);
    }

    [Fact]
    public void A_failed_read_keeps_your_place()
    {
        var menu = Menu(Herdr());
        menu.PressProject("w1");
        menu.PressTab("t2");
        menu.See(Snapshot.Empty);
        Assert.Equal(new[] { MenuState.OfflineParam }, Keys(menu));
        Assert.Equal("t2", menu.TabId);

        menu.See(Herdr());
        Assert.Equal("t2", menu.TabId);
        Assert.Equal(2, menu.Level);
    }

    [Fact]
    public void Projects_resets_a_page_left_open_before()
    {
        var menu = Menu(Herdr());
        menu.OpenOn(HerdrState.AllIssues, null);
        menu.PressTab("t3");
        menu.OpenOnProjects();
        Assert.Equal(0, menu.Level);
        Assert.False(menu.All);
    }
}
