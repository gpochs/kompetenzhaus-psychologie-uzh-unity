# Kompetenzhaus Psychologie UZH – Unity-Neuentwicklung

Diese öffentliche Repository ist die **einmalige, eigenständige Kopie** des bisherigen Spiels mit erhaltener Git-Historie. Hier entsteht eine neue Unity-Web-Version mit Bachelor- und Masterhaus, Vogel- und Ich-Perspektive, unmittelbar sichtbaren Modulquizzen, zusätzlichen Lernquests, eigener Grafik und Sound sowie einem GenAI-Baututor.

Das neue Kompetenzmodell **2.2.0-draft** verbindet Fachwissenschaft, Future Skills und KI in **16 Kompetenzen mit 48 eindeutig zugeordneten Kriterien**. Die 153 Lernzielvorschläge für 43 Modulslots und drei Wahlvarianten beschreiben konkrete Aufgaben und vorgeschlagene Nachweise. Vierzehn Zukunftsansichten und zwölf Berufsperspektiven verwenden dieselben Kriterien. Alle Angaben zum künftigen Lerndesign sind Entwürfe, keine beschlossene Studienordnung.

KI betrifft alle sechs Kompetenzbereiche: fachliches Verstehen, wissenschaftliches Prüfen, verantwortliches Handeln, Zusammenarbeit, technische Gestaltung und Werte. Eine eigene Ansicht macht diese Verbindungen sichtbar, ohne Kriterien oder Bewertungen zu verdoppeln. Phasen ohne, mit und über KI werden passend zur Aufgabe vorgeschlagen; daraus entstehen weder pauschal KI-freie Pflichtmodule noch zusätzliche Prüfungen. Gebaute Module und bestandene Spielquizze zeigen Lerngelegenheiten und Spielübung, keine nachgewiesene Kompetenz.

**Status: in Entwicklung.** Noch kein fertiger Unity-Web-Build veröffentlicht. [Entwicklungsumfang und Nachweise](docs/REBUILD.md) · [Lerndaten](content/README.md) · [GenAI-Integration](docs/genai-integration-design.md).

Die Unity-Szene ist aufgebaut; 22 eigene FBX-Modelle wurden für den Webeinsatz optimiert. Die erneuten Physik- und Browserprüfungen laufen. Der [Tutor 2.2](https://claude.ai/public/artifacts/28b92e04-206d-41b5-ad4e-2382126be043) ist bereits separat veröffentlicht; sein bestätigter Prüfungsumfang steht in [companion/published.json](companion/published.json). Das Spiel selbst wird nach der Abnahme über die Wurzel des Branches `gh-pages` veröffentlicht.

Für den Einstieg in die neue Webfassung:

- **Bauen:** In die 3D-Fläche klicken, dann mit **W A S D** oder den **Pfeiltasten** die Vogelansicht verschieben. **+ / −** vergrössert oder verkleinert die Ansicht; ein Klick auf einen Lernort öffnet dessen Aufgabe.
- **Erkunden:** Mit **W A S D** oder den **Pfeiltasten** gehen. Zum Umsehen die **linke Maustaste halten und ziehen**. Bei einem erreichbaren Lernort **E** drücken.
- **Quiz:** Antwort auswählen → **Antwort prüfen** → Erklärung lesen → **Weiter**. Offene Dialoge halten die Bewegung an; **Esc** schliesst sie. **Studium** öffnet Module und Kompetenzansichten, **KI-Begleitung** den freiwilligen Tutorzugang.

Das [bisherige Spiel](https://gpochs.github.io/kompetenzhaus-psychologie-uzh/) und seine [ursprüngliche Repository](https://github.com/gpochs/kompetenzhaus-psychologie-uzh) bleiben erhalten. Zusatzkostenbudget dieser Neuentwicklung: **0**. Interne Originaldokumente werden nicht veröffentlicht.

---

## Dokumentation der übernommenen Ausgangsversion

Die folgenden Beschreibungen, Kompetenzkürzel und Startanweisungen beziehen sich auf das historische Spiel. Sie sind keine Anleitung oder Leistungszusage für den neuen Unity-Build.

# Das Kompetenzhaus — Psychologie UZH (Entwurf)

Ein browserbasiertes 3D-Bauspiel: Studierende bauen ihr Psychologiestudium (BSc 06B-7200-120, MSc 06M-7200-120) Modul für Modul zu einem Haus — und mit jedem Baustein wächst ihr Kompetenzprofil aus **Fachkompetenzen (Fa1–Fa10)**, **KI-Kompetenzen (KI1–KI6)** und **Future Skills (Fu1–Fu3)**.

> Grundlage ist das **Kompetenzaufbaumodell vom 02.07.2026** — ein Entwurf als Gesprächsbasis für das Psychologische Institut der UZH, **kein beschlossenes Curriculum**. Die «Zukunftsmodul»-Beschreibungen sind Diskussionsvorschläge.

## Spielidee

- **Statik = Studienlogik.** Ohne Fundament (Propädeutikum, 38 ECTS) trägt nichts; das Dach ist die Bachelorarbeit; im Master ist der Einstiegs-Dreierblock der Schlüsselstein für den Abschlussturm mit der Masterprüfungs-Fahne.
- **Zwei Modi.** *Freies Bauen* (Kombinationen testen) und *Serious Mode* (nur selbst als bestanden markierte Module — das Haus zeigt den echten Studienstand).
- **Quests ✦.** Jeder Baustein trägt eine 5–15-Minuten-Mini-Aufgabe aus dem Kompetenzaufbaumodell (z. B. eine KI-Antwort gegen das Lehrbuch prüfen).
- **Kompetenzpass.** Druckbares Kompetenzprofil (PDF) mit Modulen, [A]/[B]/[C]-Prüfungslogik, Quests und eigenen Merksätzen — für Standortgespräche, Bewerbungen, PhD-Dossiers.
- **Teilen & Campus.** Haus als Link teilen (alles im URL-Hash, kein Server), Häuser von Kolleg:innen als Nachbarhäuser speichern. Bewusst **ohne Ranglisten** — das Spieldesign folgt der Evidenz zu Gamification in der Hochschullehre (u. a. Sailer & Homner, 2020; Hanus & Fox, 2015; Deci et al., 1999).
- Jahreszeiten im Semesterrhythmus, Tag/Nacht, Baustile pro Baustein, Schwerpunkt-Fassaden (DeNC/HEA/SEOP) im Master.

## Nutzung

Einfach `index.html` öffnen — kein Build, kein Server, keine Konten. Für lokale Entwicklung: `node server.js` → <http://localhost:8642>.

Alle Daten bleiben **lokal im Browser** (localStorage); Export/Import als JSON. Details: Menü → Datenschutz.

## Architektur

| Datei | Inhalt |
|---|---|
| `index.html` | HUD, Panels, Modals, Styles |
| `js/daten-struktur.js` | Blaupause: Slots, Bauregeln (Mustercurriculum), 19 Kompetenzen, Meilensteine |
| `js/daten-texte.js` | Zukunftsmodul-Texte, Lernziele, KI-Integration, Quests (de/en) |
| `js/main.js` | Three.js-Engine: Placement, Statik-Regeln, Juice, Jahreszeiten, Avatar, Kompetenzpass |
| `js/i18n.js`, `js/audio.js` | UI-Texte de/en, WebAudio-Synth (keine Audiodateien) |
| `lib/` | Three.js 0.180 (lokal gebündelt, keine CDN-Abhängigkeit) |

Entwickelt am Psychologischen Institut der Universität Zürich (Lehrentwicklung), 2026.
