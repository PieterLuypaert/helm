# Keypad flows

How the **Herdr** folder on the keypad moves between pages, what each action runs, and what the plugin does on its own. The keys themselves carry Dutch labels (`terug`, `toestaan`, `meer`); this page uses them as they appear on the keypad.

## Navigation

The folder has three levels. Pressing a key goes one level down; `terug` goes one level up. On the projects page, the back key the Logi Plugin Service adds closes the folder.

```mermaid
flowchart TD
    key["Herdr key<br/>opens the folder"] --> projects["Projects<br/>one key per workspace"]
    projects -->|"press a project:<br/>herdr shows that workspace"| project["Project<br/>main tab, then tickets"]
    project -->|"press a tab:<br/>that tab shows in iTerm"| ticket["Ticket<br/>seven actions and terug"]
    ticket -->|terug| project
    project -->|terug| projects
    projects -->|back key| closed(["Folder closed"])
```

Every key has a stripe in a status colour. A project's stripe is the most urgent status among its agents.

| Stripe | Status |
|---|---|
| red | the agent waits on you (`blocked`) |
| orange | done |
| yellow | working |
| grey | idle |

## Picking a tab

The keys switch to the ticket page at once; focusing the tab runs in the background.

```mermaid
flowchart TD
    press["Press a tab on a project page"] --> keys["Keys switch to that ticket's actions"]
    press --> focus["herdr tab focus"]
    focus --> window{"Is a herdr window open?"}
    window -->|yes| front["Bring iTerm to the front"]
    window -->|no| open["Open a new iTerm window and run herdr<br/>(openHerdrWhenClosed)"]
```

A herdr window is any `herdr` process that attaches to the server. The server itself, `herdr server`, keeps running after iTerm closes, so statuses and actions work without a window; only showing the tab needs one.

## Actions on a ticket

Each action goes to the agent of the ticket you picked on the keypad, never to whatever pane has focus. After an action, the ticket page stays open for the next one.

```mermaid
flowchart LR
    prompt["Prompt actions<br/>go, review and fix, code review, commit"] --> promptCmd["herdr agent prompt<br/>the text plus Enter"]
    keysAction["Key actions<br/>toestaan, stop"] --> keysCmd["herdr agent send-keys<br/>Enter or Esc"]
    shell["Shell action<br/>editor"] --> shellCmd["hopen<br/>the ticket's worktree in your editor"]
```

| Key | What it does |
|---|---|
| the ticket | focuses its tab again |
| `go` | sends `go`: approves the plan the agent shows |
| `toestaan` | presses Enter: picks the selected option of a permission prompt, usually Yes |
| `review and fix` | runs `/review-and-fix` |
| `code review` | runs `/code-review` |
| `commit` | asks for the work in logical conventional commits |
| `stop` | presses Esc: interrupts the agent |
| `editor` | opens the ticket's worktree with `hopen` |
| `terug` | back to the project |

The actions come from `config/keypad.json`; see the README to change them.

## What happens on its own

While the folder is open, the plugin reads herdr every two seconds and redraws only what changed.

```mermaid
flowchart TD
    tick["Every two seconds, while the folder is open"] --> read["Read workspaces, tabs and agents from herdr"]
    read --> reachable{"Is herdr reachable?"}
    reachable -->|no| offline["One key: herdr niet bereikbaar<br/>press it to retry"]
    reachable -->|yes| closed{"Was the tab or workspace you are on closed?"}
    closed -->|tab closed| upOne["Back to its project"]
    closed -->|workspace closed| upTwo["Back to the projects"]
    closed -->|no| same{"Same keys as before?"}
    same -->|yes| redraw["Redraw only the keys whose look changed"]
    same -->|no| relayout["Lay out the keys again"]
```

Two more behaviours:

- **More items than keys.** A page has nine keys. When a level has more items than fit beside the way back, seven share a page with `meer`, which pages through the rest.
- **The folder closes on an app switch.** The Logi Plugin Service may close the folder when iTerm comes to the front. The plugin keeps its place, so pressing the Herdr key again opens the page you were on.
