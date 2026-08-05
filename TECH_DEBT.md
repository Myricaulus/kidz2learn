# Tech Debt / Offene Themen

Sammelstelle für bekannte Baustellen, die nicht sofort angegangen werden, aber irgendwann
gemeinsam durchgegangen werden sollen. Neue Punkte einfach unten anhängen (fortlaufende Nummer),
erledigte Punkte nicht löschen sondern mit `[x]` abhaken und kurz vermerken, was gemacht wurde.

Geplanter Umzug: sobald `gh` (GitHub CLI) eingerichtet ist, wandern diese Punkte in echte
GitHub Issues. Bis dahin lebt die Liste hier.

---

## [ ] 1. TaskRegistry / AdaptiveTaskGenerator: Design ist "unharmonisch"

Der `AdaptiveTaskGenerator` (`Model/TaskGenerator.cs`) soll eigentlich innerhalb einer
`TaskDomain` selbstständig zwischen verschiedenen Task-Typen/Skills wählen. Aktuell ist das nur
teilweise der Fall: `SilbenChallenge.razor.cs` ruft `ChooseTaskAsync<SilbenTaskDefinition>(skill:
Skill.ReadPrecise)` mit explizit fest verdrahtetem Skill auf — das ist nur ein Workaround, kein
echtes automatisches Auswählen aus der Domain.

Zusätzlich ist die "weakest skills first"-Logik in `ChooseTaskAsync` aktuell auskommentiert
(`candidates.Where(d => d.Skills.Any(s => weakestSkills.Contains(s)))`), es wird also gar nicht
wirklich adaptiv nach Skill-Schwäche gefiltert, nur nach `DifficultyLevel` gewichtet.

**Ziel:** Ein saubereres Modell finden, wie Tasks aus einer Domain (oder einer Menge von Skills)
ausgewählt werden, ohne dass jede Page den Skill hart vorgeben muss.

**Ergänzung (Szenario-Testbarkeit):** `AdaptiveTaskGenerator` selbst ist über `ISkillMasteryStore` +
geseedeten `Random` schon szenario-testbar (siehe `Kidz2Learn.Tests/AdaptiveTaskGeneratorTests.cs`).
Die eigentliche Mastery-*Formel* (Basis-Delta nach `Difficulty`, `SkillRowFactor`, `TaskRowFactor`,
`TimeFactor`, Clamping) ist es nicht: Sie steckt komplett in `SkillMasteryStore.Adjust`
(`Model/Skills.cs`), fest verdrahtet gegen ein echtes `IndexedDbStore`.
`Kidz2Learn.Tests/FakeSkillMasteryStore.cs` zeichnet aktuell nur auf, *dass* `Adjust` aufgerufen
wurde, rechnet aber nichts nach – ein Szenario wie "0 Mastery, immer richtig, Schwierigkeit soll
exponentiell steigen bis zum Plateau des tatsächlichen Nutzerlevels" lässt sich damit nicht
durchspielen. Fix: die reine Rechenlogik aus `SkillMasteryStore.Adjust` in eine I/O-freie Einheit
extrahieren (z.B. `MasteryMath.Adjust(SkillState current, Difficulty difficulty, int timeMs,
Kompetenzniveau taskHistory, bool success) -> SkillState`), `SkillMasteryStore.Adjust` wird dann nur
noch dünne I/O-Glue (Laden → `MasteryMath.Adjust` → Speichern) drumherum. Damit kann ein
In-Memory-Store die *echte* Formel über beliebig viele simulierte Runden laufen lassen, ganz ohne
Browser/IndexedDB – Voraussetzung für die geplanten Szenario-Testläufe, mit denen die
Task-Auswahl-Mechanik selbst optimiert werden soll (siehe Punkt 7).

---

## [x] 2. Bug: SilbenChallenge — `LeseAufgaben` IndexedDB Store nicht bekannt

Fehler in der Konsole: `DOMException: IDBDatabase.transaction: 'LeseAufgaben' is not a known
object store name`. Dadurch wird die Kompetenz-Historie in der SilbenChallenge nicht korrekt
geschrieben.

