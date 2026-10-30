# Helm

Turns a Logitech MX Keypad into the helm of [herdr](https://herdr.dev): see which of your parallel coding agents need you, jump to them, and steer them with one key. The keypad and the screen always show the same issue.

An issue here is a herdr tab whose agent works on one branch, the way `hagent` opens them. The plugin reads herdr, not GitHub: it does not fetch GitHub issues.

**[How it works](docs/flows.md)** walks through every flow with diagrams: the pages, the keys, answering a question and what the plugin does on its own. **[Setup details](docs/setup.md)** has every config field.

**Status:** tested against herdr, GitHub and real Claude Code questions.

![The keypad from your own page with its three herdr keys, through the herdr menu's projects and a project, to one issue's actions, and what a key shows after a press](docs/keypad.svg?v=e9ce80f7)

## What it does

helm adds three keys you place on your own keypad page, next to your other apps:

| Key | On the key | What it does |
|---|---|---|
| **Herdr projects** | an orange stack, Projects | opens the herdr menu on the keypad, always on your projects; from there their issues and the actions for one issue |
| **Herdr all issues** | a list, with how many issues need you in a badge | opens an overview of all your issues, most urgent first |
| **Open herdr** | a grey terminal, herdr | opens herdr itself in iTerm, like any app key |

In more detail:

- **The All issues key** counts your issues across every herdr workspace, with how many need you in a badge; main tabs do not count. Pressing it opens the menu on one overview of all of them, most urgent first; press one to open its page. The same key sits first on the projects page. That the key opens the menu by itself is not yet confirmed on the keypad; if it does not, press Projects.
- **The herdr menu** has three levels: your projects, the tabs of one project, and the actions for one issue.
- **Pull requests** show on the issue keys: failing checks, requested changes, ready to merge. helm reads them with the GitHub CLI.
- **Actions** such as go, review, babysit and commit go to that issue's agent. The one that fits lights up, and every press is confirmed on the key and in herdr.
- **Questions** an agent asks, such as a permission prompt, show as one key per choice, exactly as the screen lists them. helm reads the question again just before it answers, and sends nothing when it changed.
- **The keypad follows the screen.** Switch tabs in herdr and the keypad's issue page moves along.

Every key shows how its issue is doing with an icon and a word, and its colour says what you do next: red answer or fix, orange decide, green look at the result, blue merge.

![What a key can say: waiting, go?, working and done come from the agent; CI failed, changes, review and ready from the pull request](docs/statuses.svg?v=d17463f6)

A plan counts when the agent's output has a line starting with `Single PR:` or `PR 1:`; change `planPattern` in the config to match your own format.

## When Claude asks you something

When an agent waits on you, for a permission or a choice, its key turns red with `waiting`. To answer it from the keypad:

1. **Go to the issue:** press All issues and pick the red one at the top, or open it through Projects, its project and its key.
2. **Pick your answer:** the issue's page shows the question's choices, one key each, with the numbers your screen shows: `1 Yes`, `2 Always`, `3 Auto mode`, `4 No`. Each choice carries the issue's name under it, and your screen shows that same issue, so you always know whose question you answer. Press the one you want.
3. **See it land:** the key flashes `sent` and herdr shows a notification. If the question changed in the meantime, nothing is sent and the key reads `changed`.

![Answering a question: Claude's choices on screen, the same choices on the keypad, and a question helm cannot read](docs/answer.svg?v=d6108e94)

A question helm cannot read, such as one that wants typed text, shows only `answer on screen`: that key brings the issue's tab to the front so you answer it there.

## Install

```bash
./install.sh
```

It builds the plugin and links it into Logi Options+, links the action config, and adds keypad chords to herdr. It is safe to run again; run it after every pull. Then, once, in Logi Options+: open **Installed Plugins > Herdr** and drag **Herdr projects**, **Herdr all issues** and **Open herdr** onto keys of your keypad.

Needs macOS, Logi Options+ with Logi Plugin Service 6.4 or later, herdr, iTerm2 (configurable), and the GitHub CLI `gh`, logged in, for the pull requests. The script installs the .NET 10 SDK in `~/.dotnet` when it is missing. The editor action and the chords call helper scripts in `~/.local/bin` (`hagent`, `hcleanup`, `hproject`, `hopen`), and the lazygit chord needs `lazygit`; without them, those keys do nothing. The first time a key opens herdr in iTerm, macOS asks whether the Logi Plugin Service may control iTerm: allow it, or the plugin cannot open a new iTerm window with herdr.

## Configure

The issue page's actions live in `config/keypad.json`, which the plugin reads again on every save:

```json
{ "label": "go", "type": "prompt", "value": "go", "color": "#f36a1b", "when": ["go?"] }
```

## More

- **[How it works](docs/flows.md)**: every flow, with diagrams.
- **[Setup details](docs/setup.md)**: all config fields, the herdr chords, developing the plugin and uninstalling it.
