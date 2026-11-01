#!/usr/bin/env python3
"""Captures herdr's real output for the tests, with every name made generic.

Run it from anywhere while herdr runs with a few workspaces and issue tabs open:

    python3 plugin/tests/fixtures/herdr/capture.py

It writes workspaces.json, tabs.json and agents.json next to itself, plus VERSION with herdr's
version. Project names, branch words, paths, terminal titles and session ids are replaced, so the
files can go in a public repo; the structure, ids, numbers, statuses and the /worktrees/ part of a
path stay as herdr gave them, since the plugin reads those. After a herdr update, run it again and
let the tests in HerdrOutputTests.cs say whether the plugin still reads herdr.
"""
import json
import os
import re
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
HERDR = next((p for p in [os.path.expanduser("~/.local/bin/herdr"), "/opt/homebrew/bin/herdr", "/usr/local/bin/herdr"] if os.path.exists(p)), "herdr")

WORDS = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliet", "kilo", "lima",
         "mike", "november", "oscar", "papa", "quebec", "romeo", "sierra", "tango", "uniform", "victor", "whiskey",
         "xray", "yankee", "zulu"]
KEEP = {"feat", "fix", "chore", "docs", "refactor", "test", "main", "and", "the", "of", "to", "in", "per", "a"}
PROJECTS = ["project-a", "project-b", "project-c", "project-d", "project-e"]

words, projects = {}, {}


def word(w):
    if w.isdigit() or w.lower() in KEEP:
        return w
    if w.startswith("wt") and w[2:].isdigit():
        return w
    words.setdefault(w.lower(), WORDS[len(words) % len(WORDS)] + ("" if len(words) < len(WORDS) else str(len(words) // len(WORDS))))
    return words[w.lower()]


def project(name):
    projects.setdefault(name, PROJECTS[len(projects) % len(PROJECTS)])
    return projects[name]


def branch(label):
    """feat/group-records-slice-3-invite :20150 -> feat/alpha-bravo-charlie-3-delta :20150"""
    m = re.match(r"^(.*?)(\s+:\d+)?$", label)
    body, port = m.group(1), m.group(2) or ""
    return "/".join("-".join(word(w) for w in part.split("-")) for part in body.split("/")) + port


def path(p):
    """Any folder becomes /home/dev/<project>, a worktree /home/dev/<project>/.claude/worktrees/<name>."""
    if not p:
        return p
    if "/worktrees/" in p:
        root, name = p.split("/worktrees/", 1)
        return f"/home/dev/{project(os.path.basename(root.rstrip('/').removesuffix('/.claude')))}/.claude/worktrees/{branch(name)}"
    return f"/home/dev/{project(os.path.basename(p.rstrip('/')))}"


def run(*args):
    return json.loads(subprocess.run([HERDR, *args], check=True, capture_output=True, text=True).stdout)


def save(name, data):
    with open(os.path.join(HERE, name), "w") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


workspaces, tabs, agents = run("workspace", "list"), run("tab", "list"), run("agent", "list")

for w in workspaces["result"]["workspaces"]:
    w["label"] = project(w["label"])
for t in tabs["result"]["tabs"]:
    t["label"] = branch(t["label"]) if "/" in t["label"] else t["label"]
for i, a in enumerate(agents["result"]["agents"]):
    for key in ("cwd", "foreground_cwd"):
        a[key] = path(a.get(key))
    a["terminal_title"] = "✳ Claude Code"
    a["terminal_title_stripped"] = "Claude Code"
    a["terminal_id"] = f"term_{i:014x}"
    if isinstance(a.get("agent_session"), dict) and "value" in a["agent_session"]:
        a["agent_session"]["value"] = f"00000000-0000-0000-0000-{i:012d}"

save("workspaces.json", workspaces)
save("tabs.json", tabs)
save("agents.json", agents)
version = subprocess.run([HERDR, "--version"], capture_output=True, text=True).stdout.strip().split()[-1]
with open(os.path.join(HERE, "VERSION"), "w") as f:
    f.write(version + "\n")
print(f"captured herdr {version}: {len(workspaces['result']['workspaces'])} workspaces, "
      f"{len(tabs['result']['tabs'])} tabs, {len(agents['result']['agents'])} agents")
