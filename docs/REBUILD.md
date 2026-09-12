# Kompetenzhaus Unity – Entwicklungsstand

Diese eigenständige öffentliche Repository entstand am 12.09.2026 durch eine einmalige Kopie von `gpochs/kompetenzhaus-psychologie-uzh`. Der Ausgangscommit ist `824de17987e6ed20d0eb47a98284dc4717bfe87a`. Die vorhandene Historie bleibt erhalten. `upstream` verweist lesend auf die ursprüngliche Repository; Push dorthin ist in dieser Arbeitskopie deaktiviert.

Das ursprüngliche Spiel bleibt unter https://gpochs.github.io/kompetenzhaus-psychologie-uzh/ erreichbar. Diese Repository erhält eine eigene Unity-Web-Version. Solange kein getesteter Unity-Web-Build veröffentlicht wurde, ist die Neuentwicklung noch kein fertiges Spiel.

## Vereinbarte Gestaltung

- Vogelperspektive für das Bauen des Kompetenzhauses und die Modulquests.
- Ich-Perspektive für Erkundung, zusätzliche Hauptquests und Nebenquests.
- Quizfragen erscheinen beim Start einer Modulaufgabe sofort im Vordergrund. Auswahl, Prüfung, Erklärung und Weiter sind getrennte Schritte; auch die letzte Erklärung bleibt sichtbar.
- Wissenschaftliches Denken, praktische Psychologieaufgaben und KI-Kompetenzen bestimmen die Aufgaben und Belohnungen.
- Hausgestaltung mit Materialien, Farben, Möbeln und Begrünung; Fortschritt schaltet neue Gestaltungsmöglichkeiten frei.
- Bildvorlagen aus der integrierten OpenAI-Bildgenerierung, danach eigene Blender-Modelle, dann Unity-Szenen und Web-Build.
- Geräusche und ruhige Atmosphäre aus eigener lokaler Klangsynthese; separate Lautstärken und Bewegungseinstellungen.
- GenAI-Baututor, fiktive Gesprächsübungen, Kompetenz-/CV-Reflexion und Karriereorientierung über Claude-Artefakte mit den Konten und Nutzungslimits der Spielenden. Keine vom Betreiber bezahlte API und kein API-Schlüssel im Browser.

## Grenzen der Inhalte

Das Kompetenzaufbaumodell ist ein Entwurf, kein genehmigtes Curriculum. Zusätzliche KI-Aufgaben sind didaktische Designvorschläge. Sie ändern keine offiziellen Modulvoraussetzungen, Prüfungsformen oder Gewichtungen. Originaldokumente aus internen Quellen gehören nicht in diese öffentliche Repository.

Der Teaching-Hub-Quellordner wird ausschliesslich lesend verwendet. Die ursprüngliche Arbeitskopie und das bisherige veröffentlichte Spiel werden nicht verändert.

## Kosten

Zusatzkostenbudget: **0**. Keine kostenpflichtigen Assetkäufe, Meshy-Aufträge, GenAI-APIs, Backend-Abonnemente oder Unity-Cloud-Dienste. Es gibt keine automatische kostenpflichtige Ausweichlösung. Bestehende Nutzerkonten können ihre eigenen Nutzungslimits erreichen; das normale Lernspiel bleibt dann verwendbar.

## Entwicklungsaufteilung

- Die bisherigen Dateien und Lerninhalte bleiben als nachvollziehbare Ausgangsbasis erhalten.
- `content/`: aus öffentlichen Bestandsdaten abgeleitete portable Lerndaten und neue Aufgaben.
- `UnityProject/`: Unity-Quellen, Runtime und Editor-Werkzeuge.
- `art/`: Bildvorlagen, Blender-Quellen, überprüfbare Raumplanung und selbst erzeugtes Audio.
- `companion/`: Quellfassung des weiterentwickelten Claude-Baututors.
- `docs/`: Entwicklungsnachweise. Der spätere öffentliche Web-Build liegt separat in der Wurzel des Branches `gh-pages`.

Geplante technische Nachweise: vollständiger Datenimport, robuste Quiz- und Speicherzustände, tatsächliche Unity-Kompilation, begehbare Wege und Kameras, Soundsteuerung, Tastaturbedienung, geprüfter Web-Build und funktionierender öffentlicher Link. Bildkonzepte sind keine Screenshots eines fertigen Spiels.

