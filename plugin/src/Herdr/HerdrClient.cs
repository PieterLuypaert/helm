namespace Loupedeck.HerdrPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    // One ticket tab in herdr: a tab opened by hagent, with its Claude agent.
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
        Boolean Focused);

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

        // Ticket tabs of the workspace that holds the focused tab, in the order they were opened.
        // Returns null when herdr cannot be reached.
        public static List<Ticket> ReadTickets()
        {
            var tabsJson = Run(HerdrBinary, "tab", "list");
            var agentsJson = Run(HerdrBinary, "agent", "list");
            if (tabsJson == null || agentsJson == null)
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
                var tabs = tabsDoc.RootElement.GetProperty("result").GetProperty("tabs").EnumerateArray().Select(t => t.Clone()).ToList();
                var focusedWorkspace = tabs.Where(t => t.TryGetProperty("focused", out var f) && f.GetBoolean())
                                           .Select(t => Str(t, "workspace_id")).FirstOrDefault();

                var raw = new List<(JsonElement Tab, JsonElement? Agent, String Branch)>();
                foreach (var tab in tabs)
                {
                    if (focusedWorkspace != null && Str(tab, "workspace_id") != focusedWorkspace)
                    {
                        continue;
                    }

                    var label = Str(tab, "label") ?? "";
                    agentsByTab.TryGetValue(Str(tab, "tab_id") ?? "", out var agent);
                    var hasAgent = agentsByTab.ContainsKey(Str(tab, "tab_id") ?? "");
                    var cwd = hasAgent ? Str(agent, "cwd") ?? "" : "";

                    // hagent names a ticket tab after its branch and opens it in a worktree.
                    var isTicket = label.Contains('/') || cwd.Contains("/worktrees/");
                    if (!isTicket)
                    {
                        continue;
                    }

                    raw.Add((tab, hasAgent ? agent : null, PortSuffix.Replace(label, "")));
                }

                var shortNames = TicketNames.Shorten(raw.Select(r => r.Branch).ToList());
                var tickets = new List<Ticket>();
                for (var i = 0; i < raw.Count; i++)
                {
                    var (tab, agent, branch) = raw[i];
                    tickets.Add(new Ticket(
                        TabId: Str(tab, "tab_id"),
                        Number: tab.TryGetProperty("number", out var n) ? n.GetInt32() : 0,
                        WorkspaceId: Str(tab, "workspace_id"),
                        Branch: branch,
                        ShortName: shortNames[i],
                        Status: agent.HasValue ? Str(agent.Value, "agent_status") ?? "unknown" : Str(tab, "agent_status") ?? "unknown",
                        PaneId: agent.HasValue ? Str(agent.Value, "pane_id") : null,
                        Cwd: agent.HasValue ? Str(agent.Value, "cwd") : null,
                        Topic: agent.HasValue ? Str(agent.Value, "terminal_title_stripped") : null,
                        Focused: tab.TryGetProperty("focused", out var f) && f.GetBoolean()));
                }

                return tickets.OrderBy(t => t.Number).ToList();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not parse herdr output");
                return null;
            }
        }

        public static void FocusTab(String tabId) => Run(HerdrBinary, "tab", "focus", tabId);

        public static void Prompt(String paneId, String text) => Run(HerdrBinary, "agent", "prompt", paneId, text);

        public static void SendKeys(String paneId, params String[] keys) =>
            Run(HerdrBinary, new[] { "agent", "send-keys", paneId }.Concat(keys).ToArray());

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

        private static String Str(JsonElement element, String name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