**Wahrscheinliche Ursache (schon eingegrenzt):** In `Program.cs` wird `AddIndexedDb("AufgabenDB",
[...], version)` mit einer festen Versionsnummer aufgerufen. Der Store `LeseAufgaben` wurde in
Commit `eb78566` ("Add Reading to Task-Logger") zur Store-Liste hinzugefügt, **ohne** die
`version` von 2 auf 3 zu erhöhen (vorher wurde in Commit `498186b` schon `SkillMeta`/
`SkillStates` bei `version: 2` angelegt). IndexedDB legt neue Object Stores aber nur beim
`onupgradeneeded`-Event an, das nur bei einer *Versionserhöhung* feuert. Jeder Browser, der die DB
bereits einmal bei Version 2 (ohne `LeseAufgaben`) geöffnet hatte, bekommt den neuen Store also nie
angelegt.

**Vermutlicher Fix:** `version` in `Program.cs` von 2 auf 3 erhöhen. Sollte das nicht reichen,
prüfen ob `Tavenem.Blazor.IndexedDB` das Upgrade-Handling überhaupt korrekt weiterreicht.

**Erledigt:** `version` in `Program.cs` auf `3` erhöht, damit `onupgradeneeded` bei bereits auf
Version 2 initialisierten DBs erneut feuert und `LeseAufgaben` nachträglich anlegt.

---

## [ ] 3. Zukunftsprojekt: "Allgemeiner Modus" beim Start

Ursprüngliche Idee: Beim Start soll ein allgemeiner Modus automatisch anhand der
Skill-Mastery-Historie (`SkillMasteryStore`) die passende `TaskDomain`/das passende Aufgabengebiet
auswählen, statt dass der Nutzer eine Challenge-Seite manuell anwählt. Das ist explizit ein
Langzeitprojekt, keine akute Baustelle — hier nur als Erinnerung geparkt.

---

## [ ] 4. Dynamische Page für mehrere Task-Typen (z.B. Silben- + Graphem-Challenge)

Eine einzelne Page soll dynamisch verschiedene Aufgabentypen bedienen können, z.B.
`SilbenChallenge` und `GraphemChallenge` in einer gemeinsamen Page, statt als zwei getrennte
Routen/Komponenten. Hängt vermutlich mit Punkt 1 zusammen (sauberere Task-Auswahl-Architektur
wäre Voraussetzung).

---

## [ ] 5. Neue Task-Arten (z.B. TurboArithChallenge) nicht vollständig unterstützt

**Konkretisiert (im Rahmen von TASK_PRESENTATION_REDESIGN.md Phase 4/5):** `SilbenChallenge`,
`GraphemChallenge` und `ArithmeticChallenge` laufen inzwischen alle über `TaskHost` +
`ITaskView`-Komponenten (`ChosenTask` rein, `OnNext` raus, ein neuer Pick pro Aufgabe).
`TurboArithChallenge` (`Pages/ArithmeticChallenge/TurboArithChallenge.razor`) passt strukturell
**nicht** in diesen Vertrag: es ist eine feste 3-Minuten-Zeitrunde auf einem einzigen, fest
verdrahteten Skill (`Turbo10`, kein Re-Pick pro Aufgabe - `ArithTaskRegistry.Turbo` bleibt bewusst
getrennt von `Simple`/`All`), mit eigenem Start/Running/Zusammenfassung-Zustand und
Punktevergabe am Rundenende statt pro Aufgabe. Bleibt deshalb bewusst eine eigene, nicht migrierte
Seite/Route.

Der User hat dazu eine konkrete Weiterentwicklungsidee: Turbo soll kein Sonderfall bleiben, sondern
zu einem allgemeinen "Event"-Baustein werden, den der Mixer/`TaskHost` selbst mitten in einer
normalen Session auslösen kann (z.B. nach ein paar normalen Aufgaben unangekündigt 20 Sekunden
Turbo einstreuen, danach wieder normale/andere Aufgabentypen, mit einstellbarer Wahrscheinlichkeit).
Ausführlich festgehalten in TASK_PRESENTATION_REDESIGN.md unter "Offener Punkt: Event-artige
Session-Bausteine im Mixer" - eigenes Konzept/eigene Phase, nicht Teil der aktuellen Umbauphase.

---

## [x] 6. Bug: `RingBufferJsonConverter` verliert Einträge beim Laden aus IndexedDB

