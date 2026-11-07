#!/usr/bin/env bash
set -euo pipefail

TARGET_DIR="${1:?Zielverzeichnis fehlt}"
START_WRAPPER="${2:?Pfad zum Startwrapper fehlt}"
UPDATE_WRAPPER="${3:?Pfad zum Updatewrapper fehlt}"
RUNFROMPROCESS_SOURCE="${4:?Pfad zu RunFromProcess.cs fehlt}"

# Wine-Build aus dem Lutris-Runner "dw-proton"; fehlt er, wird dieselbe
# Version geladen, die Lutris anbietet. Ohne umu, damit Prozesse eines
# Praefixes sich weiter gegenseitig sehen.
DWPROTON_VERSION='dwproton-11.0-14'
DWPROTON_SHA256='c563cc99d464fb19a767a0eb90dc723746bfcdd402a4fdaa0737b12af081f99a'
DWPROTON_URL="https://github.com/dawn-winery/dwproton-mirror/releases/download/$DWPROTON_VERSION/$DWPROTON_VERSION-x86_64.tar.xz"
LUTRIS_WINE_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/lutris/runners/wine/dw-proton-x86_64"

export WINE="${WINE:-$LUTRIS_WINE_DIR/files/bin/wine}"
export WINESERVER="${WINESERVER:-$(dirname -- "$WINE")/wineserver}"
export WINEDEBUG="${WINEDEBUG:-fixme-all}"
WINE_DATA_DIR="$(dirname -- "$WINE")/../share/wine"

# Gleicher Stand wie im Netbrain-Dockerfile: enthaelt das Verb webview2.
WINETRICKS_VERSION='f3890f670867b5ffbc3938726db45c0f7d16c8ba'
WINETRICKS_SHA256='672a1ff4442e8691a3ffc0e6860137e201a1d1227e9b3044245f1731e9e9837e'
WINETRICKS_URL="https://raw.githubusercontent.com/Winetricks/winetricks/$WINETRICKS_VERSION/src/winetricks"
ZWIFT_SETUP_URL='https://cdn.zwift.com/app/ZwiftSetup.exe'

