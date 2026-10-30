# Helm

Turns a Logitech MX Keypad into the helm of [herdr](https://herdr.dev): see which of your parallel coding agents need you, jump to them, and steer them with one key. The keypad and the screen always show the same ticket.

![The Herdr folder on the keypad: the status key, the projects, a project, a ticket, and the feedback after a press](docs/keypad.svg)

## What it does

- **A status key** shows what needs you across every herdr workspace. Pressing it takes herdr and the keypad to the most urgent ticket.
- **A folder** with three levels: your projects, the tabs of one project, and the actions for one ticket.
- **Actions** such as go, review and commit go to that ticket's agent. The one that fits lights up, and every press is confirmed on the key and in herdr.
- **The keypad follows the screen.** Switch tabs in herdr and the keypad's ticket page moves along.

Keys use herdr's colours: red when an agent waits on you, orange for a plan waiting for your go or finished work, yellow while it works, grey when idle.

## Install

```bash
./install.sh
```

It builds the plugin and links it into Logi Options+, links the action config, and adds keypad chords to herdr. It is safe to run again; run it after every pull. Then, once, in Logi Options+: open **Installed Plugins > Herdr** and drag the **Herdr** folder and the **Herdr status** key onto keys of your keypad.

Needs macOS, Logi Options+ with Logi Plugin Service 6.4 or later, herdr, and iTerm2 (configurable). The script installs the .NET 10 SDK in `~/.dotnet` when it is missing. The editor action and the chords call helper scripts in `~/.local/bin` (`hagent`, `hcleanup`, `hproject`, `hopen`); without them, those keys do nothing.

## Configure

The ticket page's actions live in `config/keypad.json`, which the plugin reads again on every save:

```json
{ "label": "go", "type": "prompt", "value": "go", "color": "#f36a1b", "when": ["go?"] }
```

## More

- **[How it works](docs/flows.md)**: every flow, with diagrams.
- **[Setup details](docs/setup.md)**: all config fields, the herdr chords, developing the plugin and uninstalling it.
