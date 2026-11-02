#!/bin/bash
# Collect from an existing installation. No game rebuild or installation changes.
set -euo pipefail
if [[ "$(uname -s)" != Darwin ]]; then
    echo 'This collector requires macOS.' >&2
    exit 1
fi
if [[ $# -gt 1 || "${1:-}" == --help ]]; then
    echo 'Usage: bash Collect-MacWindowDiagnostics.sh [/path/to/Memoria.FF8.app-or-executable]'
    exit 0
fi
tool_dir="$(cd "$(dirname "$0")" && pwd)"
target="${1:-}"
if [[ -z "$target" ]]; then
    read -r -p 'Enter the path to Memoria.FF8.app (or its launcher/game executable), without shell quotes: ' target
fi
target="${target%/}"
if [[ -d "$target/Contents/MacOS" ]]; then
    install_root="$target/Contents/Resources"
    executable="$target/Contents/MacOS/Memoria.Launcher"
else
    executable="$target"
    install_root="$(dirname "$target")"
fi
if [[ ! -f "$executable" || ! -x "$executable" ]]; then
    echo 'Selected launcher/game executable is missing or is not executable.' >&2
    exit 1
fi
executable="$(cd "$(dirname "$executable")" && pwd)/$(basename "$executable")"
install_root="$(cd "$install_root" && pwd)"
temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/memoria-window.XXXXXX")"
trap 'rm -rf -- "$temp_dir"' EXIT
mkdir "$temp_dir/report" "$temp_dir/host"
echo 'Compiling the small macOS diagnostics helper (requires Apple Command Line Tools).'
if ! xcrun swiftc "$tool_dir/Collect-MacWindowDiagnostics.swift" -o "$temp_dir/collector"; then
    echo 'Cannot compile diagnostics helper. Install Apple Command Line Tools with xcode-select --install.' >&2
    exit 1
fi
echo 'Close other Memoria game/launcher instances before continuing.'
echo 'Only display geometry, selected runtime hashes, filtered graphics preferences and bounded diagnostic logs are collected.'
echo 'No saves or game assets are collected. Review the resulting archive before sharing it.'
read -r -p 'Include only game-window screenshots? This may request macOS Screen Recording permission. [y/N] ' answer
screenshots=no
[[ "$answer" == y || "$answer" == Y ]] && screenshots=yes
export MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY="$temp_dir/host"
export MEMORIA_FF8_HOST_READY_FILE="$temp_dir/host/direct-ready.json"
export MVK_CONFIG_LOG_LEVEL=3
# Run directly rather than via Launch Services so diagnostics environment reaches children.
"$executable" > >("$temp_dir/collector" stream "$temp_dir/report/launcher-stdout.log") 2> >("$temp_dir/collector" stream "$temp_dir/report/launcher-stderr.log") &
launch_pid=$!
"$temp_dir/collector" inventory "$temp_dir/report" "$install_root" "$executable"
for phase in title-cropped resized borderless; do
    case "$phase" in
        title-cropped) echo 'Start the game in the launcher; leave the initially cropped title window unchanged.' ;;
        resized) echo 'Manually resize the game window and allow a few frames to render.' ;;
        borderless) echo 'Switch to borderless mode in the game and allow a few frames to render.' ;;
    esac
    read -r -p "Press Enter to record $phase (or type skip): " answer
    if [[ "$answer" == skip ]]; then
        printf '%s skipped by user\n' "$phase" >> "$temp_dir/report/missing.txt"
        continue
    fi
    "$temp_dir/collector" phase "$temp_dir/report" "$temp_dir/host" "$phase" "$screenshots" "$launch_pid"
done
echo 'Quit the game and launcher normally so diagnostic logs finish writing.'
read -r -p 'Press Enter after both have closed: ' answer
if kill -0 "$launch_pid" 2>/dev/null; then
    echo 'Launcher is still running. Close it to complete collection.'
    wait "$launch_pid" || true
fi
"$temp_dir/collector" finish "$temp_dir/report" "$temp_dir/host"
archive="$PWD/memoria-window-diagnostics-$(date -u +%Y%m%dT%H%M%SZ)-$$.zip"
/usr/bin/ditto -c -k --keepParent "$temp_dir/report" "$archive"
echo "Created: $archive"
echo 'Check report/missing.txt and review all included files before sharing.'
