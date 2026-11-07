# agents.md

## Projektüberblick

Quellablage und Dokumentation für Zwift für Windows unter Lutris/WineHQ. Der Lutris-Runner `linux` ist absichtlich gewählt: Er führt den Bash-Startwrapper aus, der wiederum `/usr/bin/wine64` verwendet. Das Wine-Präfix wird vom Installer selbst aufgebaut; ein Container-Image (früher `docker.io/netbrain/zwift:latest`) ist nicht mehr nötig.

Die Lösung besteht aus drei getrennten Schritten:

1. `install-zwift-winehq.sh` legt ein neues Präfix an, installiert per winetricks `corefonts dotnet48 d3dcompiler_47 webview2` sowie `renderer=vulkan`, installiert `ZwiftSetup.exe`, übersetzt `RunFromProcess.cs` mit dem `csc.exe` aus dotnet48 und lädt über `update-zwift.sh` das Spiel.
2. `update-zwift.sh` prüft vor dem Spielstart die CDN-Version und führt bei Bedarf `ZwiftLauncher.exe SilentLaunch` aus.
3. `start-zwift.sh` startet `ZwiftApp.exe` über `RunFromProcess.exe` als Kindprozess des Launchers und beendet danach Launcher und Crash-Handler.

Die Anmeldung findet ausschließlich im Spiel statt. Keine API-Tokens oder Zugangsdaten in Dateien, Befehlen oder Dokumentation aufnehmen.

## Dateien

| Datei | Aufgabe |
|---|---|
| [zwift-lutris.yml](zwift-lutris.yml) | Lutris-Installer; ruft `install-zwift-winehq.sh` mit absoluten Pfaden unter `/home/smoe/Projects/metaccount/multimedia/zwift` direkt auf. Keine `files`-Einträge: Lutris 0.5.22 lädt `file://`-URLs mit `requests` und scheitert mit „No connection adapters were found“. `game.prefix: $GAMEDIR` ist nötig, damit Lutris ohne `files` überhaupt ein Zielverzeichnis abfragt und `$GAMEDIR` ersetzt |
| [install-zwift-winehq.sh](install-zwift-winehq.sh) | Neuinstallation in ein leeres absolutes Ziel; verweigert `/`, `$HOME` und nicht leere Ziele. Argumente: Ziel, Startwrapper, Updatewrapper, `RunFromProcess.cs` |
| [start-zwift.sh](start-zwift.sh) | Profilordner anlegen, Launcher-PID ermitteln, Spiel über `RunFromProcess.exe` starten und auf dessen Ende warten |
| [update-zwift.sh](update-zwift.sh) | Lokale/CDN-Version vergleichen, höchstens 60 × 5 Sekunden auf ein Update warten |
| [RunFromProcess.cs](RunFromProcess.cs) | C#-Ersatz für `runfromprocess-rs`: `RunFromProcess.exe <ppid> <kommandozeile>` startet ein Programm mit `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS` |
| [readme.md](readme.md) | Voraussetzungen, Installation, Start, Updateverhalten und Anmeldung |

| [.gitlab-ci.yml](.gitlab-ci.yml) | CI: `build` (Mono `mcs`), `package_staging`/`package_release` (Generic Package Registry `runfromprocess`), `release`, `pages` |
| [public-src/](public-src/) | Pages-Vorlage; Platzhalter `@RELEASE@`, `@STAGING@`, `@COMMIT@`, `@DATE@`, `@PROJECT_URL@` ersetzt der `pages`-Job |

Die Skripte verwenden Bash mit `set -euo pipefail` und vier Spaces Einrückung. Bestehende ASCII-Schreibweise in Skriptmeldungen und Kommentaren beibehalten. In die Quellablage gehört nur der C#-Quelltext, keine übersetzte `.exe`.

## Aktive Pfade und Quellablage

| Zweck | Pfad |
|---|---|
| Dokumentierte Quellablage | `/home/smoe/Projects/metaccount/multimedia/zwift` |
| Produktives Präfix (noch aus dem Netbrain-Image) | `/home/smoe/Games/zwift-winehq` |
| Produktiver Startwrapper (alter Stand, `runfromprocess-rs.exe`) | `/home/smoe/Games/zwift-winehq/start-zwift.sh` |
| Produktiver Updatewrapper (alter Stand) | `/home/smoe/Games/zwift-winehq/update-zwift.sh` |
| Testpräfix des neuen Installers | `/home/smoe/Games/zwift-winehq-test` |
| Aktiver Lutris-Eintrag | `/home/smoe/.local/share/lutris/games/zwift-windows-wine-webview2-1793952865.yml` |
| Ursprüngliche Quellkopien (Netbrain-Variante) | `/home/smoe/zwift-lutris` |
| Ursprünglicher Lutris-Installer | `/home/smoe/zwift-lutris.yml` |
| Altes Proton-Präfix, nicht anfassen | `/home/smoe/Games/zwift` |
| Referenzimplementierung | `/tmp/opencode/netbrain-zwift/src` (temporär, nicht als dauerhafte Quelle voraussetzen) |