if [[ "$TARGET_DIR" != /* || "$TARGET_DIR" == / || "$TARGET_DIR" == "$HOME" ]]; then
    printf 'Unsicheres Zielverzeichnis: %s\n' "$TARGET_DIR" >&2
    exit 1
fi
if [[ -d "$TARGET_DIR" ]] && [[ -n "$(find "$TARGET_DIR" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
    printf 'Das Zielverzeichnis ist nicht leer: %s\n' "$TARGET_DIR" >&2
    printf 'Zum Schutz einer bestehenden Installation wird abgebrochen.\n' >&2
    exit 1
fi
for command_name in curl sha256sum tar xz cabextract unzip ntlm_auth; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        printf 'Benötigtes Programm fehlt: %s\n' "$command_name" >&2
        exit 1
    fi
done
for wrapper in "$START_WRAPPER" "$UPDATE_WRAPPER" "$RUNFROMPROCESS_SOURCE"; do
    if [[ ! -f "$wrapper" ]]; then
        printf 'Quelldatei nicht gefunden: %s\n' "$wrapper" >&2
        exit 1
    fi
done

WORK_DIR="$(mktemp -d)"
cleanup() {
    "$WINESERVER" -k >/dev/null 2>&1 || true
    rm -rf -- "$WORK_DIR"
}
trap cleanup EXIT

if [[ ! -x "$WINE" && "$WINE" == "$LUTRIS_WINE_DIR/files/bin/wine" ]]; then
    printf 'Lade %s fuer den Lutris-Runner dw-proton ...\n' "$DWPROTON_VERSION"
    curl -fsSL -o "$WORK_DIR/dwproton.tar.xz" "$DWPROTON_URL"
    printf '%s  %s\n' "$DWPROTON_SHA256" "$WORK_DIR/dwproton.tar.xz" | sha256sum -c --quiet -
    mkdir -p "$LUTRIS_WINE_DIR"
    tar -xJf "$WORK_DIR/dwproton.tar.xz" -C "$LUTRIS_WINE_DIR" --strip-components=1
    rm -f -- "$WORK_DIR/dwproton.tar.xz"
fi
for command_name in "$WINE" "$WINESERVER"; do
    if [[ ! -x "$command_name" ]]; then
        printf 'Wine-Programm fehlt: %s\n' "$command_name" >&2
        exit 1
    fi
done
# Ohne diese Daten haengt wineboot endlos (z. B. Fedora ohne wine-common).
for wine_data in "$WINE_DATA_DIR/wine.inf" "$WINE_DATA_DIR/winmd"; do
    if [[ ! -e "$wine_data" ]]; then
        printf 'Wine-Daten fehlen: %s\n' "$wine_data" >&2
        exit 1
    fi
done
printf 'Verwende %s\n' "$("$WINE" --version)"

printf 'Lade winetricks %s ...\n' "$WINETRICKS_VERSION"
curl -fsSL -o "$WORK_DIR/winetricks" "$WINETRICKS_URL"
printf '%s  %s\n' "$WINETRICKS_SHA256" "$WORK_DIR/winetricks" | sha256sum -c --quiet -
chmod u+x "$WORK_DIR/winetricks"

mkdir -p "$TARGET_DIR"
export WINEPREFIX="$TARGET_DIR"

# Mono und Gecko werden nicht benoetigt; dotnet48 und WebView2 ersetzen sie.
printf 'Initialisiere das Wine-Praefix ...\n'
WINEDLLOVERRIDES='mscoree,mshtml=' "$WINE" wineboot -u
"$WINESERVER" -w

printf 'Installiere corefonts, dotnet48, d3dcompiler_47 und webview2 ...\n'
"$WORK_DIR/winetricks" -q corefonts dotnet48 d3dcompiler_47 webview2

# Videostrecken benoetigen ein D3D11-Geraet mit Videounterstuetzung; das
# liefert Wine nur mit dem Vulkan-Renderer.
"$WORK_DIR/winetricks" -q renderer=vulkan

printf 'Lade und installiere Zwift ...\n'
curl -fsSL -o "$WORK_DIR/ZwiftSetup.exe" "$ZWIFT_SETUP_URL"
"$WINE" "$WORK_DIR/ZwiftSetup.exe" /SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL
"$WINESERVER" -k >/dev/null 2>&1 || true

# ZwiftApp.exe muss als Kindprozess des Launchers laufen. RunFromProcess.exe
# erledigt das und wird mit dem csc.exe aus dotnet48 im Praefix uebersetzt.
ZWIFT_DIR="$TARGET_DIR/drive_c/Program Files (x86)/Zwift"
WINDOWS_ZWIFT_DIR='C:\Program Files (x86)\Zwift'
printf 'Uebersetze RunFromProcess.exe ...\n'
cp "$RUNFROMPROCESS_SOURCE" "$ZWIFT_DIR/RunFromProcess.cs"
"$WINE" 'C:\windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /optimize+ /platform:x64 \
    /target:exe "/out:$WINDOWS_ZWIFT_DIR\\RunFromProcess.exe" "$WINDOWS_ZWIFT_DIR\\RunFromProcess.cs"
rm -f -- "$ZWIFT_DIR/RunFromProcess.cs"

cp "$START_WRAPPER" "$TARGET_DIR/start-zwift.sh"
cp "$UPDATE_WRAPPER" "$TARGET_DIR/update-zwift.sh"
chmod u+x "$TARGET_DIR/start-zwift.sh" "$TARGET_DIR/update-zwift.sh"

# ZwiftSetup.exe installiert nur den Launcher; das Spiel laedt erst das Update.
"$TARGET_DIR/update-zwift.sh"

for required_file in ZwiftLauncher.exe ZwiftApp.exe RunFromProcess.exe; do
    if [[ ! -s "$ZWIFT_DIR/$required_file" ]]; then
        printf 'Erforderliche Datei fehlt oder ist leer: %s\n' "$ZWIFT_DIR/$required_file" >&2
        exit 1
    fi
done
printf 'Installation abgeschlossen: %s\n' "$TARGET_DIR"
