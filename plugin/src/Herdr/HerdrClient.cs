namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    // One tab with a Claude agent: a ticket tab opened by hagent, or the project's main tab.
    public sealed record Ticket(
        String TabId,
        Int32 Number,
        String WorkspaceId,
        String Branch,
        String ShortName,
        String Status,
        String PaneId,
        String Cwd,
        String Topic,
        Boolean Focused,
        Boolean IsMain);

    // One herdr workspace: a project, with its main tab first and then its tickets in the order you opened them.
    public sealed record Project(
        String WorkspaceId,
        Int32 Number,
        String Name,
        Boolean Focused,
        IReadOnlyList<Ticket> Tabs)
    {
        private static readonly String[] Urgency = { "blocked", "done", "working", "idle", "unknown" };

        // The most urgent status of its tabs: blocked before done before working.
        public String Status => this.Tabs.Select(t => t.Status).OrderBy(s => Array.IndexOf(Urgency, s) is var i && i < 0 ? Urgency.Length : i).FirstOrDefault() ?? "idle";

        public Int32 Count(String status) => this.Tabs.Count(t => t.Status == status);

        public Boolean Equals(Project other) =>
            other != null && other.WorkspaceId == this.WorkspaceId && other.Number == this.Number && other.Name == this.Name
            && other.Focused == this.Focused && other.Tabs.SequenceEqual(this.Tabs);

        public override Int32 GetHashCode() => HashCode.Combine(this.WorkspaceId, this.Name, this.Tabs.Count);
    }

    // Talks to the herdr server through its CLI, which reaches the socket API from outside herdr.
    public static class HerdrClient
    {
        private static readonly String Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        private static readonly String ChildPath = String.Join(":",
            Path.Combine(Home, ".local", "bin"), "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin", "/usr/sbin", "/sbin");

        private static readonly Regex PortSuffix = new(@"\s+:\d+$", RegexOptions.Compiled);

        public static String HerdrBinary
        {
            get
            {
                foreach (var candidate in new[] { Path.Combine(Home, ".local", "bin", "herdr"), "/opt/homebrew/bin/herdr", "/usr/local/bin/herdr" })
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return "herdr";
            }
        }

        // Every herdr workspace as a project, in herdr's order. Returns null when herdr cannot be reached.
        public static List<Project> ReadProjects()
        {
            var workspacesJson = Run(HerdrBinary, "workspace", "list");
            var tabsJson = Run(HerdrBinary, "tab", "list");
            var agentsJson = Run(HerdrBinary, "agent", "list");
            if (workspacesJson == null || tabsJson == null || agentsJson == null)
            {
                return null;
            }

            try
            {
                var agentsByTab = new Dictionary<String, JsonElement>();
                using var agentsDoc = JsonDocument.Parse(agentsJson);
                foreach (var agent in agentsDoc.RootElement.GetProperty("result").GetProperty("agents").EnumerateArray())
                {
                    var tabId = Str(agent, "tab_id");
                    if (tabId != null && !agentsByTab.ContainsKey(tabId))
                    {
                        agentsByTab[tabId] = agent.Clone();
                    }
                }

                using var tabsDoc = JsonDocument.Parse(tabsJson);
                var tabsByWorkspace = tabsDoc.RootElement.GetProperty("result").GetProperty("tabs").EnumerateArray()
                    .Select(t => t.Clone())
                    .GroupBy(t => Str(t, "workspace_id") ?? "")
                    .ToDictionary(g => g.Key, g => g.ToList());

                using var workspacesDoc = JsonDocument.Parse(workspacesJson);
                var projects = new List<Project>();
                foreach (var workspace in workspacesDoc.RootElement.GetProperty("result").GetProperty("workspaces").EnumerateArray())
                {
                    var id = Str(workspace, "workspace_id");
                    var tabs = tabsByWorkspace.GetValueOrDefault(id ?? "") ?? new List<JsonElement>();
                    projects.Add(new Project(
                        WorkspaceId: id,
                        Number: Int(workspace, "number"),
                        Name: Str(workspace, "label") ?? id,
                        Focused: Bool(workspace, "focused"),
                        Tabs: ReadTabs(tabs, agentsByTab)));
                }

                return projects.OrderBy(p => p.Number).ToList();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not parse herdr output");
                return null;
            }
        }

        // The tabs of one workspace that hold an agent or a ticket: main tabs first, then tickets by number.
        private static List<Ticket> ReadTabs(List<JsonElement> tabs, Dictionary<String, JsonElement> agentsByTab)
        {
            var mains = new List<(JsonElement Tab, JsonElement? Agent)>();
            var tickets = new List<(JsonElement Tab, JsonElement? Agent, String Branch)>();
            foreach (var tab in tabs.OrderBy(t => Int(t, "number")))
            {
                var label = Str(tab, "label") ?? "";
                var hasAgent = agentsByTab.TryGetValue(Str(tab, "tab_id") ?? "", out var agent);
                var cwd = hasAgent ? Str(agent, "cwd") ?? "" : "";

                // hagent names a ticket tab after its branch and opens it in a worktree.
                if (label.Contains('/') || cwd.Contains("/worktrees/"))
                {
                    tickets.Add((tab, hasAgent ? agent : null, PortSuffix.Replace(label, "")));
                }
                else if (hasAgent)
                {
                    mains.Add((tab, agent));
                }
            }

            var result = new List<Ticket>();
            for (var i = 0; i < mains.Count; i++)
            {
                var (tab, agent) = mains[i];
                var label = Str(tab, "label") ?? "";
                var name = Int32.TryParse(label, out _) || label.Length == 0 ? (i == 0 ? "main" : $"main {i + 1}") : label;
                result.Add(ToTicket(tab, agent, name, name, isMain: true));
            }

            var shortNames = TicketNames.Shorten(tickets.Select(t => t.Branch).ToList());
            for (var i = 0; i < tickets.Count; i++)
            {
                result.Add(ToTicket(tickets[i].Tab, tickets[i].Agent, tickets[i].Branch, shortNames[i], isMain: false));
            }

            return result;
        }

        private static Ticket ToTicket(JsonElement tab, JsonElement? agent, String branch, String shortName, Boolean isMain) =>
            new(
                TabId: Str(tab, "tab_id"),
                Number: Int(tab, "number"),
                WorkspaceId: Str(tab, "workspace_id"),
                Branch: branch,
                ShortName: shortName,
                Status: agent.HasValue ? Str(agent.Value, "agent_status") ?? "unknown" : Str(tab, "agent_status") ?? "unknown",
                PaneId: agent.HasValue ? Str(agent.Value, "pane_id") : null,
                Cwd: agent.HasValue ? Str(agent.Value, "cwd") : null,
                Topic: agent.HasValue ? Str(agent.Value, "terminal_title_stripped") : null,
                Focused: Bool(tab, "focused"),
                IsMain: isMain);

        public static void FocusWorkspace(String workspaceId) => Run(HerdrBinary, "workspace", "focus", workspaceId);

        public static void FocusTab(String tabId) => Run(HerdrBinary, "tab", "focus", tabId);

        public static void Prompt(String paneId, String text) => Run(HerdrBinary, "agent", "prompt", paneId, text);

        public static void SendKeys(String paneId, params String[] keys) =>
            Run(HerdrBinary, new[] { "agent", "send-keys", paneId }.Concat(keys).ToArray());

        private static DateTime _lastClientLaunch = DateTime.MinValue;

        // True when a herdr window is open somewhere: a "herdr" process that attaches to the server,
        // as opposed to "herdr server" or the short CLI calls this plugin makes.
        public static Boolean HasAttachedClient()
        {
            var ps = Run("/bin/ps", "-axo", "args=");
            if (ps == null)
            {
                return true;
            }

            foreach (var line in ps.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0 || System.IO.Path.GetFileName(tokens[0]) != "herdr")
                {
                    continue;
                }

                if (tokens.Length == 1 || tokens[1].StartsWith("--session") || tokens[1].StartsWith("--remote")
                    || (tokens.Length > 2 && tokens[1] == "session" && tokens[2] == "attach"))
                {
                    return true;
                }
            }

            return false;
        }

        // Opens a new iTerm window and starts herdr in it, in your own shell so its PATH applies.
        // macOS asks once whether the Logi Plugin Service may control iTerm.
        public static void OpenHerdrInIterm(String bundleId)
        {
            if (DateTime.UtcNow - _lastClientLaunch < TimeSpan.FromSeconds(15))
            {
                return;
            }

            _lastClientLaunch = DateTime.UtcNow;
            // Reuse the window iTerm opens on launch; when it was already running, open a new one.
            var script = $@"
set wasRunning to application id ""{bundleId}"" is running
tell application id ""{bundleId}""
    activate
    if not wasRunning then delay 1
    if wasRunning or (count of windows) = 0 then
        create window with default profile
    end if
    tell current session of current window to write text ""herdr""
end tell";
            PluginLog.Info("No herdr window open: starting herdr in a new iTerm window");
            Run("/usr/bin/osascript", "-e", script);
        }

        public static void ActivateApp(String bundleId)
        {
            if (!String.IsNullOrWhiteSpace(bundleId))
            {
                Run("/usr/bin/open", "-b", bundleId);
            }
        }

        public static void Shell(String command, String cwd) =>
            RunIn(cwd, "/bin/bash", new[] { "-c", command }, new Dictionary<String, String> { ["TICKET_CWD"] = cwd ?? "" });

        // Runs a command and returns its stdout, or null when it fails or takes longer than five seconds.
        private static String Run(String file, params String[] args) => RunIn(null, file, args);

        private static String RunIn(String cwd, String file, String[] args, IDictionary<String, String> env = null)
        {
            try
            {
                var psi = new ProcessStartInfo(file)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.Exists(cwd) ? cwd : Home,
                };
                foreach (var arg in args)
                {
                    psi.ArgumentList.Add(arg);
                }

                psi.Environment["PATH"] = ChildPath;
                psi.Environment["HOME"] = Home;
                foreach (var pair in env ?? new Dictionary<String, String>())
                {
                    psi.Environment[pair.Key] = pair.Value;
                }

                using var process = Process.Start(psi);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(true); } catch { }
                    PluginLog.Warning($"Timed out: {file} {String.Join(' ', args)}");
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    PluginLog.Warning($"Exit {process.ExitCode}: {file} {String.Join(' ', args)}: {stderr.Result.Trim()}");
                    return null;
                }

                return stdout.Result;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Could not run {file}");
                return null;
            }
        }

        private static Int32 Int(JsonElement element, String name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 0;

        private static Boolean Bool(JsonElement element, String name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

        private static String Str(JsonElement element, String name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
