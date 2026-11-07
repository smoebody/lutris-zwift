# Zwift unter Linux mit Lutris und WineHQ

Dieses Verzeichnis enthält die Installations-, Update- und Startdateien für die Windows-Version von Zwift. Das Windows-Spiel läuft mit dem systemweit installierten WineHQ; Lutris startet lediglich einen Linux-Wrapper. Der Installer richtet das Wine-Präfix selbst ein. Ein Container-Image wird nicht benötigt.

Die bisherige produktive Installation liegt außerhalb dieses Projektverzeichnisses in `/home/smoe/Games/zwift-winehq`. Sie stammt noch aus dem Netbrain-Image und wird von diesen Dateien nicht verändert. Das frühere Proton-Präfix `/home/smoe/Games/zwift` bleibt ebenfalls unberührt.

## Dateien

| Datei | Zweck |
|---|---|
| [zwift-lutris.yml](zwift-lutris.yml) | Lokaler Lutris-Installer; ruft den Installer mit den Quelldateien aus diesem Verzeichnis auf |
| [install-zwift-winehq.sh](install-zwift-winehq.sh) | Richtet ein neues Wine-Präfix mit dotnet48, WebView2 und Zwift in einem leeren Zielverzeichnis ein |
| [update-zwift.sh](update-zwift.sh) | Vergleicht die installierte Version mit dem Zwift-CDN und aktualisiert bei Bedarf |
| [start-zwift.sh](start-zwift.sh) | Startet `ZwiftApp.exe` über den Launcher und `RunFromProcess.exe` |
| [RunFromProcess.cs](RunFromProcess.cs) | Quelltext des Hilfsprogramms, das `ZwiftApp.exe` als Kindprozess des Launchers startet |
| [agents.md](agents.md) | Technische Pfade, Wartungsregeln und Prüfbefehle |
| [.gitlab-ci.yml](.gitlab-ci.yml) | Übersetzt `RunFromProcess.exe`, veröffentlicht Pakete, Releases und die Projektseite |
| [public-src/](public-src/) | Vorlage der statischen Projektseite (GitLab Pages) |

Die Windows-Programmdateien, das Wine-Präfix, Zugangsdaten und Logs sind nicht Bestandteil dieser Quellablage.

## Voraussetzungen

- Lutris
- systemweites WineHQ mit `/usr/bin/wine64` und `/usr/bin/wineserver`, unter Fedora das vollständige Paket `wine` (insbesondere `wine-common`)
- `curl`, `sha256sum`, `cabextract`, `unzip`
- `ntlm_auth` aus `samba-winbind-clients`
- GNU `grep` mit PCRE-Unterstützung (`grep -P`)
- Internetzugang während der Installation

Fehlt `wine-common`, fehlen `/usr/share/wine/wine.inf` beziehungsweise `/usr/share/wine/winmd`. `wineboot` hängt dann endlos beim Anlegen des Präfixes. Der Installer prüft das vorab und bricht mit einer Meldung ab.

Getestet wurde der Stand mit Zwift `1.125.0` und System-Wine `11.20 (Staging)` unter Fedora 44.

## Neue Installation über Lutris

1. In Lutris **+ → Install from a local install script** wählen und [zwift-lutris.yml](zwift-lutris.yml) öffnen.
2. Als Ziel ein **neues oder leeres Verzeichnis** auswählen. Der Installer bricht bei einem nicht leeren Ziel ab, damit keine bestehende Installation überschrieben wird.
3. Nach der Installation Zwift über den erzeugten Lutris-Eintrag starten.

Der Installer lädt winetricks in einer festen Version (mit Prüfsumme), installiert damit `corefonts`, `dotnet48`, `d3dcompiler_47` und `webview2` und stellt den Vulkan-Renderer ein. Anschließend installiert er `ZwiftSetup.exe` vom Zwift-CDN, übersetzt `RunFromProcess.exe` mit dem `csc.exe` aus dotnet48 und lädt über `update-zwift.sh` das eigentliche Spiel. Das dauert je nach Verbindung längere Zeit; zwischendurch können Installationsfenster erscheinen.

Der Lutris-Runner ist bewusst `linux`: Ausgeführt wird `start-zwift.sh`; dieses Skript ruft das Windows-Spiel mit `/usr/bin/wine64` auf. Es wird also nicht der Linux-Build von Zwift verwendet.

### Manuelle Installation

Alternativ kann der Installer direkt aufgerufen werden:

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
./install-zwift-winehq.sh \
  /pfad/zum/leeren/zwift-praefix \
  "$PWD/start-zwift.sh" \
  "$PWD/update-zwift.sh" \
  "$PWD/RunFromProcess.cs"
