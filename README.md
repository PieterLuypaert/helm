# Helm

The setup for my Logitech MX Keypad as the helm of [herdr](https://herdr.dev): a key per ticket tab, and the actions for the ticket you pick.

It holds three things:

- **`plugin/`**: a Logi Actions plugin (C#, .NET 10) with one dynamic folder, **Herdr tickets**.
- **`config/keypad.json`**: the actions on a ticket's page.
- **`herdr/keys.toml`**: direct `ctrl+alt+shift` chords for the ticketflow scripts, so a plain keystroke key can run them.

## Install

```bash
./install.sh
```

It installs the .NET 10 SDK in `~/.dotnet` when missing (no sudo), builds the plugin and links it into the Logi Plugin Service, links `~/.config/herdr-keypad/config.json` to `config/keypad.json`, and writes `herdr/keys.toml` into herdr's `config.toml` between `helm: keypad chords` markers. It is safe to run again; run it after every pull.

Then, once, in Logi Options+: select the keypad, open **Installed Plugins > Herdr** and drag **Herdr tickets** onto a key.

Needs Logi Options+ with Logi Plugin Service 6.4 or later, herdr, and the herdr-ticketflow scripts in `~/.local/bin` for the chords and the editor action.

## The folder

The key opens the ticket page: every ticket tab of the herdr workspace you are in, in the order you opened them. A ticket tab is a tab named after a branch, or one whose agent runs in a worktree, which is what `hagent` makes. Each key shows the branch without its `feat/` prefix and without the leading words most tickets share, and a stripe in the agent's status colour:

| Stripe | Status |
|---|---|
| red | waiting on you (`blocked`) |
| orange | done |
| yellow | working |
| grey | idle |

Pressing a ticket focuses its tab in herdr, brings the terminal to the front and turns the folder into that ticket's page:

| | | |
|---|---|---|
| the ticket (focus it again) | go | toestaan |
| review and fix | code review | commit |
| stop | editor | terug |

Every action goes to that ticket's agent through the herdr CLI, never to whatever pane happens to have focus. **terug** returns to the ticket page; the back key the service adds after the tickets closes the folder. The keys refresh every two seconds while the folder is open; a closed tab drops out on its own.

## Changing the actions

Edit `config/keypad.json`. The plugin reads it again when it changes, so no rebuild is needed.

| Field | Meaning |
|---|---|
| `type: "prompt"` | `value` plus Enter, to the ticket's agent (`herdr agent prompt`) |
| `type: "keys"` | key presses for the ticket's agent, space separated: `enter`, `esc`, `ctrl+c` |
| `type: "shell"` | a command run in the ticket's worktree, with `$TICKET_CWD` set |
| `color` | the stripe on the key, `#RRGGBB` |
| `terminalBundleId` | the app brought to the front when you pick a ticket (iTerm2 by default); `""` leaves focus alone |

## Chords

`herdr/keys.toml` adds these direct chords, for keystroke keys in Options+:

| Chord | Action |
|---|---|
| `ctrl+alt+shift+g` | new ticket tab (`hagent`) |
| `ctrl+alt+shift+k` | clean up the ticket tab (`hcleanup`) |
| `ctrl+alt+shift+s` | pick a project workspace (`hproject`) |
| `ctrl+alt+shift+e` | open the tab's checkout in the editor (`hopen`) |
| `ctrl+alt+shift+l` | lazygit in the tab's folder |

herdr's built-in actions get their chords in the `[keys]` table of `config.toml`, which a block at the end of the file cannot extend, so they stay there by hand:

```toml
next_agent = ["prefix+a", "ctrl+alt+shift+n"]
previous_agent = ["prefix+shift+a", "ctrl+alt+shift+p"]
open_notification_target = ["prefix+o", "ctrl+alt+shift+o"]
workspace_picker = ["prefix+w", "ctrl+alt+shift+w"]
focus_agent = "ctrl+alt+shift+1..9"
```

## Developing

```bash
cd plugin/src
DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet watch build
```

Each build relinks the plugin and asks the Logi Plugin Service to reload it. The plugin logs to `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/Herdr.log`. After moving the repo, the service may refuse the new path with "plugin 'Herdr' is already loaded": use **Restart Logi Plugin Service** in the Options+ settings.

## Uninstall

```bash
rm ~/Library/Application\ Support/Logi/LogiPluginService/Plugins/HerdrPlugin.link ~/.config/herdr-keypad/config.json
```

Then remove the `helm: keypad chords` block from herdr's `config.toml` and restart the Logi Plugin Service.
