# agents.md

## Projektüberblick

Lutris-Installer für Zwift für Windows. Lutris-Runner ist `wine` (Spiel erscheint in Lutris unter „Wine“), Wine-Version `dw-proton` (`~/.local/share/lutris/runners/wine/dw-proton-x86_64`). Lutris 0.5.22 erkennt dw-proton als Proton-Build und startet alles – `create_prefix`, `wineexec`, `winetricks` und das Spiel – über umu (`~/.local/share/lutris/runtime/umu/umu-run`) mit `PROTONPATH`.

- Der Installer verwendet nur Lutris-Bordmittel (`create_prefix`, `wineexec`, `copy`) und öffentliche URLs, damit er auch auf lutris.net eingereicht werden kann.
- Abhängigkeiten installiert der Protonfix `gamefixes-umu/umu-zwift.py` aus dw-proton: `PROTON_DLL_COPY='*'` sowie `corefonts dotnet48 d3dcompiler_47 webview2`. Er läuft nur mit `GAMEID=umu-zwift` und `PROTON_VERB=waitforexitandrun`.
- `ZwiftStart.exe` (aus [ZwiftStart.cs](ZwiftStart.cs), im CI gebaut, aus dem GitHub-Release geladen) ist `game.exe`: Update, Profilordner, Vollbild-Voreinstellung, Start von `ZwiftApp.exe` als Kindprozess des Launchers.

Die Anmeldung findet ausschließlich im Spiel statt. Keine API-Tokens oder Zugangsdaten in Dateien, Befehlen oder Dokumentation aufnehmen.

## Dateien

| Datei | Aufgabe |
|---|---|
| [lutris-zwift.yml](lutris-zwift.yml) | Lutris-Installer; `files`: `ZwiftSetup.exe` (Zwift-CDN) und `ZwiftStart.exe` (GitHub-Release `release/X.Y.Z`, mit `checksum`) |
| [ZwiftStart.cs](ZwiftStart.cs) | C#-Startprogramm, .NET Framework 4, `ZwiftStart.exe [--update-only \| --no-update]`, Log `ZwiftStart.log` im Zwift-Verzeichnis |
| [readme.md](readme.md) | Installation, Ablauf, Build und Releases |
| [.gitlab-ci.yml](.gitlab-ci.yml) | GitLab-CI: `build` (Mono `mcs`), `package_staging`/`package_release` (Generic Package Registry `zwiftstart`), `release`, `pages` |
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | GitHub Actions mit derselben Logik: `build`, `staging` (wanderndes Pre-Release `staging`), `release`, `pages` |
| [public-src/](public-src/) | Pages-Vorlage für beide Plattformen; Platzhalter `@RELEASE@`, `@STAGING@`, `@COMMIT@`, `@DATE@`, `@PROJECT_URL@`, `@PROJECT_NOTE@` ersetzt der jeweilige `pages`-Job. Seite auf Englisch mit Anleitung, Speicherbedarf und Fehlerbehebung; der `pages`-Job kopiert `lutris-zwift.yml` mit auf die Seite |

Die früheren Bash-Wrapper (`install-zwift.sh`, `start-zwift.sh`, `update-zwift.sh`) und `RunFromProcess.cs` (Runner `linux`, Wine ohne umu) wurden am 7. November 2026 entfernt; sie liegen im Git-Verlauf und im Release 0.1.0.

In die Quellablage gehört nur der C#-Quelltext, keine übersetzte `.exe`.

## Pfade

| Zweck | Pfad |
|---|---|
| Quellablage | `/home/smoe/Projects/metaccount/multimedia/lutris-zwift` |
| Wine (Lutris-Runner dw-proton) | `~/.local/share/lutris/runners/wine/dw-proton-x86_64` |
| Protonfix für Zwift | `~/.local/share/lutris/runners/wine/dw-proton-x86_64/protonfixes/gamefixes-umu/umu-zwift.py` |
| Lutris-Installation (Runner wine, Lutris-Eintrag „Zwift“, ID 2) | `/home/smoe/Games/zwift-lutris-test` |
| Ältere Installation (Bash-Wrapper, ohne Lutris-Eintrag) | `/home/smoe/Games/zwift-winehq` |
| Lutris-Quelltext (0.5.22) | `/usr/lib/python3.14/site-packages/lutris/` |

**Vor jeder Installation mit `ls` prüfen, ob das Ziel existiert und was darin liegt.** Keine Installation in ein bestehendes Präfix.

