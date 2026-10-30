using System.Text.Json;
using Loupedeck.HerdrPlugin;
using Xunit;

public class PullRequestTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("git@github.com:owner/repo.git", "owner/repo")]
    [InlineData("https://gitlab.com/owner/repo.git", null)]
    [InlineData("", null)]
    public void A_GitHub_remote_gives_its_repo(String remote, String expected) =>
        Assert.Equal(expected, PullRequests.RepoSlug(remote));

    [Theory]
    [InlineData("[]", "none")]
    [InlineData("""[{"__typename":"CheckRun","status":"COMPLETED","conclusion":"SUCCESS"}]""", "passing")]
    [InlineData("""[{"status":"COMPLETED","conclusion":"SKIPPED"},{"status":"COMPLETED","conclusion":"SUCCESS"}]""", "passing")]
    [InlineData("""[{"status":"IN_PROGRESS","conclusion":""},{"status":"COMPLETED","conclusion":"SUCCESS"}]""", "pending")]
    [InlineData("""[{"status":"IN_PROGRESS","conclusion":""},{"status":"COMPLETED","conclusion":"FAILURE"}]""", "failing")]
    [InlineData("""[{"__typename":"StatusContext","state":"ERROR"}]""", "failing")]
    [InlineData("""[{"__typename":"StatusContext","state":"PENDING"}]""", "pending")]
    public void Checks_read_as_one_word(String rollup, String expected)
    {
        using var doc = JsonDocument.Parse(rollup);
        Assert.Equal(expected, PullRequests.ChecksOf(doc.RootElement));
    }

    [Fact]
    public void Gh_output_parses_into_pull_requests_by_branch()
    {
        var json = """
        [{"headRefName":"feat/a","isDraft":false,"number":12,"reviewDecision":"APPROVED","state":"OPEN",
          "statusCheckRollup":[{"status":"COMPLETED","conclusion":"SUCCESS"}],"url":"https://github.com/o/r/pull/12"},
         {"headRefName":"feat/b","number":9,"state":"MERGED","url":"https://github.com/o/r/pull/9"}]
        """;
        var prs = PullRequests.Parse(json);
        Assert.Equal(2, prs.Count);
        Assert.Equal(("feat/a", new PrInfo(12, "https://github.com/o/r/pull/12", "OPEN", false, "APPROVED", "passing")), prs[0]);
        Assert.Equal("feat/b", prs[1].Branch);
        Assert.Equal("MERGED", prs[1].Pr.State);
        Assert.Equal("none", prs[1].Pr.Checks);
        Assert.Empty(PullRequests.Parse(""));
    }

    [Fact]
    public void A_worktree_without_its_branch_falls_back_to_the_tab_name()
    {
        Assert.Equal("feat/slice-3-accept", PullRequests.BranchFor("feat/slice-3-accept\n", "feat/slice-3-invite-and-accept"));
        Assert.Equal("feat/slice-4-clients", PullRequests.BranchFor("", "feat/slice-4-clients"));
        Assert.Equal("feat/slice-5-archive", PullRequests.BranchFor("main", "feat/slice-5-archive"));
        Assert.Equal("", PullRequests.BranchFor(null, "1"));
    }

    [Fact]
    public void Only_the_newest_run_of_each_check_counts()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            [
              { "workflowName": "API - CI PR", "name": "API CI Gate", "conclusion": "FAILURE", "startedAt": "2026-10-30T13:02:41Z" },
              { "workflowName": "API - CI PR", "name": "API CI Gate", "conclusion": "SUCCESS", "startedAt": "2026-10-30T13:21:05Z" },
              { "workflowName": "Docs Guards", "name": "Docs Guards", "conclusion": "CANCELLED", "startedAt": "2026-10-30T13:02:03Z" },
              { "workflowName": "Docs Guards", "name": "Docs Guards", "conclusion": "SUCCESS", "startedAt": "2026-10-30T13:02:39Z" },
              { "workflowName": "API - CI PR", "name": "run-phpunit", "conclusion": "CANCELLED", "startedAt": "2026-10-30T13:02:04Z" },
              { "context": "ci/legacy", "state": "SUCCESS" }
            ]
            """);
        Assert.Equal("passing", PullRequests.ChecksOf(doc.RootElement));

        using var failing = System.Text.Json.JsonDocument.Parse("""
            [
              { "workflowName": "API - CI PR", "name": "API CI Gate", "conclusion": "SUCCESS", "startedAt": "2026-10-30T13:02:41Z" },
              { "workflowName": "API - CI PR", "name": "API CI Gate", "conclusion": "FAILURE", "startedAt": "2026-10-30T13:21:05Z" }
            ]
            """);
        Assert.Equal("failing", PullRequests.ChecksOf(failing.RootElement));
    }
}
