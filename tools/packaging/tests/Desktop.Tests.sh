#!/bin/sh
set -eu
source_directory=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd -P)
temporary=$(mktemp -d)
trap 'rm -rf -- "$temporary"' EXIT HUP INT TERM

# Exercise both desktop-entry string escaping and Exec argument quoting.
package="$temporary/portable space \"quote\" \$dollar \`tick\` %percent \\slash"
mkdir -p -- "$package/Data/Desktop" "$temporary/menu data"
cp -- "$source_directory/install-desktop.sh" "$package/Data/Desktop/install-desktop.sh"
printf '%s\n' '#!/bin/sh' 'printf "%s\n" "$0" > "$MEMORIA_DESKTOP_TEST_OUTPUT"' > "$package/Memoria.Launcher"
chmod 755 -- "$package/Memoria.Launcher"
printf '%s\n' 'synthetic icon fixture' > "$package/Data/Desktop/application.png"
export XDG_DATA_HOME="$temporary/menu data"
export MEMORIA_DESKTOP_TEST_OUTPUT="$temporary/launched"
sh "$package/Data/Desktop/install-desktop.sh"
desktop="$XDG_DATA_HOME/applications/org.memoria.ff8.desktop"
if command -v desktop-file-validate >/dev/null 2>&1; then
    desktop-file-validate "$desktop"
fi
gio launch "$desktop"
attempt=0
while [ ! -f "$MEMORIA_DESKTOP_TEST_OUTPUT" ] && [ "$attempt" -lt 50 ]; do
    sleep 0.1
    attempt=$((attempt + 1))
done
if [ "$(cat -- "$MEMORIA_DESKTOP_TEST_OUTPUT")" != "$package/Memoria.Launcher" ]; then
    printf '%s\n' 'The desktop entry did not launch the selected portable package.' >&2
    exit 1
fi
sh "$package/Data/Desktop/install-desktop.sh"
if [ "$(find "$XDG_DATA_HOME/applications" -type f | wc -l)" -ne 1 ]; then
    printf '%s\n' 'Registration must replace the same desktop entry.' >&2
    exit 1
fi
if XDG_DATA_HOME=relative sh "$package/Data/Desktop/install-desktop.sh"; then
    printf '%s\n' 'Relative XDG_DATA_HOME must be rejected.' >&2
    exit 1
fi
rm -- "$package/Memoria.Launcher"
if sh "$package/Data/Desktop/install-desktop.sh"; then
    printf '%s\n' 'An incomplete package must be rejected.' >&2
    exit 1
fi
printf '%s\n' 'Linux desktop registration checks passed.'
