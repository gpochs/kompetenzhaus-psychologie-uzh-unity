// UI copy only. Curriculum, quiz answers and user-authored room names remain host data.
const english = new Map([
  ['Zur Spielsteuerung', 'Skip to game controls'],
  ['Dreidimensionale Spielwelt', 'Three-dimensional game world'],
  ['Kompetenzhaus in 3D. Im Erkundungsmodus mit W A S D bewegen. Zum Umsehen die linke Maustaste halten und ziehen.', 'Kompetenzhaus in 3D. Move with W A S D in exploration mode. Hold the left mouse button and drag to look around.'],
  ['Zur Startansicht', 'Return to the welcome screen'],
  ['Psychologie · UZH', 'Psychology · UZH'],
  ['Haus auswählen', 'Choose a house'],
  ['Ton ein- oder ausschalten', 'Turn sound on or off'],
  ['Ton ausschalten', 'Mute sound'], ['Ton einschalten', 'Enable sound'], ['Ton', 'Sound'],
  ['Einstellungen öffnen', 'Open settings'], ['Einstellungen', 'Settings'],
  ['Dein', 'Your'], ['Kompetenzhaus.', 'Kompetenzhaus.'],
  ['Baue Räume für dein Wissen.', 'Build rooms for your knowledge.'],
  ['Entdecke, was Psychologie möglich macht.', 'Discover what psychology makes possible.'],
  ['Wo möchtest du beginnen?', 'Where would you like to begin?'],
  ['Fundament legen & Zusammenhänge entdecken', 'Build foundations and discover connections'],
  ['Vertiefen, gestalten & eigenständig forschen', 'Explore in depth, design and conduct research'],
  ['Dein Haus betreten', 'Enter your house'], ['Spiel wird geladen', 'Loading the game'],
  ['Die Spielwelt wird geladen …', 'Loading your world …'], ['Erneut versuchen', 'Try again'],
  ['Ein spielerischer Entwurf für ein Psychologiestudium mit KI.', 'A playful proposal for studying psychology with AI.'],
  ['Zwei unterschiedlich gestaltete Psychologiehäuser in einem gemeinsamen Garten', 'Two distinctive psychology houses in a shared garden'],
  ['Gestaltungsentwurf', 'Design concept'],
  ['Dein nächster Schritt', 'Your next step'], ['Nächster Schritt', 'Next step'],
  ['Lernweg-Hinweis ausblenden', 'Hide the learning-path guide'], ['Dein Weg', 'Your path'],
  ['Raum', 'Room'], ['Entdecken', 'Discover'], ['Bauen', 'Build'], ['Vertiefen', 'Explore further'],
  ['Deine aktuelle Hauptquest', 'Your current main quest'], ['Quest öffnen', 'Open quest'],
  ['Lieber mit einer Vorlage beginnen?', 'Prefer to begin with a template?'],
  ['Längsbau', 'Linear building'], ['Innenhof', 'Courtyard'], ['Pavillons', 'Pavilions'],
  ['Ein editierbarer Anfang. Du bestimmst danach Form, Räume und Gestaltung.', 'An editable starting point. You decide the shape, rooms and design.'],
  ['Spielbereiche', 'Game areas'], ['Erkunden', 'Explore'], ['Studium', 'Study'], ['KI-Begleitung', 'AI companion'],
  ['Bewegen', 'Move'], ['Linke Maustaste halten & ziehen · umsehen', 'Hold left mouse button & drag · look around'], ['E · Lernort', 'E · learning place'],
  ['Ansicht vergrössern', 'Zoom in'], ['Ansicht verkleinern', 'Zoom out'],
  ['Bauatelier öffnen', 'Open building studio'], ['Dein Grundriss. Deine Räume. Dein Weg.', 'Your layout. Your rooms. Your path.'],
  ['Bauatelier', 'Building studio'], ['Bauatelier schliessen', 'Close building studio'],
  ['Bachelorhaus', 'Bachelor house'], ['Masterhaus', 'Master house'],
  ['Dein Bachelorhaus', 'Your Bachelor house'], ['Dein Masterhaus', 'Your Master house'], ['Dein Psychologiecampus', 'Your psychology campus'],
  ['Grundriss', 'Layout'], ['Gestaltung', 'Design'],
  ['Gib deinem Raum einen Namen und wähle seine Grösse. Den Grundriss kannst du jederzeit verfeinern.', 'Name your room and choose its size. You can refine the layout at any time.'],
  ['Grundriss zeichnen & Geschosse', 'Draw the layout and choose floors'],
  ['Geschoss', 'Floor'], ['Erdgeschoss', 'Ground floor'], ['1. Obergeschoss', 'First floor'], ['2. Obergeschoss', 'Second floor'], ['3. Obergeschoss', 'Third floor'],
  ['Dein Bauplatz', 'Your building plot'], ['Zeichenwerkzeug', 'Drawing tool'], ['Wählen', 'Select'], ['Boden', 'Floor tile'], ['Entfernen', 'Remove'],
  ['Grundriss. Pfeiltasten wählen ein Feld, Leertaste verwendet das Zeichenwerkzeug.', 'Layout. Arrow keys select a tile; Space applies the drawing tool.'],
  ['Ein Feld entspricht 4 × 4 m. Bis zu 64 Felder über vier Geschosse. Obergeschosse brauchen einen Boden darunter.', 'Each tile is 4 × 4 m. Use up to 64 tiles across four floors. Upper floors need a supporting tile below.'],
  ['Mit einer Vorlage beginnen', 'Begin with a template'], ['Bauvorlage', 'Building template'], ['Haus mit Innenhof', 'Courtyard house'], ['Leerer Bauplatz', 'Empty plot'], ['Anwenden', 'Apply'],
  ['Eine Vorlage ersetzt den Bauplan dieses Hauses. Rückgängig ist möglich.', 'A template replaces this house’s plan. You can undo the change.'],
  ['Räume entwerfen', 'Design rooms'], ['Neuen Raum anlegen', 'Create a new room'], ['Raumname', 'Room name'],
  ['Breite in Feldern', 'Width in tiles'], ['Tiefe in Feldern', 'Depth in tiles'],
  ['2 × 2 Felder ergeben einen Raum von 8 × 8 m.', 'A 2 × 2 tile room measures 8 × 8 m.'],
  ['Position, Form & Funktion', 'Position, shape and purpose'], ['Funktion', 'Purpose'], ['Lernen', 'Learning'], ['Forschung', 'Research'], ['Austausch', 'Discussion'], ['Atelier', 'Studio'],
  ['Form', 'Shape'], ['Rechteck', 'Rectangle'], ['L-Form', 'L shape'], ['Y im Plan', 'Y on the plan'],
  ['Raum verschieben', 'Move room'], ['Raum nach links', 'Move room left'], ['Raum nach hinten', 'Move room back'], ['Raum nach vorne', 'Move room forward'], ['Raum nach rechts', 'Move room right'],
  ['90° drehen', 'Rotate 90°'], ['Raum entfernen', 'Remove room'], ['Räume verbinden', 'Connect rooms'],
  ['Türen verbinden Räume an einer gemeinsamen Wand. Treppen brauchen freie Felder genau übereinander und einen Zugang im unteren Raum.', 'Doors connect rooms along a shared wall. Stairs need clear tiles directly above each other and an entrance in the lower room.'],
  ['Verbindung', 'Connection'], ['Türdurchgang', 'Doorway'], ['Treppe', 'Stair'], ['Tür', 'Door'],
  ['Von Raum', 'From room'], ['Zu Raum', 'To room'], ['Verbindungsstelle', 'Connection point'], ['Verbindung hinzufügen', 'Add connection'], ['Vorhandene Verbindung', 'Existing connection'], ['Verbindung entfernen', 'Remove connection'],
  ['Ein gebautes Modul platzieren', 'Place a built module'], ['Modul', 'Module'],
  ['Wähle im Plan den Startpunkt. Das Modul wird dem gewählten Raum zugeordnet.', 'Choose a starting point on the plan. The module is assigned to the selected room.'],
  ['Hier platzieren', 'Place here'], ['Materialwelt', 'Material palette'], ['Warmes Holz', 'Warm timber'], ['Heller Stein', 'Light stone'],
  ['Fassade', 'Facade'], ['Offene Fensterachsen', 'Open window bays'], ['Geschlossene Flächen', 'Solid walls'], ['Glas', 'Glass'], ['Dach', 'Roof'], ['Flachdach', 'Flat roof'], ['Satteldach', 'Gabled roof'], ['Dachterrasse', 'Roof terrace'],
  ['Wandfarbe', 'Wall colour'], ['Akzentfarbe', 'Accent colour'], ['Gestaltung anwenden', 'Apply design'], ['Dächer in der Bauansicht zeigen', 'Show roofs in building view'],
  ['Einrichten', 'Furnish'], ['Wähle einen freien Punkt im Plan und stelle ein Objekt auf.', 'Choose a clear point on the plan and place an object.'],
  ['Objekt', 'Object'], ['Objekt aufstellen', 'Place object'], ['Aufgestellte Objekte', 'Placed objects'], ['Drehen', 'Rotate'], ['Tageszeit', 'Time of day'], ['Licht setzen', 'Set lighting'],
  ['Raum hinzufügen', 'Add room'], ['Passendes Modul entdecken', 'Discover a related module'], ['Letzte Bauänderung rückgängig', 'Undo the last building change'],
  ['Dein Lernpfad', 'Your learning path'], ['Wähle ein Modul. Das Quiz beginnt direkt.', 'Choose a module to open its quiz.'], ['Module schliessen', 'Close modules'], ['Module suchen', 'Search modules'], ['Modul suchen …', 'Search for a module …'],
  ['Eine Frage. Ein nächster Schritt.', 'One question. One next step.'], ['Hauptquests führen dich durch dein Studium. Nebenquests öffnen neue Blickwinkel.', 'Main quests guide you through your studies. Side quests offer new perspectives.'], ['Quests schliessen', 'Close quests'],
  ['Dein nächster Baustein', 'Your next building block'], ['Quiz schliessen', 'Close quiz'], ['Ein Baustein mehr in deinem Wissen.', 'Another building block for your knowledge.'], ['Antwort prüfen', 'Check answer'], ['Weiter', 'Continue'], ['Modul ins Haus bauen', 'Build module in the house'], ['Zurück ins Haus', 'Return to the house'], ['Modul verstehen', 'Understand this module'],
  ['Lernaufgabe im Spiel · kein Leistungsnachweis', 'In-game learning task · not an academic assessment'],
  ['So spielst du gern.', 'Make the game yours.'], ['Einstellungen schliessen', 'Close settings'], ['Sprache der Lerninhalte', 'Language'], ['Sprache', 'Language'], ['Lernmodus', 'Learning mode'],
  ['Frei bauen und entdecken', 'Build and explore freely'], ['Module mit Lernchecks freischalten', 'Unlock modules through learning checks'], ['Entwürfe zur KI-Integration anzeigen', 'Show proposed AI activities'], ['Direkt im Master beginnen', 'Begin directly in the Master programme'],
  ['Lautstärke', 'Volume'], ['Gesamtlautstärke', 'Master volume'], ['Atmosphäre', 'Ambience'], ['Effekte', 'Effects'], ['Schritte', 'Footsteps'], ['Textgrösse', 'Text size'], ['Bewegung reduzieren', 'Reduce motion'], ['Kontrast verstärken', 'Increase contrast'], ['Akustische Hinweise auch anzeigen', 'Also display sound cues'], ['Blickempfindlichkeit', 'Look sensitivity'], ['Einstellungen übernehmen', 'Apply settings'],
  ['Spielfortschritt wird auf diesem Gerät gespeichert. Das Kompetenzmodell und die KI-Lernaktivitäten sind Entwürfe.', 'Game progress is saved on this device. The competency framework and AI learning activities are proposals.'],
  ['Deine KI-Begleitung', 'Your AI companion'], ['KI-Begleitung schliessen', 'Close AI companion'],
  ['Eine zweite Perspektive auf deine Studienwahl, eine Rückfrage zu deinem Lernweg oder ein Anlass, genauer hinzusehen.', 'Explore another perspective on your study choices, ask about your learning path or examine an idea more closely.'],
  ['KI-Begleitung öffnen', 'Open AI companion'], ['Für diese Spielversion ist noch keine KI-Begleitung verbunden.', 'No AI companion is connected to this game version yet.'], ['Lernkontext kopieren', 'Copy learning context'],
  ['Du kannst deine Modulwahl und Spielübungen ausdrücklich kopieren und in der Begleitung importieren. Namen und eigene Notizen werden nicht übernommen.', 'You can choose to copy your module selections and game practice, then import them into the companion. Names and personal notes are excluded.'],
  ['KI-Begleitung als Claude-Artefakt', 'AI companion hosted as a Claude artifact'],
  ['Die Begleitung läuft in Claude. Es werden keine persönlichen Daten automatisch übertragen. Antworten werden durch KI erzeugt und müssen geprüft werden.', 'The companion runs in Claude. No personal data is transferred automatically. AI-generated answers need to be checked.'],
  ['Dein Lernweg im Überblick', 'Your learning path'], ['Was dein Haus verbindet.', 'What your house connects.'], ['Module, geplante Lerngelegenheiten und berufliche Tätigkeiten.', 'Modules, planned learning opportunities and professional activities.'],
  ['Studienübersicht schliessen', 'Close study overview'], ['Studienübersicht', 'Study overview'], ['Module', 'Modules'], ['Kompetenzprofil', 'Competency profile'], ['Berufskompass', 'Career compass'],
  ['Alles, was du bauen kannst.', 'Everything you can build.'], ['Die Modulübersicht verbindet deine Studienwahl mit den Bausteinen im Haus. Zu jedem Modul findest du den Quellenstand, Lernziele, KI-Entwürfe und den direkten Lerncheck.', 'The module overview connects your study choices to the building blocks in your house. Each module includes its source description, learning objectives, proposed AI activities and a direct learning check.'],
  ['Alle Module dieses Hauses ansehen', 'View all modules for this house'], ['Die angezeigten ECTS stammen aus der Studienplanung. Spielaktionen vergeben keine akademischen Credits.', 'ECTS values come from the study plan. Game actions do not award academic credits.'],
  ['Erkunde die Lerngelegenheiten deiner gebauten Module.', 'Explore the learning opportunities in your built modules.'], ['Wähle ein Berufsfeld. Die Tätigkeitskarten verbinden typische Aufgaben mit Kriterien und deinem Modulplan.', 'Choose a career area. The activity cards connect typical tasks to criteria and your module plan.'], ['Berufsfeld erkunden', 'Explore a career area'],
  ['Der gekennzeichnete UI-Test ist verbunden.', 'The labelled UI test is connected.'], ['Deine Spielwelt ist bereit.', 'Your world is ready.'],
  ['Oberflächenvorschau: Die Unity-Spielwelt ist hier nicht verbunden.', 'Interface preview: the Unity world is not connected here.'], ['Oberflächenvorschau · keine verbundene Unity-Spielwelt', 'Interface preview · no connected Unity world'],
  ['Hier beginnt dein Haus.', 'Your house begins here.'],
  ['Ein Ort für deine Forschungsfragen, Gespräche und nächsten Entdeckungen. Beginne mit einem Raum nach deinen Vorstellungen.', 'A place for your research questions, conversations and next discoveries. Begin with a room of your own design.'],
  ['Was möchtest du über Menschen verstehen? Gib dieser Neugier einen ersten Raum. Form und Grösse bestimmst du.', 'What would you like to understand about people? Give that curiosity its first room. You choose its shape and size.'],
  ['Ersten Raum entwerfen', 'Design your first room'], ['Zuerst einen Lerncheck ausprobieren', 'Try a learning check first'],
  ['Dein Raum wartet auf eine Idee.', 'Your room is waiting for an idea.'], ['Wähle einen Baustein aus deinem Studium und entdecke, was dahintersteckt.', 'Choose a building block from your studies and discover what it involves.'], ['Lerncheck öffnen', 'Open learning check'], ['Im freien Modus direkt bauen', 'Build directly in free mode'], ['Alle Module ansehen', 'View all modules'],
  ['Bring deinen Baustein ins Haus.', 'Bring your building block into the house.'], ['Lerncheck noch einmal öffnen', 'Open the learning check again'],
  ['Dein Wissen bekommt einen neuen Kontext.', 'Put your knowledge in a new context.'], ['Eine Lernquest verbindet dein Studium mit einer konkreten Frage. Erkunde einen neuen Blickwinkel auf Psychologie und KI.', 'A learning quest connects your studies to a concrete question. Explore a new perspective on psychology and AI.'], ['Lernquest starten', 'Start learning quest'], ['Haus erkunden', 'Explore the house'],
  ['Dein Haus verbindet viele Perspektiven.', 'Your house connects many perspectives.'], ['Erkunde deine Räume, gestalte sie weiter oder finde im Studium-Menü neue Zusammenhänge zwischen deinen Modulen.', 'Explore your rooms, develop their design or find new connections between modules in the Study menu.'], ['Studium öffnen', 'Open Study'],
  ['Mein Forschungslabor', 'My research lab'], ['Mein erster Lernraum', 'My first learning room'], ['Mein Lernraum', 'My learning room'], ['Mein Raum', 'My room'], ['Raum am Innenhof', 'Courtyard room'],
  ['Dein Haus hat bereits Räume. Vorlagen findest du im Bauatelier.', 'Your house already has rooms. You can find templates in the building studio.'],
  ['Studienplatz und Voraussetzungen', 'Study slot and prerequisites'], ['Die folgenden Abhängigkeiten stammen aus der Spielplanung. Sie sind keine geprüften Zulassungs- oder Buchungsregeln.', 'These dependencies come from the game plan. They are not verified admission or module-booking rules.'], ['Keine Modulvoraussetzung im Spielmodell.', 'No module prerequisite in the game model.'], ['Dieses Feld enthält eine Wahloption. Du kannst sie im Lerncheck unter «Modul verstehen» vor dem Bauen festlegen.', 'This slot has a module choice. Set it in the learning check under “Understand this module” before building.'],
  ['Das Modul wird vorbereitet.', 'The module is being prepared.'], ['Erst den Bachelorpfad abschliessen oder direkt im Master beginnen.', 'Complete the Bachelor path first, or choose direct Master entry.'], ['Bestätige deine Selbsteinschätzung im Modul.', 'Confirm your self-check in the module.'], ['Meistere zuerst den Lerncheck.', 'Complete the learning check first.'],
  ['Im Haus platziert', 'Placed in the house'], ['Gebaut · noch nicht im Bauplan platziert', 'Built · awaiting placement on the plan'], ['Lerncheck wiederholen', 'Repeat learning check'], ['Quiz starten', 'Start quiz'], ['Für diese Suche gibt es kein Modul.', 'No modules match this search.'],
  ['Dein roter Faden', 'Your main route'], ['Neue Blickwinkel', 'New perspectives'], ['Designentwurf', 'Design proposal'], ['Deine Aufgabe', 'Your task'], ['Dieser Schritt ist im aktuellen Lernpfad noch gesperrt.', 'This step is still locked in the current learning path.'], ['Nochmals erkunden', 'Explore again'], ['Quest beginnen', 'Begin quest'], ['Hauptquest', 'Main quest'], ['Nebenquest', 'Side quest'], ['Lerncheck', 'Learning check'],
  ['Lies die Erklärung. Mit «Weiter» gehst du zum nächsten Schritt.', 'Read the explanation, then select Continue for the next step.'], ['Der Lerncheck ist abgeschlossen.', 'The learning check is complete.'], ['Genau. Darauf kommt es an.', 'Exactly. That is the key point.'], ['Ein guter Moment zum Nachdenken.', 'A useful moment to reflect.'], ['Du hast alle Fragen gemeistert. Dein Wissen bleibt die Grundlage für deinen nächsten Schritt.', 'You have completed all the questions. Your knowledge is the foundation for your next step.'],
  ['Raum für dieses Modul entwerfen', 'Design a room for this module'], ['Modul platzieren', 'Place module'], ['Lernaufgabe im Spiel · Modul- und KI-Planungen als Designentwurf', 'In-game learning task · module and AI activities are design proposals'], ['Modul im Quellenstand', 'Module in the source version'], ['Wahlpflichtmodul', 'Elective module'], ['KI-Integration · Designentwurf', 'AI integration · design proposal'], ['Lernziele im Entwurf', 'Proposed learning objectives'], ['Ich habe meine Vorkenntnisse zu diesem Modul eingeschätzt.', 'I have reflected on my prior knowledge for this module.'], ['Dein Schwerpunkt', 'Your focus'], ['Vertiefung', 'Specialisation'], ['Thema', 'Topic'], ['Forschungsfrage', 'Research question'], ['Offen lassen', 'Leave open'],
  ['Wähle ein Haus zum Bauen.', 'Choose a house to build in.'], ['Dein Haus darf insgesamt bis zu 64 Bodenfelder haben. Entferne zunächst unbenutzte Felder.', 'Your house can have up to 64 floor tiles in total. Remove unused tiles first.'],
  ['Dein Raum ist bereit für einen Lernbaustein. Öffne einen Lerncheck oder gestalte deinen Grundriss weiter.', 'Your room is ready for a learning module. Open a learning check or continue designing your layout.'], ['Dein erster Raum: Name und Grösse wählen, dann hinzufügen. Den Grundriss kannst du später verfeinern.', 'For your first room, choose a name and size, then add it. You can refine the layout later.'], ['Jetzt Lerncheck öffnen', 'Open a learning check now'], ['Raum anpassen', 'Update room'], ['Modul auswählen', 'Select module'], ['Objekt auswählen', 'Select object'], ['Wähle Breite und Tiefe zwischen 1 und 8 Feldern.', 'Choose a width and depth between 1 and 8 tiles.'], ['Raum auswählen', 'Select room'],
  ['Wähle zwei unterschiedliche Räume.', 'Choose two different rooms.'], ['Keine passende Treppenstelle. Zwei Felder müssen frei übereinanderliegen; unten braucht es einen Zugang aus demselben Raum oder durch eine Tür.', 'No suitable stair location. Two tiles must be clear and directly aligned; the lower tile needs access from the same room or through a doorway.'], ['Keine freie gemeinsame Wand auf derselben Etage.', 'No clear shared wall on the same floor.'], ['Verbindung auswählen', 'Select connection'], ['Die Verbindungsstelle ist nicht mehr frei. Wähle eine passende Stelle.', 'That connection point is no longer clear. Choose another suitable point.'], ['Wähle zuerst eine vorhandene Verbindung.', 'Select an existing connection first.'], ['Wähle zuerst einen Raum.', 'Choose a room first.'], ['Der gedrehte Raum würde den Bauplatz verlassen.', 'The rotated room would extend beyond the plot.'], ['Wähle zuerst ein gebautes Modul.', 'Select a built module first.'], ['Wähle zuerst ein Objekt.', 'Choose an object first.'], ['Wähle zuerst ein aufgestelltes Objekt.', 'Select a placed object first.'],
  ['Pflanze', 'Plant'], ['Bank', 'Bench'], ['Leuchte', 'Lamp'], ['Pinnwand', 'Noticeboard'], ['Schreibtisch', 'Desk'], ['Stuhl', 'Chair'], ['Bücherregal', 'Bookshelf'],
  ['Du bestimmst, welche Frage du mitnimmst.', 'You choose which question to take with you.'], ['Falls die Einbettung eine Anmeldung verlangt oder leer bleibt, öffne die Begleitung separat.', 'If the embedded companion asks you to sign in or stays blank, open it separately.'], ['Die Begleitung öffnet separat. Die Einbettung ist für die veröffentlichte GitHub-Spielseite vorgesehen.', 'The companion opens separately. Embedding is intended for the published GitHub game page.'], ['Kopiert. Öffne in der KI-Begleitung «Spielstand importieren» und füge den Text dort ein. Der Import startet kein Gespräch.', 'Copied. Open “Import game context” in the AI companion and paste the text. Importing does not start a conversation.'],
  ['Die Lerninhalte konnten nicht geladen werden.', 'The learning content could not be loaded.'], ['Die Lerninhalte haben ein unbekanntes Format.', 'The learning content has an unknown format.'], ['Die Spielwelt konnte nicht geladen werden. Bitte prüfe die Verbindung und versuche es erneut.', 'The game world could not be loaded. Check your connection and try again.'],
]);

