# agents.md

## Projektüberblick

Quellablage und Dokumentation für Zwift für Windows unter Lutris. Als Wine dient der Lutris-Wine-Runner `dw-proton` (`~/.local/share/lutris/runners/wine/dw-proton-x86_64`); ein System-Wine ist nicht nötig. Der Lutris-Runner `linux` ist absichtlich gewählt: Er führt den Bash-Startwrapper aus, der wiederum `files/bin/wine` aus dw-proton aufruft. Das Wine-Präfix baut der Installer selbst auf; ein Container-Image (früher `docker.io/netbrain/zwift:latest`) ist nicht nötig.

Die Lösung besteht aus drei getrennten Schritten:

1. `install-zwift.sh` lädt bei Bedarf dw-proton, legt ein neues Präfix an, installiert per winetricks `corefonts dotnet48 d3dcompiler_47 webview2` sowie `renderer=vulkan`, installiert `ZwiftSetup.exe`, übersetzt `RunFromProcess.cs` mit dem `csc.exe` aus dotnet48 und lädt über `update-zwift.sh` das Spiel.
2. `update-zwift.sh` prüft vor dem Spielstart die CDN-Version und führt bei Bedarf `ZwiftLauncher.exe SilentLaunch` aus.
3. `start-zwift.sh` startet `ZwiftApp.exe` über `RunFromProcess.exe` als Kindprozess des Launchers und beendet danach Launcher und Crash-Handler.

Die Anmeldung findet ausschließlich im Spiel statt. Keine API-Tokens oder Zugangsdaten in Dateien, Befehlen oder Dokumentation aufnehmen.

## Dateien

| Datei | Aufgabe |
|---|---|
| [zwift-lutris.yml](zwift-lutris.yml) | Lutris-Installer; ruft `install-zwift.sh` mit absoluten Pfaden unter `/home/smoe/Projects/metaccount/multimedia/zwift` direkt auf. Keine `files`-Einträge: Lutris 0.5.22 lädt `file://`-URLs mit `requests` und scheitert mit „No connection adapters were found“. `game.prefix: $GAMEDIR` ist nötig, damit Lutris ohne `files` überhaupt ein Zielverzeichnis abfragt und `$GAMEDIR` ersetzt |
| [install-zwift.sh](install-zwift.sh) | Neuinstallation in ein leeres absolutes Ziel; verweigert `/`, `$HOME` und nicht leere Ziele. Argumente: Ziel, Startwrapper, Updatewrapper, `RunFromProcess.cs` |
| [start-zwift.sh](start-zwift.sh) | Profilordner anlegen, Launcher-PID ermitteln, Spiel über `RunFromProcess.exe` starten und auf dessen Ende warten |
| [update-zwift.sh](update-zwift.sh) | Lokale/CDN-Version vergleichen, höchstens 60 × 5 Sekunden auf ein Update warten |
| [RunFromProcess.cs](RunFromProcess.cs) | C#-Ersatz für `runfromprocess-rs`: `RunFromProcess.exe <ppid> <kommandozeile>` startet ein Programm mit `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS` |
| [readme.md](readme.md) | Voraussetzungen, Installation, Start, Updateverhalten, Anmeldung, Build und Releases |
| [.gitlab-ci.yml](.gitlab-ci.yml) | GitLab-CI: `build` (Mono `mcs`), `package_staging`/`package_release` (Generic Package Registry `runfromprocess`), `release`, `pages` |
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | GitHub Actions mit derselben Logik: `build`, `staging` (wanderndes Pre-Release `staging`), `release`, `pages` |
| [public-src/](public-src/) | Pages-Vorlage für beide Plattformen; Platzhalter `@RELEASE@`, `@STAGING@`, `@COMMIT@`, `@DATE@`, `@PROJECT_URL@`, `@PROJECT_NOTE@` ersetzt der jeweilige `pages`-Job |

Die Skripte verwenden Bash mit `set -euo pipefail` und vier Spaces Einrückung. Bestehende ASCII-Schreibweise in Skriptmeldungen und Kommentaren beibehalten. In die Quellablage gehört nur der C#-Quelltext, keine übersetzte `.exe`.

## Pfade