Gefunden beim Aufsetzen von `Kidz2Learn.Tests` (siehe `RingBufferTests.cs`, zwei Tests sind
bewusst mit `Skip` markiert, um die Suite grün zu halten).

`RingBuffer<T>` wird über `RingBufferJsonConverter<T>` serialisiert/deserialisiert, u.a. für
`SkillState.AttemptsHistory` (`Entities/SkillStates.cs`), die bei jedem `SkillMasteryStore.Adjust`
in IndexedDB gespeichert und beim nächsten Laden über `GetItemAsync<SkillState>` wieder
deserialisiert wird.

**Bug 1 (Haupteffekt, immer):** Der Deserialisierungs-Konstruktor `RingBuffer(int maxItemCount,
List<T> items, int itemStart)` (`Shared/RingBuffer.cs`) setzt
`Count = Math.Min(maxItemCount, items.Count) - 1;` — unabhängig davon, ob schon ein Wraparound
stattgefunden hat. Das `-1` ist ein reiner Off-by-one-Fehler: nach **jedem** Laden aus IndexedDB
fehlt der zuletzt hinzugefügte Eintrag der History.

**Bug 2 (zusätzlich, nach einem Wraparound):** `Write()` serialisiert die Items bereits in
logischer Reihenfolge (über den Indexer), aber der Konstruktor legt sie beim Lesen wieder ab
Rohindex 0 im neuen Array ab und übernimmt trotzdem das alte physische `itemstart` aus dem JSON.
Nach einem Wraparound (`Itemstart != 0`) zeigt der Indexer dann auf falsche/verschobene Positionen
— zusätzlich zum Off-by-one-Verlust aus Bug 1.

**Vermutlicher Fix:** In der Praxis reicht es vermutlich, `RingBufferJsonConverter.Write` weiterhin
in logischer Reihenfolge zu schreiben, aber beim Lesen `itemstart` einfach auf `0` zu setzen (die
Items liegen ja schon in der richtigen Reihenfolge ab Index 0) und `Count` ohne `-1` zu berechnen.

**Impact:** Betrifft aktuell "nur" `SkillState.AttemptsHistory`, die laut den TODOs in
`SkillMasteryStore.Adjust` (Model/Skills.cs) ohnehin noch nirgendwo ausgewertet wird (geplant für
Bayesian Knowledge Tracing / `AttemptFailReason`-Inferenz). Kein akuter Nutzer-Impact, aber sobald
diese History für irgendetwas verwendet wird, ist sie stillschweigend korrupt.

**Erledigt:** `RingBuffer(int, List<T>, int)` setzt `Itemstart` jetzt fest auf `0` (die
übergebenen `items` liegen durch `RingBufferJsonConverter.Write` bereits in logischer Reihenfolge,
der alte physische `itemstart` ist dagegen wertlos) und berechnet `Count` ohne das `-1`. Die beiden
zuvor geskippten Tests in `RingBufferTests.cs` sind entsperrt und grün.

---

## [ ] 7. Zukunftsprojekt: Mastery-History für dynamische Schwierigkeit/Punkte/"Events"

Beim Konzipieren von `TASK_PRESENTATION_REDESIGN.md` aufgekommen, aber bewusst nicht Teil dieses
Umbaus. Ideensammlung für ein späteres, eigenes Vorhaben:

- **Pro-Skill/Pro-Task-Typ-Historie**, die die Mastery-Logik auswertet, um z.B. zu erkennen, ob
  ein länger nicht trainierter ("alter") Skill mal wieder abgefragt werden sollte, oder ob die
  Schwierigkeit vorübergehend gesenkt werden muss, weil Frustration erkannt wurde (Signale dafür:
  Zeit pro Versuch, Fehlerlog – beides selbst noch offen, siehe `SkillMasteryStore`-TODOs in
  CLAUDE.md zu Bayesian Knowledge Tracing/Fail-Reason-Inferenz). Bug #6 (`RingBuffer`) betraf
  bereits die Datenstruktur, die dafür in Frage käme.
- **Dynamische Punktvergabe im `TaskChooser`/Picker nach effektiver Schwierigkeit** statt fester
  Werte pro Task-Definition (siehe Baustein 6 in `TASK_PRESENTATION_REDESIGN.md`): unerfahrene Kinder
  sollen für dieselbe Aufgabenkategorie mehr Punkte bekommen als schon fortgeschrittene, ggf. auch
  gezielt Bonuspunkte bei erkannter Frustration.