## Lutris 0.5.22: Eigenheiten

- `wine.version: dw-proton` wird zu `dw-proton-x86_64` normalisiert; fehlt der Runner, lädt Lutris ihn (Version laut lutris.net-API: `dwproton-11.0-14`).
- `wineexec`-Schritte bekommen `system.env` des Skripts, aber nicht die Wine-Version des Skripts für umu: Ohne eigenes `PROTONPATH` setzt Lutris `PROTONPATH=GE-Proton`, und umu startet GE-Proton im dw-proton-Präfix („wine client error: version mismatch“). Daher stehen `PROTONPATH`, `PROTON_VERB=waitforexitandrun`, `GAMEID` und `UMU_ID` in `system.env`; `update_proton_env` überschreibt gesetzte Werte nicht.
- Ohne `PROTON_VERB` nimmt Lutris nach `create_prefix` `runinprefix`; damit überspringt Proton den Protonfix.
- `UMU_ID` bestimmt beim Spielstart die GAMEID (sonst `umu-default`).
- Dateien mit URL ab `/` werden zu `file://` und scheitern („No connection adapters“). Für lokale Tests eine Kopie des YAML mit `http://127.0.0.1:8765/ZwiftStart.exe` erzeugen und `python3 -m http.server 8765 --bind 127.0.0.1 --directory …` laufen lassen.
- `lutris -i` übergibt an eine laufende Lutris-Instanz und kehrt sofort zurück; das Log steht in `~/.cache/lutris/lutris.log`.

## Laufzeitverhalten und Invarianten

- Proton legt `drive_c/users/steamuser` und `drive_c/users/$USER` an; Windows-Programme sehen `steamuser`.
- Unter umu sehen sich Prozesse desselben Präfixes (`Process.GetProcessesByName`, Elternprozess-Trick funktionieren).
- `ZwiftSetup.exe` installiert nur den Launcher (Version `0.0.0`); das Spiel kommt über `ZwiftStart.exe --update-only` (höchstens 60 Minuten Wartezeit, `UpdateTimeoutMinutes`).
- Ohne Elternprozess-Umweg funktioniert der Start nicht: `ZwiftApp.exe` allein startet den Launcher und beendet sich; neben einem Launcher, aber nicht als sein Kindprozess, lädt es nichts.
- Der Launcher darf erst beendet werden, wenn `AppData\Local\Zwift\Logs\Log.txt` `Legacy Launcher Used` enthält; sonst endet `ZwiftApp.exe` nach etwa 5 s ohne `Log.txt`.
- Vollbild: `ZWIFT\CONFIG\FULLSCREEN` in `AppData\Local\Zwift\prefs.xml`. `ZwiftStart.exe` setzt `1` nur, wenn der Schlüssel fehlt.
- Fehlende Standardordner im Benutzerprofil lassen `ZwiftApp.exe` sofort enden; `ZwiftStart.exe` legt sie bei jedem Start an.
- Ein Präfix nicht mit verschiedenen Wine-/Proton-Versionen betreiben.

## Repositorys, CI, Pakete und Pages

- **GitLab** `multimedia/lutris-zwift` (ID 110, privat) auf `gitlab.metaccount.de`; Remote `origin` per SSH über `git.metaccount.de:10022`. Hauptrepository.
- **GitHub** `smoebody/lutris-zwift` (öffentlich): Spiegel über GitLab-Push-Mirror (ID 4, Deploy-Key, `only_protected_branches`, `keep_divergent_refs=true`). Ohne `keep_divergent_refs` löscht der Spiegel das Tag `staging`, das nur auf GitHub existiert. Nicht direkt auf GitHub committen.
- `ZwiftStart.exe` wird im CI mit Mono `mcs -platform:x64` übersetzt und auf PE32+/x86-64 geprüft. Die Übersetzung ist nicht reproduzierbar (auch nicht mit `-deterministic`); maßgeblich für den Installer ist die Datei aus dem GitHub-Release.
- Release-Tags `release/X.Y.Z` lösen auf beiden Plattformen ein Release aus. Danach URL und `checksum` in `lutris-zwift.yml` anpassen.
- GitLab Pages (`https://multimedia.pages.metaccount.de/lutris-zwift`) nur nach Anmeldung; GitHub Pages (`https://smoebody.github.io/lutris-zwift/`) öffentlich, Quelle „GitHub Actions“.
- Beide `pages`-Jobs laden Staging und das höchste `release/*`-Tag; Releases ohne `ZwiftStart.exe` (0.1.0) werden übersprungen.