| Zweck | Pfad |
|---|---|
| Quellablage | `/home/smoe/Projects/metaccount/multimedia/zwift` |
| Wine (Lutris-Runner dw-proton) | `~/.local/share/lutris/runners/wine/dw-proton-x86_64/files/bin/wine` |
| Aktuelle Installation (mit dw-proton, 7. November 2026) | `/home/smoe/Games/zwift-winehq` |
| Ursprüngliche Quellkopien (Netbrain-Variante) | `/home/smoe/zwift-lutris` |
| Ursprünglicher Lutris-Installer | `/home/smoe/zwift-lutris.yml` |

Die früheren Präfixe (`zwift`, `zwift-winehq` aus dem Netbrain-Image, `zwift-winehq-test`, `zwift-claude`) und der alte Lutris-Eintrag wurden vom Benutzer am 6./7. November 2026 gelöscht (Papierkorb). Für die aktuelle Installation existiert noch kein Lutris-Eintrag.

**Vor jedem Aufruf des Installers prüfen, ob das Ziel existiert.** Der Installer legt ein fehlendes Ziel an und installiert hinein; die Schutzprüfung greift nur bei nicht leeren Verzeichnissen. Ein Aufruf mit einem Pfad, unter dem man eine Installation nur vermutet, startet eine vollständige Installation.

Beim Verschieben dieses Projektverzeichnisses müssen die vier absoluten Pfade in `installer[0].execute.args` von `zwift-lutris.yml` sowie die dokumentierten Beispielpfade angepasst werden. Die Wrapper selbst bestimmen ihr Verzeichnis dynamisch.

## Repositorys, CI, Pakete und Pages

- **GitLab** `multimedia/zwift` (ID 110, privat) auf `gitlab.metaccount.de`; Remote `origin` per SSH über `git.metaccount.de:10022`. Hauptrepository.
- **GitHub** `smoebody/lutris-zwift` (öffentlich): Spiegel, befüllt vom GitLab-Push-Mirror über einen Deploy-Key mit Schreibrecht. Nicht direkt auf GitHub committen; Änderungen dort überschreibt der Spiegel.
- Es gibt nur Linux-Runner. `RunFromProcess.exe` wird im CI mit Mono `mcs -platform:x64` (Image `mono:6.12`) übersetzt und auf PE32+/x86-64 geprüft; der Installer übersetzt weiterhin selbst mit `csc.exe` im Präfix und lädt nichts aus dem CI.
- Release-Tags folgen `release/X.Y.Z`; durch den Spiegel löst ein Tag auf beiden Plattformen ein Release aus.
- Push auf `main`: GitLab überschreibt die Paketversion `staging`, GitHub verschiebt das Pre-Release/Tag `staging` und ersetzt dessen Dateien.
- GitLab Pages ist pfadbasiert (`https://multimedia.pages.metaccount.de/zwift`) und nur nach Anmeldung erreichbar: Bei diesem privaten Projekt lässt sich die Pages-Sichtbarkeit nicht auf `public` stellen („public access is disabled“).
- GitHub Pages (`https://smoebody.github.io/lutris-zwift/`) ist öffentlich; Quelle muss in den Repository-Einstellungen auf „GitHub Actions“ stehen.
- Beide `pages`-Jobs bauen die Seite bei jedem Lauf neu und laden Staging sowie das höchste `release/*`-Tag (`sort -V`) aus Paket-Registry beziehungsweise GitHub-Releases. Auf den Seiten liegt nur das jeweils neueste Release.

## Commits

- Commits und Tags werden mit dem SSH-Schlüssel `smoe@metaccount.de` aus dem Agenten signiert (`~/.gitconfig`: `gpg.format ssh`, `user.signingkey ~/.ssh/id-smoe.pub`). Der Schlüssel ist in GitLab und GitHub als Signaturschlüssel hinterlegt.
- Von einem LLM unterstützte Commits erhalten den Trailer `Assisted-by: <Modell>`, z. B. `git commit --trailer "Assisted-by: Claude Opus 5.5 (Anthropic)"`.
- `main` ist auf GitLab gegen Force-Push geschützt.

## Laufzeitverhalten und Invarianten