- **Zufällig eingestreute "Events" durch den Picker**, um Monotonie entgegenzuwirken – Teil des
  Wunsches, das Lernkonzept maximal gamifiziert zu halten.
- Hängt lose mit Punkt 1 (Picker-Design) und Punkt 3 ("Allgemeiner Modus") zusammen, ist aber deutlich
  größer als beide und sollte als eigene Phase *nach* dem Presentation-Redesign angegangen werden.

---

## [ ] 8. Bug: `WordMeta.Data` enthält "dort"/"Dort" als zwei separate Wörter

Beim manuellen Testen von Phase 3a gefunden (siehe TASK_PRESENTATION_REDESIGN.md): Bei einer
`read_syllables`-Aufgabe tauchten "dort" und "Dort" gleichzeitig als zwei unterschiedliche Optionen
auf. Ursache bestätigt in `Model/WordMeta.g.cs`: `["Dort"]` und `["dort"]` sind zwei separate
Dictionary-Einträge (Zeilen 89 und 185), beide mit identischem IPA (`dˈɔɾt`) und Tag (`adv`) - ein
reiner Case-Duplikat aus der `WaveSplit/`-Datenpipeline. Betrifft vermutlich weitere Wortpaare, nicht
nur dieses eine.

**Vermutlicher Fix:** Beim nächsten Lauf von `WaveSplit/DeduplicateNames.py` (oder einer Erweiterung
davon) case-insensitive statt case-sensitive deduplizieren. Nicht händisch in `WordMeta.g.cs`
gepatcht, weil das laut CLAUDE.md bei der nächsten Regenerierung ohnehin überschrieben würde (alles
nach dem `// ###Endmarker for replacement###`-Marker).

---

## [ ] 9. Bug: `Logger.Erfolgreich`/`GesamtAnzahl` driften seitenübergreifend, HUD zeigt z.B. "800%"

Ebenfalls beim manuellen Testen von Phase 3a gefunden: Nach Seitenwechsel zeigte die HUD-Anzeige oben
rechts absurde Werte wie "800% richtig".

**Ursache bestätigt:** `LoggerService.Erfolgreich`/`GesamtAnzahl` (`Services/LoggerService.cs`) sind
öffentliche Felder auf einem **Singleton**-Service, geteilt über alle Pages hinweg.
`LiveLogger.razor:5` rendert `Logger.Erfolgreich` als Prozentwert: `(Logger.Erfolgreich * 100)`,
implizit vorausgesetzt, `Erfolgreich` sei ein 0..1-Verhältnis - so wie
`ArithmeticChallenge.razor.cs` es auch befüllt (`Logger.Erfolgreich = stats.RichtigProzent()`, ein
Bruch, plus Reset in `OnParametersSetAsync` bei jedem Seitenaufruf aus der persistierten
`ArithemticLogStats`). `SilbenChallenge`/`GraphemChallenge` behandeln dasselbe Feld dagegen als
**absoluten Zähler** (`Logger.Erfolgreich++`) und resetten ihn nie beim Seitenbetreten - daher
z.B. `8 * 100 = 800%` nach ein paar richtigen Silben-Antworten. Direkte Konsequenz der in Baustein 6
(`TASK_PRESENTATION_REDESIGN.md`) und Punkt 4 dieser Liste beschriebenen Lücke: Silben hat kein
Äquivalent zu `ArithemticLogStats`, füllt das Feld deshalb komplett anders.

**Vermutlicher Fix:** Gehört eigentlich in dieselbe Baustelle wie die Aggregat-Stats-Lücke (Punkt 4,
Baustein 6) - `Erfolgreich` bräuchte eine einheitliche Semantik (Verhältnis, nicht gemischt
Verhältnis/Zähler) plus einen Reset-Mechanismus, der nicht jede Page einzeln nachbauen muss. Kein
Quick-Fix im Rahmen des aktuellen Umbaus, siehe dort.

Bestätigt keine Regression durch die heutigen Änderungen (Phase 1-3a) - beide Stellen unverändert.

