# Zwift unter Linux mit Lutris

Dieses Repository enthält einen Lutris-Installer für die Windows-Version von Zwift. Zwift läuft im Lutris-Runner **wine** mit dem Wine-Build **dw-proton**; ein systemweit installiertes Wine ist nicht nötig. Lutris lädt dw-proton bei Bedarf selbst und startet es über umu. Das Startprogramm `ZwiftStart.exe` übernimmt Update, Vollbild-Voreinstellung und den eigentlichen Spielstart.

Quellen: [GitLab](https://gitlab.metaccount.de/multimedia/zwift) (Hauptrepository) · [GitHub](https://github.com/smoebody/lutris-zwift) (Spiegel) · Downloads: [smoebody.github.io/lutris-zwift](https://smoebody.github.io/lutris-zwift/)

## Dateien

| Datei | Zweck |
|---|---|
| [zwift-lutris.yml](zwift-lutris.yml) | Lutris-Installer (Runner `wine`, dw-proton); lädt `ZwiftSetup.exe` vom Zwift-CDN und `ZwiftStart.exe` aus dem GitHub-Release |
| [ZwiftStart.cs](ZwiftStart.cs) | Quelltext des Startprogramms im Zwift-Verzeichnis |
| [agents.md](agents.md) | Technische Pfade, Wartungsregeln und Prüfbefehle |
| [.gitlab-ci.yml](.gitlab-ci.yml) | GitLab-CI: übersetzt `ZwiftStart.exe`, veröffentlicht Pakete, Releases und die Projektseite |
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | Dieselbe Pipeline als GitHub Actions |
| [public-src/](public-src/) | Vorlage der statischen Projektseite (GitLab und GitHub Pages) |

Die Windows-Programmdateien, das Wine-Präfix, Zugangsdaten und Logs sind nicht Bestandteil dieser Quellablage.

## Voraussetzungen

- Lutris 0.5.22 oder neuer (getestet mit dem Fedora-Paket, nicht mit Flatpak)
- Internetzugang während der Installation; es werden mehrere GB geladen

## Installation über Lutris

1. In Lutris **+ → Install from a local install script** wählen und [zwift-lutris.yml](zwift-lutris.yml) öffnen.
2. Als Ziel ein **neues, leeres Verzeichnis** wählen.
3. Nach der Installation den Eintrag **Zwift** starten. Er steht unter dem Runner **Wine**.

Ablauf des Installers:

1. `create_prefix` legt das Präfix mit dw-proton an.
2. `ZwiftSetup.exe` wird still installiert. Vorher führt Proton den Protonfix `umu-zwift` aus. Er installiert `corefonts`, `dotnet48`, `d3dcompiler_47` und `webview2`; das dauert am längsten.
3. `ZwiftStart.exe` wird nach `C:\Program Files (x86)\Zwift` kopiert.
4. `ZwiftStart.exe --update-only` lädt das eigentliche Spiel über den Zwift-Launcher.

## ZwiftStart.exe

```
ZwiftStart.exe [--update-only | --no-update]
```

Bei jedem Start:

1. Vergleicht die installierte Version mit `Zwift_ver_cur.xml` vom Zwift-CDN. Bei einer neuen Version startet es `ZwiftLauncher.exe SilentLaunch` und wartet bis zu fünf Minuten auf das Update. Ist das CDN nicht erreichbar, startet es die installierte Version.
2. Legt die Standardordner im Windows-Benutzerprofil an. Fehlen sie, beendet sich `ZwiftApp.exe` sofort.
3. Setzt in `AppData\Local\Zwift\prefs.xml` `<FULLSCREEN>1</FULLSCREEN>`, aber nur, wenn der Schlüssel fehlt. Eine Umstellung im Spiel bleibt also erhalten.
4. Startet den Launcher und `ZwiftApp.exe` als dessen Kindprozess. Sobald das Spiel `Legacy Launcher Used` ins Log schreibt, wird der Launcher beendet.
5. Bleibt aktiv, bis `ZwiftApp.exe` endet. So erkennt Lutris das Spielende.

Das Protokoll steht in `drive_c/Program Files (x86)/Zwift/ZwiftStart.log`. Unter umu erscheint die Konsolenausgabe nicht.

**Die Anmeldung erfolgt direkt im Spiel.** Es werden weder API-Token noch gespeicherte Zugangsdaten verwendet.

### Warum der Elternprozess-Umweg?

- `ZwiftApp.exe` allein startet den Launcher und beendet sich selbst.
- `ZwiftApp.exe` neben einem laufenden Launcher, aber nicht als sein Kindprozess, bleibt als Prozess bestehen, lädt aber nichts.
- Wird der Launcher zu früh beendet, schließt sich `ZwiftApp.exe` nach wenigen Sekunden.

Nur als Kindprozess des Launchers lädt das Spiel und zeigt das Anmeldefenster. `ZwiftStart.exe` setzt dafür `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS`, so wie nirsofts RunFromProcess.

## Build und Releases

GitLab ist das Hauptrepository; GitLab spiegelt `main` und alle Tags nach [GitHub](https://github.com/smoebody/lutris-zwift). Beide Plattformen übersetzen `ZwiftStart.cs` mit Mono `mcs -platform:x64` (Image `mono:6.12`) und prüfen, ob eine PE32+-Datei für x86-64 entstanden ist.

| Ereignis | GitLab ([.gitlab-ci.yml](.gitlab-ci.yml)) | GitHub ([ci.yml](.github/workflows/ci.yml)) |
|---|---|---|
| Push auf `main` | Paket `zwiftstart`, Version `staging` | Pre-Release `staging` (Tag wandert mit) |
| Tag `release/X.Y.Z` | Paketversion `X.Y.Z` und Release `ZwiftStart X.Y.Z` | Release `ZwiftStart X.Y.Z` |
| beides | Projektseite neu bauen | Projektseite neu bauen |

Der Installer lädt `ZwiftStart.exe` aus dem GitHub-Release, weil GitLab nicht öffentlich ist. Für eine neue Version von `ZwiftStart.exe`:

1. Release-Tag anlegen und pushen (der Spiegel überträgt es nach GitHub):
   ```bash
   git tag -s release/X.Y.Z -m "ZwiftStart X.Y.Z"
   git push origin release/X.Y.Z
   ```
2. In [zwift-lutris.yml](zwift-lutris.yml) URL und `checksum` auf die SHA-256 aus `ZwiftStart.exe.sha256` des GitHub-Releases umstellen. Mono übersetzt nicht reproduzierbar; GitLab und GitHub liefern also unterschiedliche Dateien.

Commits und Tags werden signiert. Von einem LLM unterstützte Commits erhalten den Trailer `Assisted-by: <Modell>`.

Die Projektseiten liefern die Programmdatei ebenfalls aus – öffentlich unter <https://smoebody.github.io/lutris-zwift/>, nach Anmeldung unter <https://multimedia.pages.metaccount.de/zwift>:

| Download | Pfad |
|---|---|
| Neuestes Release | `download/latest/ZwiftStart.exe` |
| Bestimmtes Release (nur das neueste) | `download/X.Y.Z/ZwiftStart.exe` |
| Staging (`main`) | `download/staging/ZwiftStart.exe` |

Zu jeder Datei gibt es eine `.sha256`-Prüfsumme. Release 0.1.0 enthält noch die alte Lösung (Bash-Wrapper, Runner `linux`, `RunFromProcess.exe`).

## Veröffentlichung auf lutris.net

Siehe [agents.md](agents.md#lutrisnet).