## Geprüfter Zwischenstand: Modell 2.2

Die neue Datenbasis umfasst 16 Kompetenzen mit 48 Kriterien und konkrete KI-Bezüge in jedem Bereich. 153 Lernzielvorschläge decken 43 Studienbausteine und 3 Wahlvarianten ab. Das Profil und die 12 Berufsperspektiven verwenden diese Kriterien ohne automatische Kompetenzwerte. Modell 2.2 zeigt zusätzlich KI als Querschnitt durch alle sechs Bereiche und aufgabenbezogene Phasen ohne, mit und über KI. Die bestätigten Daten-, Logik- und Oberflächenprüfungen sind im [aktuellen Arbeitsplan](PLAN.md) getrennt aufgeführt; ihre Testzahlen werden nicht zu einem vermeintlichen Gesamtlauf addiert.

Die Bildvorlagen, Raumplanung und der Unity-Plan sind freigegeben; die tatsächliche Unity-Szene ist aufgebaut. Nach einer Messung wurden die 22 eigenen Modelle gezielt vereinfacht, geprüft und erneut als FBX exportiert. Der [Optimierungsnachweis](../art/review/model-optimization.json) unterscheidet den einzelnen Modellsatz (37’282 → 21’486 Dreiecke) von der Szene mit allen Instanzen. In drei gemessenen Unity-Aufbauten lag die höchste Geometrieobergrenze bei 112’500 Dreiecken, im dichten Aufbau mit allen 43 Modulslots und zwei Häusern mit jeweils 64 Bodenfeldern. Das unveränderte Budget beträgt 160’000. Diese Stichprobe deckt nicht jede mögliche Gestaltung ab; die [Szenenmessung](../art/review/production-metrics.json) ist keine Browser-Leistungsabnahme. Die abschliessende Physik- und Browserprüfung läuft weiter.