Der installierte Lutris-Eintrag verweist nicht auf die Projektdateien, sondern direkt auf die Wrapper im produktiven Präfix. Der ursprüngliche Installer unter `/home/smoe/zwift-lutris.yml` und seine Quellen bleiben unverändert.

**Projektdateien und produktive Wrapper sind eigenständige Kopien und nicht mehr kompatibel.** Der neue `start-zwift.sh` erwartet `RunFromProcess.exe`, im produktiven Präfix liegt nur `runfromprocess-rs.exe`. Den neuen Startwrapper daher nicht in das produktive Präfix kopieren, ohne dort vorher `RunFromProcess.exe` zu übersetzen. Für den Umstieg ist eine Neuinstallation in ein leeres Verzeichnis vorgesehen; danach den Lutris-Eintrag (vorher sichern) auf das neue Präfix umstellen.

Bekannter Fehler im produktiven `update-zwift.sh`: `launcher_is_running` sucht nach `^Name:`, `tasklist` liefert aber `Image Name:`. Dadurch bricht ein echtes Update sofort mit „Launcher wurde vor Abschluss des Updates beendet“ ab. In der Quellablage ist das behoben (Prüfung auf `^PID:`).

Beim Verschieben dieses Projektverzeichnisses müssen die vier absoluten Pfade in `installer[0].execute.args` von `zwift-lutris.yml` sowie die dokumentierten Beispielpfade angepasst werden. Die Wrapper selbst bestimmen ihr Verzeichnis dynamisch.

## CI, Pakete und Pages

- GitLab-Projekt `multimedia/zwift` (ID 110, privat) auf `gitlab.metaccount.de`; Remote per SSH über `git.metaccount.de:10022`.
- Es gibt nur Linux-Docker-Runner (Tag `docker`). `RunFromProcess.exe` wird im CI deshalb mit Mono `mcs -platform:x64` übersetzt; der Installer übersetzt weiterhin selbst mit `csc.exe` im Präfix und lädt nichts aus dem CI.
- Release-Tags folgen `release/X.Y.Z`; andere Tags starten keine Pipeline. Push auf `main` überschreibt die Paketversion `staging`.
- Pages ist pfadbasiert (`https://multimedia.pages.metaccount.de/zwift`, eindeutige Domain abgeschaltet). Der `pages`-Job baut die Seite bei jedem Lauf neu und lädt dazu Staging und das höchste `release/*`-Tag (`sort -V`) aus der Paket-Registry. Releases verlinken auf die Pages-Dateien; dort liegt nur das jeweils neueste Release.
- Die Pages-Sichtbarkeit lässt sich bei diesem privaten Projekt nicht auf `public` stellen („public access is disabled“).

## Laufzeitverhalten und Invarianten

- Standardpräfix ist bei beiden Wrappern das jeweilige Skriptverzeichnis; ein vorhandenes `WINEPREFIX` hat Vorrang.
- `WINE` und `WINESERVER` können über Umgebungsvariablen überschrieben werden; Standard ist `/usr/bin/wine64` beziehungsweise `/usr/bin/wineserver`.
- `WINEPREFIX/drive_c/Program Files (x86)/Zwift` ist die erwartete Windows-Installationsstruktur.
- Unter Fedora muss das vollständige Paket `wine` installiert sein. Fehlt `wine-common`, fehlt `/usr/share/wine/winmd`; `rundll32 setupapi,InstallHinfSection … wine.inf` läuft dann mit 100 % CPU endlos und `wineboot` hängt. Der Installer prüft `wine.inf` und `winmd` vorab.
- `ZwiftSetup.exe` installiert nur den Launcher (`Zwift_ver_cur.xml` mit Version `0.0.0`). Das Spiel selbst kommt erst über `update-zwift.sh`, das der Installer am Ende aufruft.
- Ohne Elternprozess-Umweg funktioniert der Start nicht (getestet am 6. November 2026): `ZwiftApp.exe` allein startet den Launcher und beendet sich; neben einem laufenden Launcher, aber nicht als sein Kindprozess, lädt es nichts und schreibt kein `Log.txt`.
- `RunFromProcess.exe` wird mit `C:\windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /platform:x64` übersetzt und benötigt daher dotnet48 im Präfix. `start-zwift.sh` ruft es mit `wine start /wait` auf; es beendet sich direkt nach `CreateProcess`.
- Der Startwrapper legt die vollständigen Standardprofilordner für den aktuellen Linux-Benutzer an. Diese Ordner nicht entfernen: Fehlen sie, beendet sich `ZwiftApp.exe` sofort.
- Alle `tasklist`-Aufrufe zur PID-/Prozesserkennung benötigen `LC_ALL=C`; die Auswertung erwartet englische Schlüssel wie `PID:`. Auf `Image Name:` oder `PID:` prüfen, nicht auf `Name:`.
- `start-zwift.sh` beendet nach erfolgreichem Spielstart nur `ZwiftLauncher.exe` und `ZwiftWindowsCrashHandler.exe`. Der `EXIT`-Trap ruft abschließend `wineserver -k` für das ausgewählte Präfix auf.
- `update-zwift.sh` bricht ab, wenn die CDN-Version nicht gelesen werden kann oder leer ist, und beendet Wine nur dann im `EXIT`-Trap, wenn das Skript selbst den Update-Launcher gestartet hat.
- Im produktiven Netbrain-Präfix muss `.update-timestamp` weiterhin `disable` enthalten (Präfix aus WineHQ 11.21, System-Wine 11.20). Neu installierte Präfixe passen zur System-Wine und brauchen das nicht.
- Proton und WineHQ nicht im selben Präfix mischen. `/home/smoe/Games/zwift` bleibt unverändert.
- Der sichtbare Launcher kann sich nach `Main Window Visible`/`SessionRole` beenden. Das ist für diese Lösung kein Fehler; Login und Spiel laufen direkt über `ZwiftApp.exe`.

