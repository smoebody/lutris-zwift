#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export WINEPREFIX="${WINEPREFIX:-$SCRIPT_DIR}"
export WINEDEBUG="${WINEDEBUG:-fixme-all}"

WINE="${WINE:-${XDG_DATA_HOME:-$HOME/.local/share}/lutris/runners/wine/dw-proton-x86_64/files/bin/wine}"
WINESERVER="${WINESERVER:-$(dirname -- "$WINE")/wineserver}"
INSTALL_DIR="$WINEPREFIX/drive_c/Program Files (x86)/Zwift"
WINDOWS_INSTALL_DIR='C:\Program Files (x86)\Zwift'
USER_PROFILE="$WINEPREFIX/drive_c/users/$(id -un)"

# In Praefixen, die unter einem anderen Benutzernamen erstellt wurden, legt
# Wine nicht alle Standardordner an. ZwiftApp.exe beendet sich ohne sie.
mkdir -p "$USER_PROFILE"/{Contacts,Desktop,Documents,Downloads,Favorites,Links,Music,Pictures/Screenshots,'Saved Games',Searches,Videos}

wine_task_info() {
    # Die Auswertung erwartet die englische Ausgabe "PID:". Ohne LC_ALL=C wird
    # sie in einer deutschen Locale lokalisiert und die PID fehlt.
    LC_ALL=C "$WINE" tasklist /fo list /fi "IMAGENAME eq $1"
}

wine_task_pid() {
    wine_task_info "$1" | grep -m1 -Po '^PID:[\t ]*\K[0-9]+'
}

wait_for_task() {
    local task="$1"
    local attempts=100

    while ! wine_task_pid "$task" >/dev/null 2>&1; do
        if (( attempts-- <= 0 )); then
            printf 'Zeitueberschreitung beim Warten auf %s.\n' "$task" >&2
            return 1
        fi
        sleep 0.1
    done
}

cleanup() {
    "$WINESERVER" -k >/dev/null 2>&1 || true
}
trap cleanup EXIT

cd "$INSTALL_DIR"
printf 'Starte ZwiftLauncher.exe im Hintergrund ...\n'
"$WINE" start /d "$WINDOWS_INSTALL_DIR" ZwiftLauncher.exe SilentLaunch
wait_for_task ZwiftLauncher.exe
launcher_pid="$(wine_task_pid ZwiftLauncher.exe)"
printf 'Launcher-Windows-PID: %s\n' "$launcher_pid"

# Ohne den Launcher als Elternprozess startet ZwiftApp.exe den Launcher neu
# und beendet sich selbst.
printf 'Starte ZwiftApp.exe als Kindprozess des Launchers ...\n'
"$WINE" start /d "$WINDOWS_INSTALL_DIR" /wait /unix \
    "$INSTALL_DIR/RunFromProcess.exe" "$launcher_pid" ZwiftApp.exe
wait_for_task ZwiftApp.exe

"$WINE" taskkill /f /im ZwiftLauncher.exe >/dev/null 2>&1 || true
"$WINE" taskkill /f /im ZwiftWindowsCrashHandler.exe >/dev/null 2>&1 || true
printf 'Zwift wurde gestartet; der Launcher wurde beendet.\n'

while wine_task_pid ZwiftApp.exe >/dev/null 2>&1; do
    sleep 5
done