- Standardpräfix ist bei beiden Wrappern das jeweilige Skriptverzeichnis; ein vorhandenes `WINEPREFIX` hat Vorrang.
- `WINE` und `WINESERVER` können über Umgebungsvariablen überschrieben werden. Standard ist dw-proton unter `${XDG_DATA_HOME:-~/.local/share}/lutris/runners/wine/dw-proton-x86_64/files/bin/`; `WINESERVER` liegt standardmäßig neben `WINE`.
- Der Installer lädt dw-proton nur, wenn `WINE` nicht gesetzt ist und der Runner fehlt: feste Version `dwproton-11.0-14` von derselben URL wie Lutris, SHA-256 gegen die von GitHub veröffentlichte Prüfsumme geprüft, entpackt mit `--strip-components=1` wie Lutris. Bei einer neuen dw-proton-Version in Lutris Version und Prüfsumme gemeinsam anpassen.
- dw-proton wird **ohne umu/Proton-Skript** direkt über `files/bin/wine` verwendet. Nur so sehen sich die Prozesse eines Präfixes für `tasklist`, `start /unix` und den Elternprozess-Trick von `RunFromProcess.exe`.
- Der Installer prüft vor `wineboot`, dass `wine.inf` und `winmd` unter `files/share/wine` vorhanden sind; ohne sie hängt `wineboot` endlos.
- `WINEPREFIX/drive_c/Program Files (x86)/Zwift` ist die erwartete Windows-Installationsstruktur.
- `ZwiftSetup.exe` installiert nur den Launcher (`Zwift_ver_cur.xml` mit Version `0.0.0`). Das Spiel selbst kommt erst über `update-zwift.sh`, das der Installer am Ende aufruft.
- Ohne Elternprozess-Umweg funktioniert der Start nicht (getestet am 6. November 2026): `ZwiftApp.exe` allein startet den Launcher und beendet sich; neben einem laufenden Launcher, aber nicht als sein Kindprozess, lädt es nichts und schreibt kein `Log.txt`.
- `RunFromProcess.exe` wird mit `C:\windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /platform:x64` übersetzt und benötigt daher dotnet48 im Präfix. `start-zwift.sh` ruft es mit `wine start /wait` auf; es beendet sich direkt nach `CreateProcess`.
- Der Installer setzt `WINEDLLPATH=files/lib/vkd3d:files/lib/wine` vor `wineboot`, wie das Proton-Skript. Nur dann kopiert Wine `libvkd3d-*.dll` ins Präfix; ohne sie lädt `wined3d.dll` und damit `d3d9.dll` nicht, und der Launcher (`Patcher.dll`) beendet sich vor dem Update. Die Wrapper setzen die Variable ebenfalls.
- Das Windows-Benutzerprofil heißt bei dw-proton immer `steamuser`, bei anderem Wine wie der Linux-Benutzer. Der Startwrapper nimmt `steamuser`, sobald es existiert, und legt dort die vollständigen Standardprofilordner an. Diese Ordner nicht entfernen: Fehlen sie, beendet sich `ZwiftApp.exe` sofort.
- Alle `tasklist`-Aufrufe zur PID-/Prozesserkennung benötigen `LC_ALL=C`; die Auswertung erwartet englische Schlüssel wie `PID:`. Auf `Image Name:` oder `PID:` prüfen, nicht auf `Name:`.
- `start-zwift.sh` beendet nach erfolgreichem Spielstart nur `ZwiftLauncher.exe` und `ZwiftWindowsCrashHandler.exe`. Der `EXIT`-Trap ruft abschließend `wineserver -k` für das ausgewählte Präfix auf.
- `update-zwift.sh` bricht ab, wenn die CDN-Version nicht gelesen werden kann oder leer ist, und beendet Wine nur dann im `EXIT`-Trap, wenn das Skript selbst den Update-Launcher gestartet hat.
- Ein Präfix nicht mit verschiedenen Wine-Versionen (System-Wine, dw-proton, Proton über umu) nacheinander betreiben.
- Der sichtbare Launcher kann sich nach `Main Window Visible`/`SessionRole` beenden. Das ist für diese Lösung kein Fehler; Login und Spiel laufen direkt über `ZwiftApp.exe`.