---

## [x] 10. Bug: `Kompetenzniveau.Versuche`/`Richtig` überlebten IndexedDB-Roundtrip nicht

Beim manuellen Testen von ArithmeticChallenge gefunden: dieselbe Aufgabe >20x hintereinander falsch
gelöst, trotzdem stieg die im Feedback angezeigte Versuchszahl nicht an, und die %-Anzeige blieb bei
"--%" statt ab dem 5. Versuch einen Wert zu zeigen. Erst vermutet als Regression aus der
`RingBuffer`-Änderung (Punkt 6) - **war es nicht**: `Kompetenzniveau` nutzt gar keinen `RingBuffer`,
sondern einen simplen String (`Historie`) plus zwei `int`-Properties.

**Ursache bestätigt (per Repro mit `System.Text.Json` isoliert nachgestellt):**
`Versuche`/`Richtig` hatten `{ get; private set; }`. System.Text.Jsons Standard-Reflektions-Deserializer
befüllt ohne `[JsonInclude]` nur Properties mit **öffentlichem** Setter - `Historie` (öffentlicher
Setter) überlebte jeden IndexedDB-Roundtrip korrekt, `Versuche`/`Richtig` wurden dagegen bei jedem
`GetItemAsync<...Log>(id)` still auf `0` zurückgesetzt (die Serialisierung selbst schrieb die
richtigen Werte raus, nur das Zurücklesen ging verloren). Da sowohl `ArithmeticChallenge.Evaluate`
als auch `SilbenChallenge`/`GraphemChallenge`.`CheckAnswer` das Log-Entity bei **jedem einzelnen
Versuch** frisch aus IndexedDB laden (nicht nur beim Seitenaufruf), blieb `Versuche` faktisch immer
bei 1 hängen - `GetProzent()`s Schwelle (`Versuche >= 5`) wurde nie erreicht. Betraf alle drei
Challenge-Pages gleichermaßen, nicht nur Arithmetik (dort nur zuerst auffällig, weil 20x dieselbe
Aufgabe wiederholt wurde).

**Erledigt:** `[JsonInclude]` auf beide Properties ergänzt (`Model/Kompetenzniveau.cs`), Verhalten
per Repro-Skript vor/nach dem Fix verifiziert. Neuer Regressionstest
`KompetenzniveauTests.JsonRoundTrip_PreservesVersucheAndRichtig` (Serialisieren + Deserialisieren
direkt, ohne IndexedDB/JSRuntime - reiner `System.Text.Json`-Test, kein IndexedDB-Zugriff nötig).
Build + volle Testsuite (60 Tests) grün.

---

## [ ] 11. Bug: Markier-Popup lässt sich während des Spicken-Hovers blind bedienen

Beim manuellen Testen von `/` (nach dem TaskHost-Cutover) gefunden, **keine Regression durch den
Umbau** - Verhalten war schon im alten `SilbenChallenge.razor` identisch (Popup wurde in Phase 4b
nur wortwörtlich nach `MarkierPopup.razor` verschoben).

Hält man die Maus auf dem grünen "richtiges Wort"-Kästchen (3 Sek. zum Spicken), blendet
`MarkierPopup.razor` das falsche Wort per CSS aus (`.k4l-correct-box:hover ~ .k4l-wrong-reveal-wrap
.k4l-wrong-word-box { opacity: 0; ... }`, Zeile ~246). `opacity: 0` deaktiviert aber keine
Pointer-Events - die `<span class="k4l-letter">`/Lücken-`<span>`s darunter (`@onclick="() =>
OpenLetterEdit(li)"` bzw. `OpenGap(gi)`, Zeile 79/50/54) bleiben normal klickbar, nur unsichtbar.
Ein Kind kann also während des Spickens weiter (blind) Buchstaben markieren/korrigieren, an
Positionen, die es sich vom vorherigen sichtbaren Zustand gemerkt hat oder einfach durchprobiert.

**Vermutlicher Fix:** `pointer-events: none` auf `.k4l-wrong-word-box` ergänzen, solange sie über
den Hover-Trigger ausgeblendet ist (gleiche Selektor-Kombination wie die bestehende
`opacity: 0`-Regel).

---