## Commits

- Commits und Tags werden mit dem SSH-Schlüssel `smoe@metaccount.de` signiert; `user.email` ist im Repository auf `smoe@metaccount.de` gesetzt.
- Von einem LLM unterstützte Commits erhalten den Trailer `Assisted-by: <Modell>`, z. B. `git commit --trailer "Assisted-by: Claude Opus 5.5 (Anthropic)"`.
- `main` ist auf GitLab gegen Force-Push geschützt.

## Sicherheitsregeln bei Änderungen

- Keine Installation in bestehende Präfixe (`zwift-lutris-test`, `zwift-winehq`, `mywhoosh`). Vorher `ls`.
- Keine Präfixe, Registrydateien, Windows-Programmdateien, übersetzte `.exe`-Dateien, Logs oder Zugangsdaten in die Quellablage.
- Den Lutris-Runner dw-proton nicht löschen oder ersetzen, solange eine Installation ihn verwendet.
- Keine vollständige Installation als reinen Syntaxtest; sie lädt mehrere GB.

## Prüfbefehle

```bash
cd /home/smoe/Projects/metaccount/multimedia/lutris-zwift
python3 -c "import yaml; [yaml.safe_load(open(f)) for f in ('lutris-zwift.yml', '.gitlab-ci.yml', '.github/workflows/ci.yml')]; print('YAML ok')"
mkdir -p /tmp/opencode/zs
podman run --rm -v "$PWD":/src:ro,Z -v /tmp/opencode/zs:/out:Z docker.io/library/mono:6.12 \
    mcs -platform:x64 -optimize+ -target:exe -out:/out/ZwiftStart.exe /src/ZwiftStart.cs
```

`ZwiftStart.exe` in einem bestehenden Präfix so starten wie Lutris (Ausgabe nur in `ZwiftStart.log`):

```bash
P=/pfad/zum/praefix
WINEPREFIX=$P PROTONPATH=~/.local/share/lutris/runners/wine/dw-proton-x86_64 GAMEID=umu-zwift \
WINEARCH=win64 PROTON_VERB=waitforexitandrun ~/.local/share/lutris/runtime/umu/umu-run \
    "$P/drive_c/Program Files (x86)/Zwift/ZwiftStart.exe"
```

Nach einer Änderung am Startablauf ist ein realer Start bis zum Anmeldefenster zu testen; in `drive_c/users/steamuser/AppData/Local/Zwift/Logs/Log.txt` müssen `Legacy Launcher Used` und `GameFlowState Login` erscheinen.

## lutris.net

Zwift hat dort bereits eine Spielseite (`zwift`, ID 30983) mit zwei Installern (`zwift-1301` von tmiland, `zwift-windows`), beide mit nirsoft RunFromProcess. Einreichen:

1. Konto auf lutris.net anlegen und anmelden.
2. Auf <https://lutris.net/games/zwift/> „Add installer“ wählen. Den Inhalt von `script:` aus `lutris-zwift.yml` einfügen (ohne `name`, `game_slug`, `slug`, `runner` – diese Felder setzt das Formular). Dazu Runner `wine`, Version (z. B. „dw-proton“), Beschreibung und Notes ausfüllen.
3. Moderatoren prüfen den Entwurf; erst danach ist er für alle sichtbar. Änderungen gehen als neue Revision wieder durch die Prüfung.

Voraussetzungen: nur öffentliche URLs mit `checksum` (außer `ZwiftSetup.exe`, das sich ändert), keine lokalen Pfade. `$HOME` in `PROTONPATH` ist an den Lutris-Runner-Pfad gebunden; bei Flatpak-Lutris ist dieser anders. Flatpak-Lutris ist daher als nicht unterstützt dokumentiert.

## Getesteter Stand

7. November 2026, Fedora 44, Lutris 0.5.22 (RPM), dw-proton `dwproton-11.0-14`, umu 1.4.4, Zwift `1.125.0`:

- Vollinstallation über `lutris-zwift.yml` (ZwiftStart.exe vom lokalen Testserver) nach `/home/smoe/Games/zwift-lutris-test`: alle Schritte mit Code 0, Lutris-Eintrag mit Runner `wine`.
- Start aus Lutris: `Legacy Launcher Used` und `GameFlowState Login`, Vollbild ohne Titelleiste (vom Benutzer bestätigt).
