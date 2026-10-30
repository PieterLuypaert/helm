# Helm

Turns a Logitech MX Keypad into the helm of [herdr](https://herdr.dev): see which of your parallel coding agents need you, jump to them, and steer them with one key. The keypad and the screen always show the same issue.

An issue here is a herdr tab whose agent works on one branch, the way `hagent` opens them. The plugin reads herdr, not GitHub: it does not fetch GitHub issues.

**[How it works](docs/flows.md)** walks through every flow with diagrams: the pages, the keys, answering a question and what the plugin does on its own. **[Setup details](docs/setup.md)** has every config field.

**Status:** tested against herdr, GitHub and real Claude Code questions.

![The keypad from your own page with its three herdr keys, through the herdr menu's projects and a project, to one issue's actions, and what a key shows after a press](docs/keypad.svg?v=2813b4c4)

## What it does

helm adds three keys you place on your own keypad page, next to your other apps: black tiles with a big icon and a short word under it, like your own macros.

| Key | On the key | What it does |
|---|---|---|
| **Herdr projects** | an orange stack | opens the herdr menu on the keypad, on your projects |
| **Herdr all issues** | the number of issues over a list, with how many need you in a badge | opens the herdr menu on every issue at once, most urgent first |
| **Open herdr** | a grey terminal | opens herdr itself in iTerm, like any app key |

- **The herdr menu** goes from your projects to a project's tabs, or from the issues key straight to every issue at once, and on to the actions for one issue. The last key goes one level up and is named after where it goes, such as `projects`; Logi's own arrow on the first key leaves the menu.
- **Pull requests** show on the issue keys: failing checks, requested changes, ready to merge. helm reads them with the GitHub CLI.
- **Actions** such as go, review, babysit and commit go to that issue's agent. The issue's page shows only the ones that fit it now, the best one lit up, and every press is confirmed on the key and in herdr.
- **Questions** an agent asks, such as a permission prompt, show as one key per choice, exactly as the screen lists them. helm reads the question again just before it answers, and sends nothing when it changed.
- **The keypad follows the screen.** Switch tabs in herdr and the keypad's issue page moves along.

Every key shows how its issue is doing with one icon per colour, and the colour says what you do next: red answer or fix, orange decide, green look at the result, blue merge, yellow wait, grey nothing for you. The issue's own page names the exact status.

![What a key can say: one icon per colour, with the statuses behind each](docs/statuses.svg?v=f0cd7745)

A plan counts when the agent's output has a line starting with `Single PR:` or `PR 1:`; change `planPattern` in the config to match your own format.

## When Claude asks you something

When an agent waits on you, for a permission or a choice, its key turns red with `waiting`. To answer it from the keypad:

1. **Go to the issue:** press the issues key and pick the red one at the top, or open it through Projects, its project and its key.
2. **Pick your answer:** the issue's page shows the question's choices, one key each, with the numbers your screen shows: `1 Yes`, `2 Always`, `3 Auto mode`, `4 No`. Each choice carries the issue's name under it, and your screen shows that same issue, so you always know whose question you answer. Press the one you want.
3. **See it land:** the key flashes `sent` and herdr shows a notification. If the question changed in the meantime, nothing is sent and the key reads `changed`.

![Answering a question: Claude's choices on screen, the same choices on the keypad, and a question helm cannot read](docs/answer.svg?v=135d83fa)

A question the keys cannot hold, such as one that wants typed text, shows `answer on screen`: that key brings the issue's tab to the front so you answer it there.

## Install

```bash
./install.sh
```

It builds the plugin and links it into Logi Options+, links the action config, and adds keypad chords to herdr. It is safe to run again; run it after every pull. Then, once, in Logi Options+: open **Installed Plugins > Herdr** and drag **Herdr projects**, **Herdr all issues** and **Open herdr** onto keys of your keypad.

Needs macOS, Logi Options+ with Logi Plugin Service 6.4 or later, herdr, iTerm2 (configurable), and the GitHub CLI `gh`, logged in, for the pull requests. The script installs the .NET 10 SDK in `~/.dotnet` when it is missing. The editor action and the chords call helper scripts in `~/.local/bin` (`hagent`, `hcleanup`, `hproject`, `hopen`), and the lazygit chord needs `lazygit`; without them, those keys do nothing. The first time a key opens herdr in iTerm, macOS asks whether the Logi Plugin Service may control iTerm: allow it, or the plugin cannot open a new iTerm window with herdr.

## Configure

The issue page's actions live in `config/keypad.json`, which the plugin reads again on every save:

```json
{ "label": "go", "type": "prompt", "value": "go", "color": "#f36a1b", "when": ["go?"], "onlyWhen": ["go?", "idle", "done"] }
```

## More

- **[How it works](docs/flows.md)**: every flow, with diagrams.
- **[Setup details](docs/setup.md)**: all config fields, the herdr chords, developing the plugin and uninstalling it.