Der [Tutor 2.2](https://claude.ai/public/artifacts/28b92e04-206d-41b5-ad4e-2382126be043) ist separat veröffentlicht. Echte Claude-Antworten im Tutormodus wurden auf dieser Veröffentlichung in Deutsch und Englisch geprüft; 39 Tests mit simuliertem Host sind ein eigener Nachweis. Die früheren vier Modi und Gesprächsabläufe wurden auf der erhaltenen 2.1-Fassung geprüft, nicht erneut auf 2.2. Details, Quellenhash und verbleibende Sprachgrenzen stehen in [companion/published.json](../companion/published.json). Die Einbettung im veröffentlichten Spiel bleibt offen.

## Erste Schritte in der Webfassung

Nach der Hauswahl kann man einen ersten Raum zeichnen, eine editierbare Vorlage wählen oder unmittelbar einen Lerncheck öffnen. Die Bedienung ist für Tastatur und Maus beschrieben; schmale Ansichten allein belegen noch keine vollständige Touch-Steuerung.

- **Bauen / Vogelansicht:** Einmal in die 3D-Fläche klicken. **W A S D** oder **Pfeiltasten** verschieben die Ansicht; die sichtbaren Tasten **+ / −** heissen «Ansicht vergrössern» und «Ansicht verkleinern» (EN: «Zoom in/out»). Ein Klick auf einen Lernort öffnet seine Aufgabe.
- **Erkunden / Ich-Perspektive:** **W A S D** oder **Pfeiltasten** bewegen die Spielfigur. Zum Umsehen die **linke Maustaste halten und ziehen**; der Webmodus benötigt keine Maussperre. Bei einem erreichbaren Lernort **E** drücken und den eingeblendeten Hinweis beachten.
- **Bauplan und Dialoge:** Hat das Grundrissraster den Fokus, wählen die Pfeiltasten ein Feld und die Leertaste benutzt das Zeichenwerkzeug. In geöffneten Dialogen ruht die Bewegung. **Esc** schliesst den Dialog; danach bei Bedarf wieder in die 3D-Fläche klicken.
- **Lerncheck:** Antwort wählen, **Antwort prüfen**, Erklärung lesen, **Weiter**. Auch die letzte Erklärung bleibt sichtbar. **Studium** bündelt Modulübersicht und Kompetenzansichten; **Quests** und **KI-Begleitung** sind eigene freiwillig erreichbare Menüs.

Nach den Änderungen an Zoom und Bedienhinweisen bestanden die **20/20 UI-/Vertragstests** erneut. Sie ersetzen keine Bedienprüfung des neu gebauten Unity-Players im Browser.

## Öffentliche FBX-Dateien aus der geprüften Quelle exportieren

Der verbindliche Exportweg für veröffentlichbare FBX-Dateien ist `art/blender/export_public_fbx.py`. Voraussetzungen sind Blender 5.2 mit dem vorhandenen FBX-Exporter, die freigegebene `art/models/kompetenzhaus-assets.blend`, das passende `asset-audit.json` und die 22 bereits geprüften FBX-Exporte als Vergleichsbasis. Im Repository-Stammordner ausführen; `blender` muss auf die installierte Version im Suchpfad verweisen:

```powershell
$repoRoot = (Resolve-Path -LiteralPath '.').Path
blender --background --factory-startup `
  --python "art/blender/export_public_fbx.py" -- "$repoRoot"
```

Der einzige Skriptparameter hinter `--` ist der Repositorypfad. Der Wrapper ruft den vorhandenen Generator intern im Exportmodus auf; `--export` wird hier nicht zusätzlich übergeben. Er prüft zuerst den Hash der freigegebenen Blender-Quelle und vergleicht die strukturierten FBX-Szenendaten vor und nach dem Export. Native Dateipfadfelder werden vor der regulären Binärkodierung durch portable Dateinamen ersetzt, darunter `Original|ApplicationNativeFile`, `Original|FileName`, `DocumentUrl` und `SrcDocumentUrl`. Die installierten Exporterdateien und die Blender-Quelle werden nicht geändert; eine prozesslokale Anpassung wird nach dem Export zurückgesetzt.

Bei Erfolg meldet das Skript `PUBLIC_FBX_METADATA_CLEAN 22 scene data unchanged; source unchanged` und aktualisiert [asset-audit.json](../art/models/asset-audit.json) sowie [fbx-metadata-audit.json](../art/models/fbx-metadata-audit.json). Der Nachweis umfasst gleiche Geometrie-, Transformations-, Material- und Verbindungsdaten und keine privaten absoluten Dateipfade in den strukturiert gelesenen Metadaten. Nicht mit einem direkten `build_assets.py --export`-Lauf als letzten Veröffentlichungsschritt umgehen: Dieser kann die nativen Quelldateipfade erneut eintragen. FBX-Binärdateien werden nicht durch rohe Textersetzung bereinigt.

Der Wrapper ist ein Export- und Metadatencheck, keine erneute Physik- oder Browserprüfung. Nachher folgen Unity-Import, aktuelle Tests und der finale Web-Build.

## Lokaler WebGL-Build mit Unity CLI

Voraussetzungen: Die Unity CLI ist im Suchpfad verfügbar, Unity **6000.6.0f1** samt Web-Build-Support ist installiert, und die gespeicherte Spielszene wurde aufgebaut. Während des Builds darf keine zweite Editor-Instanz dieses Projekt öffnen. Der folgende PowerShell-Aufruf wird im Stammordner dieser Repository ausgeführt. Die Pfade werden aus dem aktuellen Verzeichnis ermittelt; Leerzeichen in ihnen bleiben durch die Anführungszeichen erhalten.

```powershell
$repoRoot = (Resolve-Path -LiteralPath '.').Path
$unityProject = Join-Path $repoRoot 'UnityProject'
$buildOutput = Join-Path $unityProject 'Build/WebGL'
$buildLog = Join-Path $unityProject 'Logs/web-build.log'

unity run "$unityProject" `
  --editor-version "6000.6.0f1" `
  --format json --non-interactive --no-banner --timeout 3600 `
  -- -buildTarget WebGL -force-d3d11 `
  -executeMethod "Kompetenzhaus.Editor.BuildGame.Web" `
  -sourceRoot "$repoRoot" `
  -buildOutput "$buildOutput" `
  -logFile "$buildLog"
```

Die Backticks am Zeilenende setzen den PowerShell-Befehl fort; hinter ihnen darf kein Leerzeichen stehen. Argumente hinter `--` werden an den Unity Editor übergeben. **`-buildTarget WebGL` muss bereits beim Start gesetzt sein.** Es sorgt dafür, dass Unity mit den WebGL-Modulreferenzen kompiliert. Ohne dieses Argument trat hier `CS0103` für `WebGLInput` auf: Die spätere Wahl von `BuildTarget.WebGL` innerhalb der Build-Methode behebt die vorher fehlende Referenz nicht. `-force-d3d11` bezeichnet die Grafik-API des lokalen Windows-Editors, nicht die Grafik-API des Browser-Builds.

Bei langen Windows-Pfaden kann ein bereits eingerichteter kurzer Alias wie `U:\` optional dasselbe `UnityProject` bezeichnen. Dafür die Zuweisung von `$unityProject` durch `$unityProject = 'U:\'` ersetzen und danach `$buildOutput` sowie `$buildLog` aus diesem Wert ableiten. `$repoRoot` bleibt der vollständige physische Repositorypfad für `-sourceRoot`: Ein Laufwerksalias hat keinen übergeordneten Repositoryordner. Der Alias ist keine Projektkopie und keine Voraussetzung der Anleitung.

`BuildGame.Web` synchronisiert Inhalt und Webvorlage, konfiguriert den Player und baut die gespeicherte Szene. Das Ausgabeziel muss innerhalb dieses Unity-Projekts liegen. Die Methode prüft den Unity-`BuildReport` und protokolliert bei Erfolg `[Kompetenzhaus] WEB_BUILD_COMPLETE bytes=…`. CLI-Ergebnis, Log und erzeugte Dateien gemeinsam kontrollieren. Ein erfolgreicher lokaler Build benötigt danach noch einen tatsächlichen Browsertest; die Methode veröffentlicht nichts. Lokale Ausgaben unter `UnityProject/Build/` und Logs bleiben aus Git ausgeschlossen.

## Veröffentlichung und verbleibende Prüfung

Die geplante Pages-Quelle ist **Branch `gh-pages`, Ordner `/`**. In dessen Wurzel wird der Inhalt des abgenommenen lokalen Web-Builds abgelegt: `index.html`, Shell-Dateien, `Build/`, `TemplateData/`, die beiden Laufzeit-Lerndateien unter `content/` und die freigegebenen Companion-Metadaten. Eine `.nojekyll`-Datei gehört ebenfalls in diese Wurzel. `docs/` auf `main` bleibt Entwicklungsdokumentation; der ursprüngliche Spielstand im Original-Repository wird nicht überschrieben.

Vorher `node tools/inspect-web-build.mjs` ausführen und zusätzlich die gebauten Shell-, Lern- und Companion-Dateien mit ihren aktuellen Quellen vergleichen. Die statische Prüfung eines früheren Builds bestand, enthielt aber noch ältere Bedienhinweise: Ein erfolgreicher Dateicheck allein bestätigt deshalb nicht die Aktualität der gesamten Oberfläche. Es gibt noch keine bestätigte öffentliche Veröffentlichung des neuen Spiels.

Am 12.09.2026 bestanden nach Optimierung und bereinigtem FBX-Export **5/5 echte Unity-PlayMode-Tests**: vier Physiktests und eine Szenenmessung. In der zusammengesetzten Szene mit den importierten Modellen und dem produktiven `CharacterController` wurden Modell-/Kameraintegration, durchquerbare Aussen- und Innentüren bei blockierenden Wänden, Treppenauf- und -abstieg in allen vier Ausrichtungen sowie Kollisionen nach Umbau, Undo und Wiederherstellung geprüft. Der dichte Messaufbau blieb bei 112’500 Dreiecken. Die Tests verwendeten einen temporären Zustand und lasen oder schrieben keine gespeicherten Spielstände.

Der [Physiknachweis](../art/review/physics-review.json) enthält Testfälle, Messwerte, Quellenhashes und die Grenzen dieser Prüfung. **Ein fertiger WebGL-Build, vollständige Browserbedienung, Browserleistung und öffentlicher Release sind damit noch nicht bestätigt.**
