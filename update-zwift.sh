#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export WINEPREFIX="${WINEPREFIX:-$SCRIPT_DIR}"
export WINEDEBUG="${WINEDEBUG:-fixme-all}"

WINE="${WINE:-${XDG_DATA_HOME:-$HOME/.local/share}/lutris/runners/wine/dw-proton-x86_64/files/bin/wine}"
WINESERVER="${WINESERVER:-$(dirname -- "$WINE")/wineserver}"
# dw-proton haelt vkd3d getrennt in lib/vkd3d; ohne Proton-Skript muss
# WINEDLLPATH wie dort gesetzt werden, sonst fehlt wined3d.dll libvkd3d-1.dll.
WINE_LIB_DIR="$(dirname -- "$WINE")/../lib"
if [[ -d "$WINE_LIB_DIR/vkd3d" ]]; then
    export WINEDLLPATH="$WINE_LIB_DIR/vkd3d:$WINE_LIB_DIR/wine${WINEDLLPATH:+:$WINEDLLPATH}"
fi
ZWIFT_DIR="$WINEPREFIX/drive_c/Program Files (x86)/Zwift"
WINDOWS_ZWIFT_DIR='C:\Program Files (x86)\Zwift'
VERSION_URL='https://cdn.zwift.com/gameassets/Zwift_Updates_Root/Zwift_ver_cur.xml'
started_launcher=0

get_current_version() {
    local version_file='Zwift_ver_cur.xml'
    if [[ -f "$ZWIFT_DIR/Zwift_ver_cur_filename.txt" ]]; then
        version_file="$(tr -d '\0\r' < "$ZWIFT_DIR/Zwift_ver_cur_filename.txt" | head -n 1)"
    fi
    grep -oP 'sversion="\K.*?(?=\s)' "$ZWIFT_DIR/$version_file" 2>/dev/null | cut -f 1 -d ' ' || echo 'none'
}

get_latest_version() {
    curl -fsSL "$VERSION_URL" | grep -oP 'sversion="\K.*?(?=\s)' | cut -f 1 -d ' '
}

wine_task_info() {
    LC_ALL=C "$WINE" tasklist /fo list /fi 'IMAGENAME eq ZwiftLauncher.exe'
}

launcher_is_running() {
    wine_task_info | grep -q '^PID:[[:space:]]*[0-9]'
}

cleanup() {
    if (( started_launcher )); then
        "$WINESERVER" -k >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

current_version="$(get_current_version)"
if ! latest_version="$(get_latest_version)" || [[ -z "$latest_version" ]]; then
    printf 'Die aktuelle Zwift-Version konnte nicht vom CDN gelesen werden.\n' >&2
    exit 1
fi
if [[ "$current_version" == "$latest_version" ]]; then
    printf 'Zwift ist aktuell: %s\n' "$current_version"
    exit 0
fi

printf 'Aktualisiere Zwift von %s auf %s ...\n' "$current_version" "$latest_version"
cd "$ZWIFT_DIR"
"$WINE" start /d "$WINDOWS_ZWIFT_DIR" ZwiftLauncher.exe SilentLaunch
started_launcher=1

counter=0
updated_version="$current_version"
while [[ "$updated_version" == "$current_version" ]] && (( counter < 60 )); do
    if ! launcher_is_running; then
        printf 'Der Launcher wurde vor Abschluss des Updates beendet.\n' >&2
        exit 1
    fi
    sleep 5
    updated_version="$(get_current_version)"
    counter=$((counter + 1))
done

if [[ "$updated_version" == "$current_version" ]]; then
    printf 'Zeitueberschreitung beim Zwift-Update.\n' >&2
    exit 1
fi
if [[ "$updated_version" != "$latest_version" ]]; then
    printf 'Unerwartete Zwift-Version nach dem Update: %s statt %s\n' \
        "$updated_version" "$latest_version" >&2
    exit 1
fi
printf 'Zwift wurde auf Version %s aktualisiert.\n' "$updated_version"