## [ ] 12. Bug: Markier-Popup akzeptiert nur eine von mehreren gültigen Korrekturen bei mehrdeutigem Alignment

Beim manuellen Testen von `/` gefunden, **keine Regression durch den Umbau** - `WordDiff.cs` und die
`OnWeiterClicked`-Prüflogik sind seit Phase 4b unverändert nach `MarkierPopup.razor.cs` verschoben.

Konkretes Repro: falsches Wort "anfasssen" (drei "s" statt zwei) - das letzte "s" als überzählig
markiert wurde abgelehnt, nur das erste "s" wurde akzeptiert.

**Ursache:** `WordDiff.Align` (`Model/WordDiff.cs` bzw. `Pages/SilbenChallenge/WordDiff.cs`) ist
Standard-Wagner-Fischer mit Backtracking in fester Prioritätsreihenfolge (Match → Substitute →
Delete → Insert). Bei mehreren gleich guten Alignments - z.B. drei aufeinanderfolgenden identischen
Buchstaben, von denen einer zu viel ist - liefert das Backtracking deterministisch **genau eine**
Lösung (hier: das erste "s"), nicht die Menge aller gleichwertigen Alternativen.
`MarkierPopup.OnWeiterClicked` (`Components/TaskViews/MarkierPopup.razor.cs:142`) prüft dann per
`_markedIndices.SetEquals(_requiredMarks)` exakt gegen **dieses eine** Alignment, statt zu prüfen,
ob das tatsächliche Ergebnis der Kind-Korrektur (Marks/Substitutionen/Lücken angewandt auf
`WrongWord`) zu `CorrectWord` führt. Bei Wörtern mit wiederholten Buchstaben (Doppel-/Dreifach-
Konsonanten, worauf Deutsch als Sprache besonders anfällig ist) gibt es das oft mehrfach.

**Vermutlicher Fix:** Zwei unabhängige Bausteine, wahrscheinlich beide nötig:
1. `WordDiff.Align` so erweitern, dass es bei Ties alle gleichwertigen Alignments liefert (oder
   zumindest eine Kanonisierung, die bei Wiederholungsgruppen konsistent das "sinnvollste" wählt),
   statt sich nur auf die Backtracking-Reihenfolge zu verlassen.
2. `OnWeiterClicked`s Prüfung ergebnisorientiert machen: Marks/Substitutionen/Lücken auf `WrongWord`
   anwenden und das Resultat gegen `CorrectWord` vergleichen, statt Index-für-Index gegen ein
   einziges vorab berechnetes Alignment zu prüfen - das akzeptiert automatisch jede korrekte
   Lösung, unabhängig davon, welche der mehreren möglichen Alignments `WordDiff` intern gewählt
   hat.

---

## [ ] 13. Bug: Debug-Override zeigt bei `skill=read_precise` 5 statt 3 Optionen

Beim manuellen Testen von `/debug/SilbenChallenge?task=silben&word=Sonnensystem&skill=read_precise`
gefunden - **nicht** vom TaskHost-Umbau verursacht, `SilbenDebugOverride.cs` war hiervon inhaltlich
unberührt (nur der `OfType`-Filter kam dazu).

**Ursache:** `SilbenDebugOverride.TryForce` (`Model/Tasks/SilbenDebugOverride.cs`) generiert die
Distraktoren ohne `options=`-Parameter hart mit `ErstleserDistraktorGenerator.Generate(target, 4,
Random.Shared)` → 4 Distraktoren + Zielwort = 5 Optionen, **unabhängig davon, welcher Skill
erzwungen wird**. Die echten Generatoren in `SilbenTaskRegistry.cs` liefern aber je nach Skill
unterschiedlich viele Optionen: `read_precise` nutzt `ErstleserDistraktorGenerator.Generate(...,
2, r)` → 3 Optionen, `GraphemPhonem` ebenfalls 3, `read_syllables` dagegen 6. Die hartcodierte `4`
im Debug-Override passt zu keinem der drei.

**Vermutlicher Fix:** Distraktor-Anzahl im Override von der jeweils erzwungenen
`SilbenTaskDefinition` ableiten statt hartcodiert - am saubersten, indem die Definition selbst (oder
ihr `Generator`) die erwartete Optionsanzahl exponiert, statt sie im Debug-Code zu erraten.