```

Der Installer kopiert Start- und Updatewrapper in das neue Präfix. Von dort aus lassen sie sich ohne weitere Angaben aufrufen:

```bash
/pfad/zum/zwift-praefix/update-zwift.sh
/pfad/zum/zwift-praefix/start-zwift.sh
```

Ohne gesetztes `WINEPREFIX` erwarten beide Wrapper das Präfix in ihrem eigenen Verzeichnis. Für einen Aufruf aus diesem Projektverzeichnis muss `WINEPREFIX` gesetzt werden.

## Bestehende Installation starten

In Lutris den Eintrag **Zwift** starten. Dessen aktive Konfiguration verwendet weiterhin:

- Start: `/home/smoe/Games/zwift-winehq/start-zwift.sh`
- Vorab ausgeführtes Update: `/home/smoe/Games/zwift-winehq/update-zwift.sh`
- Wine-Präfix: `/home/smoe/Games/zwift-winehq`

Diese Wrapper sind ältere Kopien und verwenden noch `runfromprocess-rs.exe` aus dem Netbrain-Image. Die Wrapper aus diesem Verzeichnis erwarten `RunFromProcess.exe` und dürfen nicht ohne Weiteres in das alte Präfix kopiert werden (siehe [agents.md](agents.md)).

Der Startwrapper bleibt aktiv, solange `ZwiftApp.exe` läuft. Nach dem Beenden des Spiels beendet er die Wine-Prozesse dieses Präfixes.

## Updates und Anmeldung

Vor jedem Start über Lutris liest `update-zwift.sh` die lokale Versionsdatei und `Zwift_ver_cur.xml` vom Zwift-CDN. Kann die CDN-Version nicht gelesen werden, bricht der Wrapper mit einer Meldung ab. Ist die Installation aktuell, endet die Prüfung ohne Launcher-Start. Andernfalls startet der Wrapper `ZwiftLauncher.exe SilentLaunch`, wartet auf die neue Versionsdatei und beendet den von ihm gestarteten Launcher anschließend.

`start-zwift.sh` startet den Launcher erneut im Modus `SilentLaunch`, ermittelt dessen Windows-PID und startet `ZwiftApp.exe` mit `RunFromProcess.exe` als Kindprozess des Launchers. Danach werden Launcher und Crash-Handler beendet; das Spiel läuft weiter.

**Die Anmeldung erfolgt direkt im Spiel.** Es werden weder API-Token noch gespeicherte Zugangsdaten von den Skripten verwendet.

## Warum `RunFromProcess.exe`?

Im Testpräfix wurde geprüft, ob das Spiel ohne diesen Umweg startet:

- `ZwiftApp.exe` allein startet den Launcher und beendet sich selbst.
- `ZwiftApp.exe` neben einem laufenden Launcher, aber nicht als sein Kindprozess, bleibt als Prozess bestehen, lädt aber nichts.

Nur als Kindprozess des Launchers lädt das Spiel und zeigt das Anmeldefenster. `RunFromProcess.exe` übernimmt dafür die Aufgabe von `runfromprocess-rs` aus dem Netbrain-Projekt. Der Quelltext liegt hier; übersetzt wird er erst im Präfix mit dem `csc.exe`, das dotnet48 mitbringt. So liegt keine Windows-Binärdatei in der Quellablage, und Rust wird nicht benötigt.

## Wichtige WineHQ-Anpassungen

- **Vollständige Benutzerprofilordner:** Der Startwrapper legt unter `drive_c/users/$USER` unter anderem `Documents`, `Downloads` und `Saved Games` an. Fehlen sie, beendet sich `ZwiftApp.exe` sofort wieder.
- **`LC_ALL=C` für `tasklist`:** Die PID-Erkennung erwartet die englische Ausgabe `PID:` und funktioniert dadurch auch mit deutscher Systemgebietsschema-Einstellung.
- **Getrennte Präfixe:** Proton und WineHQ werden nicht im selben Präfix eingesetzt oder nacheinander darauf angewendet.

## Build und Releases

Die GitLab-Pipeline übersetzt `RunFromProcess.cs` mit Mono `mcs -platform:x64` (Image `mono:6.12`) und prüft, ob eine PE32+-Datei für x86-64 entstanden ist.

- **Push auf `main`:** Upload als Staging-Version in die Generic Package Registry (Paket `runfromprocess`, Version `staging`), danach Aktualisierung der Projektseite.
- **Tag `release/X.Y.Z`:** Upload als Paketversion `X.Y.Z`, GitLab-Release `RunFromProcess X.Y.Z` und Aktualisierung der Projektseite.

Release anlegen:

```bash
git tag release/1.0.0
git push origin release/1.0.0
```

Die Projektseite unter <https://multimedia.pages.metaccount.de/zwift> liefert die Programmdatei selbst aus:

| Download | Pfad |
|---|---|
| Neuestes Release | `download/latest/RunFromProcess.exe` |
| Bestimmtes Release (nur das neueste) | `download/X.Y.Z/RunFromProcess.exe` |
| Staging (`main`) | `download/staging/RunFromProcess.exe` |

Zu jeder Datei gibt es eine `.sha256`-Prüfsumme. Ältere Releases liegen in der Paket-Registry des Projekts.

## Statische Prüfung

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
bash -n install-zwift-winehq.sh start-zwift.sh update-zwift.sh
```

Die reine Updateprüfung einer Installation ist mit folgendem Befehl möglich; bei einer verfügbaren neuen Version wird dabei tatsächlich aktualisiert:

```bash
WINEPREFIX=/pfad/zum/zwift-praefix ./update-zwift.sh
```
