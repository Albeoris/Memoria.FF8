#!/bin/sh
set -eu

# Run with sh; portable package resources intentionally retain mode 0644.
package=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd -P)
if [ "${1:-}" = '--launch' ]; then
    shift
    exec "$package/Memoria.Launcher" "$@"
fi
applications=${XDG_DATA_HOME:-"$HOME/.local/share"}/applications
case "$applications" in
    /*) ;;
    *) printf '%s\n' 'XDG_DATA_HOME must be an absolute path.' >&2; exit 1 ;;
esac

# Desktop entry strings cannot contain literal line breaks or tab characters.
if printf '%s' "$package$applications" | LC_ALL=C grep -q '[[:cntrl:]]'; then
    printf '%s\n' 'Package and desktop paths must not contain control characters.' >&2
    exit 1
fi
if [ ! -f "$package/Memoria.Launcher" ] || [ ! -f "$package/Data/Desktop/application.png" ]; then
    printf '%s\n' 'Run this installer from the complete extracted Memoria FF8 package.' >&2
    exit 1
fi

# Exec has its own quoting layer, followed by desktop-entry string escaping.
executable=$(printf '%s' "$package/Data/Desktop/install-desktop.sh" | sed 's/\\/\\\\/g; s/`/\\`/g; s/\$/\\$/g; s/"/\\"/g; s/%/%%/g; s/\\/\\\\/g')
icon=$(printf '%s' "$package/Data/Desktop/application.png" | sed 's/\\/\\\\/g')
mkdir -p -- "$applications"
destination=$applications/org.memoria.ff8.desktop
(
    umask 022
    printf '%s\n' '[Desktop Entry]' 'Type=Application' 'Name=Memoria FF8' 'Comment=Play Final Fantasy VIII with Memoria' "Exec=/bin/sh \"$executable\" --launch" "Icon=$icon" 'StartupWMClass=org.memoria.ff8' 'Terminal=false' 'Categories=Game;RolePlaying;'
) > "$destination"
chmod 644 -- "$destination"
printf 'Installed %s\n' "$destination"
