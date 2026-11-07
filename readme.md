# Zwift unter Linux mit Lutris

Dieses Repository enthält die Installations-, Update- und Startdateien für die Windows-Version von Zwift. Das Windows-Spiel läuft mit dem Lutris-Wine-Runner **dw-proton**; ein systemweit installiertes Wine ist nicht nötig. Lutris startet lediglich einen Linux-Wrapper. Der Installer lädt dw-proton bei Bedarf selbst und richtet das Wine-Präfix ein. Ein Container-Image wird nicht benötigt.

Quellen: [GitLab](https://gitlab.metaccount.de/multimedia/zwift) (Hauptrepository) · [GitHub](https://github.com/smoebody/lutris-zwift) (Spiegel) · Downloads: [smoebody.github.io/lutris-zwift](https://smoebody.github.io/lutris-zwift/)

## Dateien

| Datei | Zweck |
|---|---|
| [zwift-lutris.yml](zwift-lutris.yml) | Lokaler Lutris-Installer; ruft den Installer mit den Quelldateien aus diesem Verzeichnis auf |
| [install-zwift.sh](install-zwift.sh) | Lädt bei Bedarf dw-proton und richtet ein neues Wine-Präfix mit dotnet48, WebView2 und Zwift in einem leeren Zielverzeichnis ein |
| [update-zwift.sh](update-zwift.sh) | Vergleicht die installierte Version mit dem Zwift-CDN und aktualisiert bei Bedarf |
| [start-zwift.sh](start-zwift.sh) | Startet `ZwiftApp.exe` über den Launcher und `RunFromProcess.exe` |
| [RunFromProcess.cs](RunFromProcess.cs) | Quelltext des Hilfsprogramms, das `ZwiftApp.exe` als Kindprozess des Launchers startet |
| [agents.md](agents.md) | Technische Pfade, Wartungsregeln und Prüfbefehle |
| [.gitlab-ci.yml](.gitlab-ci.yml) | GitLab-CI: übersetzt `RunFromProcess.exe`, veröffentlicht Pakete, Releases und die Projektseite |
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | Dieselbe Pipeline als GitHub Actions |
| [public-src/](public-src/) | Vorlage der statischen Projektseite (GitLab und GitHub Pages) |

Die Windows-Programmdateien, das Wine-Präfix, Zugangsdaten und Logs sind nicht Bestandteil dieser Quellablage.

## Voraussetzungen

- Lutris
- `curl`, `sha256sum`, `tar`, `xz`, `cabextract`, `unzip`
- `ntlm_auth` aus `samba-winbind-clients`
- GNU `grep` mit PCRE-Unterstützung (`grep -P`)
- Internetzugang während der Installation

### Wine: dw-proton aus Lutris

Verwendet wird der Wine-Build aus dem Lutris-Runner `dw-proton` unter `~/.local/share/lutris/runners/wine/dw-proton-x86_64/files/bin/wine`. Ist er nicht vorhanden, lädt der Installer dieselbe Version, die Lutris anbietet (`dwproton-11.0-14`), prüft die SHA-256-Summe und entpackt sie an diese Stelle. Lutris erkennt den Runner danach ebenfalls.

Die Skripte rufen `wine` aus dw-proton direkt auf, ohne umu bzw. das Proton-Startskript. Nur so laufen alle Prozesse im selben Präfix sichtbar nebeneinander – Voraussetzung dafür, dass `ZwiftApp.exe` als Kindprozess des Launchers starten kann.

Mit den Umgebungsvariablen `WINE` und `WINESERVER` lässt sich ein anderes Wine wählen, etwa ein System-Wine (`WINE=/usr/bin/wine64 WINESERVER=/usr/bin/wineserver`). Unter Fedora muss es dann das vollständige Paket `wine` inklusive `wine-common` sein; der Installer prüft `wine.inf` und `winmd` vorab.

## Neue Installation über Lutris

1. In Lutris **+ → Install from a local install script** wählen und [zwift-lutris.yml](zwift-lutris.yml) öffnen.
2. Als Ziel ein **neues oder leeres Verzeichnis** auswählen. Der Installer bricht bei einem nicht leeren Ziel ab, damit keine bestehende Installation überschrieben wird.
3. Nach der Installation Zwift über den erzeugten Lutris-Eintrag starten.

Der Installer lädt bei Bedarf dw-proton und winetricks in einer festen Version (mit Prüfsumme), installiert damit `corefonts`, `dotnet48`, `d3dcompiler_47` und `webview2` und stellt den Vulkan-Renderer ein. Anschließend installiert er `ZwiftSetup.exe` vom Zwift-CDN, übersetzt `RunFromProcess.exe` mit dem `csc.exe` aus dotnet48 und lädt über `update-zwift.sh` das eigentliche Spiel. Das dauert je nach Verbindung längere Zeit; zwischendurch können Installationsfenster erscheinen.

Der Lutris-Runner ist bewusst `linux`: Ausgeführt wird `start-zwift.sh`; dieses Skript ruft das Windows-Spiel mit `wine` aus dw-proton auf. Es wird also nicht der Linux-Build von Zwift verwendet.

### Manuelle Installation

Alternativ kann der Installer direkt aufgerufen werden:

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
./install-zwift.sh \
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

**Achtung:** Der Installer legt ein nicht vorhandenes Ziel an und installiert hinein. Er schützt nur vor nicht leeren Verzeichnissen. Den Zielpfad daher vorher prüfen.

## Spiel starten

In Lutris den nach der Installation erzeugten Eintrag **Zwift** starten oder direkt `/pfad/zum/zwift-praefix/start-zwift.sh` aufrufen. Der Startwrapper bleibt aktiv, solange `ZwiftApp.exe` läuft. Nach dem Beenden des Spiels beendet er die Wine-Prozesse dieses Präfixes.

## Updates und Anmeldung

Vor jedem Start über Lutris liest `update-zwift.sh` die lokale Versionsdatei und `Zwift_ver_cur.xml` vom Zwift-CDN. Kann die CDN-Version nicht gelesen werden, bricht der Wrapper mit einer Meldung ab. Ist die Installation aktuell, endet die Prüfung ohne Launcher-Start. Andernfalls startet der Wrapper `ZwiftLauncher.exe SilentLaunch`, wartet auf die neue Versionsdatei und beendet den von ihm gestarteten Launcher anschließend.

`start-zwift.sh` startet den Launcher erneut im Modus `SilentLaunch`, ermittelt dessen Windows-PID und startet `ZwiftApp.exe` mit `RunFromProcess.exe` als Kindprozess des Launchers. Danach werden Launcher und Crash-Handler beendet; das Spiel läuft weiter.

**Die Anmeldung erfolgt direkt im Spiel.** Es werden weder API-Token noch gespeicherte Zugangsdaten von den Skripten verwendet.

## Warum `RunFromProcess.exe`?

Im Testpräfix wurde geprüft, ob das Spiel ohne diesen Umweg startet:

- `ZwiftApp.exe` allein startet den Launcher und beendet sich selbst.
- `ZwiftApp.exe` neben einem laufenden Launcher, aber nicht als sein Kindprozess, bleibt als Prozess bestehen, lädt aber nichts.

Nur als Kindprozess des Launchers lädt das Spiel und zeigt das Anmeldefenster. `RunFromProcess.exe` übernimmt dafür die Aufgabe von `runfromprocess-rs` aus dem Netbrain-Projekt. Der Quelltext liegt hier; übersetzt wird er erst im Präfix mit dem `csc.exe`, das dotnet48 mitbringt. So liegt keine Windows-Binärdatei in der Quellablage, und Rust wird nicht benötigt.

## Wichtige Wine-Anpassungen

- **Vollständige Benutzerprofilordner:** Der Startwrapper legt unter `drive_c/users/$USER` unter anderem `Documents`, `Downloads` und `Saved Games` an. Fehlen sie, beendet sich `ZwiftApp.exe` sofort wieder.
- **`LC_ALL=C` für `tasklist`:** Die PID-Erkennung erwartet die englische Ausgabe `PID:` und funktioniert dadurch auch mit deutscher Systemgebietsschema-Einstellung.
- **Ein Wine pro Präfix:** Ein Präfix nicht nacheinander mit verschiedenen Wine-Versionen (System-Wine, dw-proton, Proton über umu) betreiben.

## Build und Releases

GitLab ist das Hauptrepository; GitLab spiegelt jede Änderung samt Tags automatisch nach [GitHub](https://github.com/smoebody/lutris-zwift). Beide Plattformen übersetzen `RunFromProcess.cs` mit Mono `mcs -platform:x64` (Image `mono:6.12`) und prüfen, ob eine PE32+-Datei für x86-64 entstanden ist.

| Ereignis | GitLab ([.gitlab-ci.yml](.gitlab-ci.yml)) | GitHub ([ci.yml](.github/workflows/ci.yml)) |
|---|---|---|
| Push auf `main` | Paket `runfromprocess`, Version `staging` | Pre-Release `staging` (Tag wandert mit) |
| Tag `release/X.Y.Z` | Paketversion `X.Y.Z` und Release `RunFromProcess X.Y.Z` | Release `RunFromProcess X.Y.Z` |
| beides | Projektseite neu bauen | Projektseite neu bauen |

Release anlegen (auf GitLab; der Spiegel überträgt das Tag nach GitHub):

```bash
git tag -s release/1.0.0 -m "RunFromProcess 1.0.0"
git push origin release/1.0.0
```

Commits und Tags werden signiert. Von einem LLM unterstützte Commits erhalten den Trailer `Assisted-by: <Modell>`.

Die Projektseiten liefern die Programmdatei selbst aus – öffentlich unter <https://smoebody.github.io/lutris-zwift/>, nach Anmeldung unter <https://multimedia.pages.metaccount.de/zwift>:

| Download | Pfad |
|---|---|
| Neuestes Release | `download/latest/RunFromProcess.exe` |
| Bestimmtes Release (nur das neueste) | `download/X.Y.Z/RunFromProcess.exe` |
| Staging (`main`) | `download/staging/RunFromProcess.exe` |

Zu jeder Datei gibt es eine `.sha256`-Prüfsumme. Ältere Releases liegen in der GitLab-Paket-Registry beziehungsweise unter den GitHub-Releases.

## Statische Prüfung

```bash
cd /home/smoe/Projects/metaccount/multimedia/zwift
bash -n install-zwift.sh start-zwift.sh update-zwift.sh
```

Die reine Updateprüfung einer Installation ist mit folgendem Befehl möglich; bei einer verfügbaren neuen Version wird dabei tatsächlich aktualisiert:

```bash
WINEPREFIX=/pfad/zum/zwift-praefix ./update-zwift.sh
```
