# Keypad flows

How the keypad works with herdr: the pages, what each key does, and what the plugin does on its own. The keys carry Dutch labels for navigation (`terug`, `meer`); this page uses them as they appear on the keypad.

![The keypad from your own page with its three herdr keys, through the herdr menu's projects and a project, to one issue's actions, and what a key shows after a press](keypad.svg?v=0d03498b)

## Your page

The keypad starts on a page you set up yourself in Logi Options+, with your own apps and keys. helm adds three keys to it, each with its own job:

| Key | On the key | What it does | Where |
|---|---|---|---|
| Herdr issues | an orange stack, Issues | opens the herdr menu below | on the keypad |
| Herdr status | a bell in the colour of what needs you, with the count in a badge | jumps to the most urgent issue | on the keypad and the screen |
| Open herdr | a grey terminal, herdr, like an app key | opens herdr itself in iTerm: in front if it runs, else in a new iTerm window | on the screen |

## One rule: the keypad and the screen show the same issue

Whatever you do on one, the other follows. Pressing a tab on the keypad shows it in herdr. Switching tabs in herdr, with the keyboard or the mouse, moves the keypad's issue page along. So an action always goes to the issue you are looking at.

You still reach every project and every issue from the keypad; the rule only concerns the issue whose page is open.

## Statuses

Every key shows one word for its issue, from its agent and its pull request, in herdr's colours. The agent wins while it asks or works; after that the pull request speaks.

![What a key can say](statuses.svg?v=6923af9b)

| Colour | Status | Meaning |
|---|---|---|
| red | `waiting` | the agent asks you something: a permission or a question |
| orange | `go?` | the agent sits still with a plan waiting for your go, and there is no pull request yet |
| red | `CI failed` | the pull request's checks fail |
| red | `changes` | a reviewer asked for changes |
| green | `done` | the agent finished its turn: look at the result |
| blue | `ready` | the pull request is approved and its checks pass: yours to merge |
| yellow | `working` | the agent is busy |
| grey | `review`, `draft` | the pull request waits on a review, or is a draft |
| dark grey | `idle`, `merged`, `closed` | nothing going on, or nothing left to do |

A plan is a line in the agent's last output that matches `planPattern` in `config/keypad.json`; by default a line starting with `Single PR:` or `PR 1:`.

A project key shows the most urgent status among its tabs, with up to two counts, such as "1 waiting, 1 go?". A white frame marks the project and the tab that are on screen.

## Navigation

```mermaid
flowchart TD
    status["Herdr status key<br/>on your page"] -->|"press: the most urgent issue"| issue
    key["Issues key<br/>on your page"] --> projects["Projects<br/>one key per workspace"]
    projects -->|"press a project:<br/>herdr shows that workspace"| project["Project<br/>main tab, then issues"]
    project -->|"press a tab:<br/>herdr shows that tab"| issue["Issue<br/>seven actions and terug"]
    issue -->|terug| project
    project -->|terug| projects
    projects -->|back key| closed(["Folder closed"])
```

When a level has more items than fit beside the way back, seven share a page with `meer`, which pages through the rest.

## The keypad and the screen together

```mermaid
sequenceDiagram
    actor You
    participant Keypad
    participant Plugin
    participant herdr
    You->>Keypad: press "7 checkout"
    Keypad->>Plugin: open its issue page
    Plugin->>herdr: tab focus
    herdr-->>You: 7 checkout on screen
    Note over Plugin,herdr: the plugin reads herdr every two seconds
    You->>herdr: switch to "3 sign up" with the keyboard
    Plugin->>herdr: read
    herdr-->>Plugin: 3 sign up is on screen
    Plugin->>Keypad: issue page of 3 sign up
```

- **On an issue page** the keypad follows the tab herdr shows, across projects too.
- **On the projects and project pages** it does not jump, because you are browsing there; it frames what is on screen instead.
- **Opening the folder** lands on the issue on screen, unless the status key just picked one.
- **Only a change on the screen counts.** Right after a key press herdr may still show the previous tab for a moment; the keypad does not jump back to it.

## Picking a tab

```mermaid
flowchart TD
    press["Press a tab on a project page"] --> keys["Keys switch to that issue's page"]
    press --> focus["herdr tab focus"]
    focus --> window{"Is a herdr window open?"}
    window -->|yes| front["Bring iTerm to the front"]
    window -->|no| open["Open a new iTerm window and run herdr<br/>(openHerdrWhenClosed)"]
```

The herdr server keeps running after iTerm closes, so statuses and actions work without a window; only showing the tab needs one.

## The issue page

The page shows the issue with its status, and its pull request number when it has one, seven actions and `terug`. The action that fits the issue's status lights up, filled with its colour; an action that does nothing in that status is greyed out.

| Issue status | Lights up | Greyed out |
|---|---|---|
| `go?` | `go` | `open PR` without a pull request |
| `CI failed`, `changes` | `babysit pr` | |
| `done` | `review and fix` | |
| `ready` | `open PR` | |
| `waiting` | the page shows the agent's question instead, see below | |

| Key | What it does |
|---|---|
| the issue | shows its tab again |
| `go` | sends `go`: approves the plan the agent shows |
| `review and fix` | runs `/review-and-fix` |
| `babysit pr` | runs `/babysit-pr`: drives the pull request through its checks and review |
| `commit` | asks for the work in logical conventional commits |
| `stop` | presses Esc: interrupts the agent |
| `open PR` | opens the issue's pull request in your browser |
| `editor` | opens the issue's worktree with `hopen` |
| `terug` | back to the project |

