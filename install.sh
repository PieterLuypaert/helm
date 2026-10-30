#!/usr/bin/env bash
# Sets up the keypad on this Mac: builds the herdr plugin and links it into Logi Options+,
# links the action config, and writes the keypad chords into herdr's config.
# Safe to run again; run it after every pull.
set -euo pipefail

repo=$(cd "$(dirname "$0")" && pwd)
dotnet_dir="$HOME/.dotnet"
herdr_config=${HERDR_CONFIG_PATH:-$HOME/.config/herdr/config.toml}
action_config="$HOME/.config/herdr-keypad/config.json"

step() { printf '\n==> %s\n' "$1"; }

step ".NET 10 SDK in $dotnet_dir"
if "$dotnet_dir/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "present"
else
  tmp=$(mktemp -d)
  trap 'rm -rf "$tmp"' EXIT
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tmp/dotnet-install.sh"
  bash "$tmp/dotnet-install.sh" --channel 10.0 --install-dir "$dotnet_dir"
fi
export DOTNET_ROOT="$dotnet_dir" PATH="$dotnet_dir:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step "Herdr plugin for Logi Options+"
if [ ! -d /Applications/Utilities/LogiPluginService.app ]; then
  echo "warning: Logi Plugin Service not found. Install Logi Options+ first, then run this again." >&2
fi
mkdir -p "$HOME/Library/Application Support/Logi/LogiPluginService/Plugins"
# The build links bin/ into the Logi Plugin Service and asks it to reload the plugin.
dotnet build "$repo/plugin/src/HerdrPlugin.csproj" -nologo -v minimal | grep -E "error|warning CS|Created plugin link|reload" || true
[ -f "$repo/plugin/bin/Debug/bin/HerdrPlugin.dll" ] || { echo "build failed" >&2; exit 1; }

step "Action config at $action_config"
mkdir -p "$(dirname "$action_config")"
if [ -e "$action_config" ] && [ ! -L "$action_config" ]; then
  mv "$action_config" "$action_config.bak-$(date +%Y%m%d-%H%M%S)"
  echo "moved your own config aside"
fi
ln -sfn "$repo/config/keypad.json" "$action_config"
echo "linked to config/keypad.json"

step "Keypad chords in $herdr_config"
if [ ! -f "$herdr_config" ]; then
  echo "no herdr config found; skipped"
else
  python3 - "$herdr_config" "$repo/herdr/keys.toml" <<'PY'
import datetime, re, shutil, sys
config_path, block_path = sys.argv[1], sys.argv[2]
begin, end = "# >>> helm: keypad chords", "# <<< helm: keypad chords"
config = open(config_path, encoding="utf-8").read()
block = f"{begin}\n{open(block_path, encoding='utf-8').read().rstrip()}\n{end}\n"
pattern = re.compile(re.escape(begin) + r".*?" + re.escape(end) + r"\n?", re.S)
if pattern.search(config):
    updated = pattern.sub(lambda _: block, config, count=1)
else:
    updated = config.rstrip("\n") + "\n\n" + block
if updated == config:
    print("up to date")
else:
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    shutil.copy2(config_path, f"{config_path}.bak-{stamp}")
    open(config_path, "w", encoding="utf-8").write(updated)
    print("written (backup beside it)")
PY
  if command -v herdr >/dev/null 2>&1 || [ -x "$HOME/.local/bin/herdr" ]; then
    "$(command -v herdr || echo "$HOME/.local/bin/herdr")" server reload-config >/dev/null 2>&1 \
      && echo "herdr reloaded" || echo "herdr not running; it reads the chords on its next start"
  fi
fi

step "Done"
echo "In Logi Options+, put the 'Herdr tickets' folder (Installed Plugins > Herdr) on a keypad key."