english.set('Die Treppe braucht zwei freie Felder genau übereinander und unten einen Zugang. Verschiebe belegende Module oder Möbel; erweitere bei Bedarf den unteren Raum oder ergänze eine Tür.', 'Stairs need two clear tiles directly above each other and an entrance below. Move occupying modules or furniture; enlarge the lower room or add a doorway if needed.');
const hostStairError = 'A staircase needs an accessible lower entry: enlarge its room or add a doorway first.';

export function translateShellText(value, language = 'de') {
  const text = String(value ?? '');
  if (language !== 'en' && text === hostStairError) return 'Die Treppe braucht unten einen Zugang. Erweitere zuerst den Raum oder ergänze eine Tür.';
  if (language !== 'en') return text;
  const trimmed = text.trim();
  return english.has(trimmed) ? text.replace(trimmed, english.get(trimmed)) : text;
}

// Capture only initial static markup. Updating text nodes preserves nested icons,
// input elements and listeners; user input and source-generated content are not walked.
export function createStaticShellLocalizer(root) {
  const entries = [];
  const walk = element => {
    if (['SCRIPT', 'STYLE', 'SVG'].includes(element.tagName)) return;
    for (const child of element.childNodes) {
      if (child.nodeType === 3 && english.has(child.textContent.trim())) entries.push({ node: child, source: child.textContent });
      else if (child.nodeType === 1) walk(child);
    }
    for (const attribute of ['aria-label', 'title', 'placeholder', 'alt']) {
      const source = element.getAttribute?.(attribute);
      if (source && english.has(source.trim())) entries.push({ node: element, attribute, source });
    }
  };
  walk(root.documentElement || root);
  return language => {
    for (const entry of entries) {
      const value = translateShellText(entry.source, language);
      if (entry.attribute) entry.node.setAttribute(entry.attribute, value);
      else entry.node.textContent = value;
    }
    const document = root.ownerDocument || root;
    document.documentElement.lang = language === 'en' ? 'en' : 'de';
    document.title = language === 'en' ? 'Your Kompetenzhaus · Psychology UZH' : 'Dein Kompetenzhaus · Psychologie UZH';
    document.querySelector('meta[name="description"]')?.setAttribute('content', language === 'en' ? 'Design your Kompetenzhaus and explore psychology, AI and scientific inquiry.' : 'Gestalte dein Kompetenzhaus und erkunde Psychologie, KI und wissenschaftliches Arbeiten.');
  };
}