## Sicherheitsregeln bei Änderungen

- Keine Neuinstallation über `/home/smoe/Games/zwift-winehq` ausführen. Vor jedem Installeraufruf mit `ls` prüfen, ob das Ziel existiert und was darin liegt.
- Keine Präfixe, Registrydateien, Windows-Programmdateien, übersetzte `.exe`-Dateien, Logs oder Zugangsdaten in diese Quellablage kopieren.
- Den Lutris-Runner dw-proton nicht löschen oder durch eine andere Version ersetzen, solange eine Installation ihn verwendet.
- Keine vollständige Installation oder einen Spielstart als reinen Syntaxtest ausführen. Eine Installation lädt mehrere GB herunter; ein tatsächlicher Updateaufruf kann Programmdateien verändern.

## Prüfbefehle

Statische Prüfungen ohne Änderung eines Präfixes:

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
bash -n install-zwift.sh start-zwift.sh update-zwift.sh
python3 - <<'PY'
from pathlib import Path
import shlex
import yaml

config = yaml.safe_load(Path('zwift-lutris.yml').read_text())
args = shlex.split(config['script']['installer'][0]['execute']['args'])
for arg in args:
    if arg.startswith('/'):
        assert Path(arg).is_file(), arg
yaml.safe_load(Path('.gitlab-ci.yml').read_text())
yaml.safe_load(Path('.github/workflows/ci.yml').read_text())
print('YAML und lokale Quellpfade sind gültig.')
PY
```

Schutz des Installers vor Überschreiben testen. Das Ziel wird dafür selbst angelegt und befüllt; der Aufruf muss vor jeder Wine-Aktion mit Fehler enden:

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
guard=/tmp/opencode/install-guard-test
mkdir -p "$guard" && touch "$guard/nicht-leer"
! ./install-zwift.sh "$guard" "$PWD/start-zwift.sh" "$PWD/update-zwift.sh" "$PWD/RunFromProcess.cs"
rm -rf "$guard"
```

Prüfen, ob dw-proton ein Präfix anlegen kann und `tasklist` die erwartete Ausgabe liefert (Wegwerfpräfix, unter einer Minute):

```bash
wine=~/.local/share/lutris/runners/wine/dw-proton-x86_64/files/bin
export WINEPREFIX=/tmp/opencode/wineboot-test
timeout 300 "$wine/wine" wineboot -u
"$wine/wine" start notepad.exe; sleep 3
LC_ALL=C "$wine/wine" tasklist /fo list /fi 'IMAGENAME eq notepad.exe' | grep '^PID:'
"$wine/wineserver" -k
rm -rf "$WINEPREFIX"
```

Reine CDN-/Versionsprüfung nur dann als harmlos einordnen, wenn installierte und CDN-Version gleich sind. Andernfalls führt derselbe Befehl ein echtes Update aus:

```bash
WINEPREFIX=/pfad/zum/zwift-praefix ./update-zwift.sh
```

Nach einer Änderung am Startablauf ist ein realer Start bis zum Anmeldefenster zu testen; in `drive_c/users/steamuser/AppData/Local/Zwift/Logs/Log.txt` müssen `Legacy Launcher Used` und `GameFlowState Login` erscheinen.

## Getesteter Stand

7. November 2026, Fedora 44, dw-proton `wine-11.0 (CachyOS)` (`dwproton-11.0-14`), Zwift `1.125.0`:

- Vollinstallation nach `/home/smoe/Games/zwift-winehq`. Der erste Lauf scheiterte am Update, weil `libvkd3d-*.dll` fehlten (siehe `WINEDLLPATH` oben). Danach wurden `wineboot -u` mit gesetztem `WINEDLLPATH` und `update-zwift.sh` erneut ausgeführt, nicht der komplette Installer.
- Start über `start-zwift.sh` bis zum Anmeldefenster: `Log.txt` enthält `Legacy Launcher Used` und `GameFlowState Login`; vom Benutzer bestätigt.
- `RunFromProcess.exe` aus dem GitLab-CI (Mono) im Wegwerfpräfix getestet: Das gestartete Programm erhält den gewünschten Elternprozess.
