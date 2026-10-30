# Setup details

Everything the README keeps short: every field of the action config, the herdr chords, developing the plugin and removing it.

## Actions

Edit `config/keypad.json`. The plugin reads it again when it changes, so no rebuild is needed.

| Field | Meaning |
|---|---|
| `type: "prompt"` | `value` plus Enter, to the issue's agent (`herdr agent prompt`) |
| `type: "keys"` | key presses for the issue's agent, space separated: `enter`, `esc`, `ctrl+c` |
| `type: "shell"` | a command run in the issue's worktree, with `$ISSUE_CWD` set |
| `color` | the stripe on the key, `#RRGGBB` |
| `when` | the issue statuses in which the key lights up, such as `["go?"]` |
| `onlyWhen` | the statuses in which the key works at all; it is greyed out in the others |
| `terminalBundleId` | the app brought to the front when you pick an issue (iTerm2 by default); `""` leaves focus alone |
| `openHerdrWhenClosed` | with no herdr window open, picking an issue opens a new iTerm window and starts herdr in it, on that issue (iTerm only; `true` by default). macOS asks once whether the Logi Plugin Service may control iTerm |

## Chords

`herdr/keys.toml` adds these direct chords, for keystroke keys in Options+:

| Chord | Action |
|---|---|
| `ctrl+alt+shift+g` | new issue tab (`hagent`) |
| `ctrl+alt+shift+k` | clean up the issue tab (`hcleanup`) |
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
