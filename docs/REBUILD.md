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
- `docs/`: Entwicklungsnachweise und später der geprüfte öffentliche Web-Build.

Geplante technische Nachweise: vollständiger Datenimport, robuste Quiz- und Speicherzustände, tatsächliche Unity-Kompilation, begehbare Wege und Kameras, Soundsteuerung, Tastaturbedienung, geprüfter Web-Build und funktionierender öffentlicher Link. Bildkonzepte sind keine Screenshots eines fertigen Spiels.

## Geprüfter Zwischenstand: Modell 2.1

Die neue Datenbasis umfasst 16 Kompetenzen mit 48 Kriterien und konkrete KI-Bezüge in jedem Bereich. 153 Lernzielvorschläge decken 43 Studienbausteine und 3 Wahlvarianten ab. Das Profil und die 12 Berufsperspektiven verwenden diese Kriterien ohne automatische Kompetenzwerte. 39 Unity-Tests sowie anschliessende C#-Referenz-/Hashprüfungen sind erfolgreich; echte DE-/EN-Datenexporte wurden erzeugt. Das bestätigt die Daten-/Lernlogik, noch keinen spielbaren Web-Build.

Die Bildvorlagen, lokale Raumplanung und Blender-Produktionsskripte sind vorbereitet. Produktion und endgültiger Weltaufbau warten auf die explizite Sichtfreigabe der vorgelegten Vorlagen. Bildgalerie und Vorschauoberfläche sind keine Spielaufnahmen. Der lokal geprüfte KI-Baututor besitzt eine eigene Veröffentlichung; sein jeweils tatsächlich veröffentlichter Quellenstand und die Live-Prüfungen stehen in companion/published.json.
