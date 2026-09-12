# Öffentlicher Unity-Release · 12.09.2026

[Spiel öffnen](https://gpochs.github.io/kompetenzhaus-psychologie-uzh-unity/) · [erfolgreicher Pages-Lauf](https://github.com/gpochs/kompetenzhaus-psychologie-uzh-unity/actions/runs/34690814542)

Veröffentlicht ist die Wurzel von `gh-pages`, Commit `c42efdc855cd1972a136f657e172cc7c8305ecd0`. Die 15 Spieldateien umfassen **20’479’960 Bytes**, zusätzlich liegen `.nojekyll` und `.gitattributes` im Releasebranch. Die abschliessende Anpassung betraf zwei statische Shell-/Metadatendateien; Unity-WASM und Szenendaten blieben unverändert.

## Tatsächlich geprüft

- Die öffentliche Seite startet den echten Unity-Player. Quizantwort und anschliessende Erklärung funktionieren. Das Profil zeigt Modell 2.2 mit 16 Kompetenzen und 48 Kriterien, sechs KI-Bereichen und Phasen ohne, mit und über KI. Zwölf Berufsperspektiven sind verfügbar; die Psychotherapieansicht wurde mit ihren konkreten Tätigkeiten geprüft.
- Ein benannter Raum mit 2 × 2 Bodenfeldern wurde auf der öffentlichen Seite über die Bauoberfläche angelegt und nach Neuladen wiederhergestellt. Vogel- und Ich-Perspektive sowie Umsehen durch Ziehen wurden dort geprüft.
- Im echten lokalen Web-Player in Edge wurden alle **43 Modul-Einstiege** geöffnet. Ein Modulquiz und die erste Hauptquest wurden vollständig durchlaufen, einschliesslich letzter Erklärung und ausdrücklichem Weiter. Speichern, Neuladen, Bauänderungen, Undo und ausgewählte Einstellungen wurden geprüft. Der Bericht unterscheidet früheren und optimierten Build; nicht jede Frage, Wahlvariante oder alle elf Quests wurden vollständig durchgespielt.
- Die optimierte Szene bestand **5/5 PlayMode-Tests**: vier Physiktests und eine Szenenmessung. **20/20 UI-Vertragstests** sowie die getrennten Daten- und Companiontests bestanden. Diese Zahlen sind kein gemeinsamer Gesamtlauf.
- In Edge und dem In-App-Browser wurden tatsächliche 3D-Ansichten geprüft. Im In-App-Browser bei 1440 × 900 ergaben Unity-Messwerte auf diesem Laptop ungefähr **59 FPS** für den kleinen Startaufbau und die Innenhofvorlage. Die grösste gemessene Geometrieobergrenze betrug **112’500 Dreiecke** in einem dichten Aufbau mit zwei Häusern zu je 64 Bodenfeldern. Das ist weder ein FPS-Nachweis für diese dichte Szene noch eine Garantie für jede erlaubte Dekoration oder andere Geräte.

Der abschliessende [öffentliche Release-Nachweis](../art/review/public-release.json) enthält die Abläufe auf GitHub Pages, den bestätigten externen Tutorzugang und die dortige Gerätestichprobe von rund 59.2 FPS bei 1440 × 900 mit einem Raum. Weitere Nachweise liegen unter [lokaler Browserprüfung](../art/review/browser-runtime-review.json), [Edge-Prüfung](../art/review/runtime-evidence/edge-ui-2026-09-12.json), [Physik](../art/review/physics-review.json) und [Szenenmessung](../art/review/production-metrics.json). WebAudio-Ausführung und Mischereinstellungen wurden beobachtet; ein subjektives Hörqualitätsurteil wird nicht behauptet.

## KI-Begleitung: geprüfter externer Zugang

Die eingebettete Oberfläche lud auf GitHub Pages, lieferte für die Testfrage aber keine KI-Antwort. Deshalb gilt **`embedGenerationVerified: false`**. Das Spiel öffnet den [veröffentlichten Claude-Tutor 2.2](https://claude.ai/public/artifacts/28b92e04-206d-41b5-ad4e-2382126be043) separat und bietet freiwilliges Kopieren des Lernkontexts an. Dort erzeugte dieselbe Frage eine echte Antwort zu P1.2, R4.2, V1 und einer eigenständigen Vorhersage ohne KI. Automatische Kontextübertragung wird nicht behauptet. Die **39 Mock-Host-Tests** sind von diesen echten Claude-Antworten getrennt; [published.json](../companion/published.json) hält den genauen Umfang fest.

Zusatzkosten: **0**. Keine kostenpflichtige API, Käufe oder Nachladungen. Der externe Tutor verwendet das Claude-Konto und verfügbare Kontingent der Besuchenden. Einige Status- und Accessibility-Texte dort bleiben nach dem Wechsel zu Englisch deutsch.

Das [Originalspiel](https://gpochs.github.io/kompetenzhaus-psychologie-uzh/) wurde erneut sichtbar geprüft; der ursprüngliche Remote-Commit bleibt `824de17987e6ed20d0eb47a98284dc4717bfe87a`. Original-Repository und interne Rohquellen wurden nicht verändert. Das Kompetenz- und Modulmodell bleibt ein didaktischer Entwurf, keine beschlossene Studienordnung oder Kompetenzbescheinigung.