The actions, and when they light up or grey out, come from `config/keypad.json`; see [Setup details](setup.md) to change them.

## Answering a question

While an agent waits on you, its page shows the question it asks, such as Claude Code's permission prompt, with one key per choice exactly as the screen lists them.

![Answering a question: Claude's choices on screen, the same choices on the keypad, and a question helm cannot read](answer.svg?v=23aa5a7c)

- **Each key sends its own choice.** A numbered choice is picked by the number the screen shows, which is not always its position: in Claude Code 2.1, `No` is choice 4, after `Yes, and switch to auto mode`. Choices without numbers, such as the folder-trust question, are reached with the arrow keys from the selected one, then Enter.
- **Nothing is sent blind.** Just before sending, helm reads the question again; when the choices changed, it sends nothing and the key reads `changed`.
- **A question helm cannot read** shows only `answer on screen`, which brings the issue's tab to the front so you answer it there.
- **Every choice names its issue.** Each choice key carries the issue's name under it, the top-left key shows the issue and what it asks about, and the screen shows that same issue.
- **Yes in green, No in red,** anything else neutral, so the answer reads at a glance.

## Pressing an action

```mermaid
flowchart TD
    press["Press an action"] --> dim{"Is it greyed out?"}
    dim -->|yes| notNow["The key flashes: not now<br/>nothing is sent"]
    dim -->|no| send["Send it to the issue's agent"]
    send --> taken{"Did herdr take it?"}
    taken -->|yes| sent["The key flashes: sent<br/>herdr shows: go: 7 checkout"]
    taken -->|no| failed["The key flashes the reason, such as waits on you<br/>herdr shows: go not sent"]
```

Each action goes to the agent of the issue on the page, by its pane, never to whatever pane has focus. The page stays open for the next action.

```mermaid
flowchart LR
    prompt["Prompt actions<br/>go, review and fix, babysit pr, commit"] --> promptCmd["herdr agent prompt<br/>the text plus Enter"]
    keysAction["Key actions<br/>stop, and the answers to a question"] --> keysCmd["herdr agent send-keys<br/>Esc, a number, or arrows and Enter"]
    openPr["Open PR"] --> openCmd["open<br/>the pull request in your browser"]
```

## The status key

A key for your normal keypad page, outside the folder: how many issues need you across every project, and how many are ready to merge. It is filled with the colour of the most urgent status; with nothing waiting it reads "all clear".

![Walking through what needs you: the count, the most urgent issue, the next one, and an issue you handled dropping out](status-key.svg?v=d6fbdb37)

```mermaid
flowchart TD
    look["The status key: 3 need you, 1 ready"] --> press["Press it"]
    press --> any{"Does an issue need you?"}
    any -->|no| projects["The folder opens on the projects page"]
    any -->|yes| within{"Pressed within ten seconds<br/>of the last press?"}
    within -->|no| first["The most urgent issue:<br/>waiting, go?, CI failed, changes, then done"]
    within -->|yes| next["The next issue in that list"]
    first --> show["herdr shows it, the folder opens on it,<br/>the key shows where you are, such as 1 of 2"]
    next --> show
```

An issue that no longer needs you drops out of the list, so pressing the key walks you through whatever is left. `ready` does not count as needing you; the key shows it beside the count instead.

The key opens the folder through a generic action of the Logi plugin API that is not documented for folders. If the menu does not open, press the Issues key: it opens on the issue the status key picked.

## What happens on its own

One reader serves the whole plugin, so the folder and the status key always agree. It reads herdr every two seconds and pull requests once a minute.

```mermaid
flowchart TD
    tick["Every two seconds"] --> read["Read workspaces, tabs and agents from herdr"]
    read --> reachable{"Is herdr reachable?"}
    reachable -->|no| offline["The folder shows herdr offline<br/>press it to retry"]
    reachable -->|yes| plan["For an idle or done agent whose state changed:<br/>read its last 30 lines for a plan"]
    plan --> status["Combine the agent with its pull request<br/>into one status word"]
    status --> changed{"Did anything change?"}
    changed -->|no| quiet["Nothing to redraw"]
    changed -->|yes| folder["Folder: follow the screen, drop closed tabs,<br/>redraw the keys that changed"]
    changed -->|yes| statusKey["Status key: redraw its counts"]
    minute["Every minute, and for a new issue at once"] --> branch["Read each issue folder's real branch and GitHub repo"]
    branch --> gh["Per repo: gh pr list for your open<br/>and your merged pull requests"]
    gh --> status
```

- **Pull requests come from the GitHub CLI.** Per repo, helm asks for your own open pull requests with their checks and review decision, and your own merged ones; two calls a minute, whatever the number of issues. It matches them to an issue by the branch the issue's worktree is on, not by the tab's label, which can be stale. Without `gh`, or for a repo that is not on GitHub, issues simply have no pull request.
- **A failed call keeps what helm knew,** so one GitHub hiccup does not blank the keys.
- **A closed tab** drops the folder back to its project; a closed workspace back to the projects.
- **The folder may close on an app switch.** The Logi Plugin Service can close it when iTerm comes to the front. Pressing the Issues key again opens the issue on screen.