## Sicherheitsregeln bei Änderungen

- Keine Neuinstallation über `/home/smoe/Games/zwift-winehq` oder `/home/smoe/Games/zwift` ausführen. Der Installer akzeptiert nur neue beziehungsweise leere Ziele.
- Keine Präfixe, Registrydateien, Windows-Programmdateien, übersetzte `.exe`-Dateien, Logs oder Zugangsdaten in diese Quellablage kopieren.
- Das alte Proton-Präfix und das vorhandene Podman-Image nicht aktualisieren, neu erzeugen oder löschen.
- Änderungen am aktiven Lutris-Eintrag nur bei Bedarf vornehmen und vorher sichern.
- Keine vollständige Installation oder einen Spielstart als reinen Syntaxtest ausführen. Eine Installation lädt mehrere GB herunter; ein tatsächlicher Updateaufruf kann Programmdateien verändern.

## Prüfbefehle

Statische Prüfungen ohne Änderung eines Präfixes:

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
bash -n install-zwift-winehq.sh start-zwift.sh update-zwift.sh
python3 - <<'PY'
from pathlib import Path
import shlex
import yaml

config = yaml.safe_load(Path('zwift-lutris.yml').read_text())
args = shlex.split(config['script']['installer'][0]['execute']['args'])
for arg in args:
    if arg.startswith('/'):
        assert Path(arg).is_file(), arg
print('YAML und lokale Quellpfade sind gültig.')
PY
```

Schutz des Installers vor Überschreiben testen; der Aufruf muss vor einer Wine-Aktion wegen des nicht leeren Ziels mit Fehler enden:

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
! ./install-zwift-winehq.sh /home/smoe/Games/zwift-winehq \
    "$PWD/start-zwift.sh" "$PWD/update-zwift.sh" "$PWD/RunFromProcess.cs"
```

Prüfen, ob System-Wine ein Präfix anlegen kann (Wegwerfpräfix, unter einer Minute):

```bash
WINEPREFIX=/tmp/opencode/wineboot-test timeout 300 /usr/bin/wine64 wineboot -i
WINEPREFIX=/tmp/opencode/wineboot-test /usr/bin/wineserver -k
rm -rf /tmp/opencode/wineboot-test
```

Reine CDN-/Versionsprüfung nur dann als harmlos einordnen, wenn installierte und CDN-Version gleich sind. Andernfalls führt derselbe Befehl ein echtes Update aus:

```bash
WINEPREFIX=/pfad/zum/zwift-praefix ./update-zwift.sh
```

Nach einer Änderung am Startablauf ist ein realer Start bis zum Anmeldefenster zu testen; in `drive_c/users/$USER/AppData/Local/Zwift/Logs/Log.txt` müssen `Legacy Launcher Used` und `GameFlowState Login` erscheinen.

## Getesteter Stand (6. November 2026)

- System-Wine `11.20 (Staging)`, Fedora 44, Paket `wine` vollständig installiert
- Neuer Installer im Testpräfix: winetricks `f3890f6`, dotnet48, WebView2, `ZwiftSetup.exe` erfolgreich; Spiel per `update-zwift.sh` auf `1.125.0` (`168455`) gebracht
- `RunFromProcess.exe` mit `csc.exe` übersetzt; `ZwiftApp.exe` startete als Kindprozess des Launchers, lud die Welt und zeigte das Anmeldefenster
- Noch offen: vollständiger Installerlauf am Stück mit dem aktuellen Skript, Start über den neuen `start-zwift.sh`, Anmeldung und Fahrt, Installation über Lutris
- Produktives Präfix: Zwift `1.125.0` aus dem Netbrain-Image (`c735f91a…`, WineHQ 11.21), unverändert
