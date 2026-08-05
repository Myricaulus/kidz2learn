# Konzept: Task-Auswahl und -Darstellung entkoppeln

Status: Umsetzung läuft inkrementell, siehe Phasenplan unten. Dient als Grundlage für ein
inkrementelles Umbauvorhaben.

## Manuelle Abnahme-Checkliste

UI-Verhalten wird nicht automatisiert durch Claude im Browser getestet (siehe
[[feedback_no_playwright_ui_testing]]) - hier sammeln sich stattdessen die Punkte, die du selbst
einmal durchklicken solltest, sobald du Zeit hast. Build + volle Unit-Testsuite sind für jeden
Punkt unten bereits grün.

- [x] **SilbenChallenge, `read_syllables`/`GraphemPhonem`:** eine falsche Option anklicken, danach
  die richtige. Erwartet: Feedback "Nochmal versuchen!" bei Falsch, danach normaler Ablauf wie
  bisher (keine sichtbare Änderung erwartet - der Unterschied ist nur, dass jetzt auch bei der
  falschen Antwort ein `SkillState`-Adjust in IndexedDB passiert, sichtbar z.B. via
  `SkillMasteryStore`s `Console.WriteLine` in der Browser-Devtools-Konsole). **Verifiziert:** Popup,
  Meldung und Konsole sauber. Dabei zwei vorbestehende, unabhängige Bugs gefunden und in
  TECH_DEBT.md #8/#9 geparkt (Case-Duplikat "dort"/"Dort" in `WordMeta.Data`; `Logger.Erfolgreich`
  driftet seitenübergreifend, HUD zeigt teils >100%) - keine Regression durch Phase 1-3a.
- [x] **SilbenChallenge, `read_precise`:** falsche Option anklicken → Markier-Popup öffnet sich wie
  bisher, Ablauf/Optik unverändert. **Verifiziert.**
- [x] **GraphemChallenge:** dieselbe Prüfung wie oben (falsch → richtig), keine sichtbare
  Verhaltensänderung erwartet. **Verifiziert.**
- [x] **Nach Phase 3b (Glue-Extraktion via `TaskSessionController`):** kurzer Spotcheck
  SilbenChallenge/GraphemChallenge (falsch → richtig, Punkte/Sound wie gehabt) - Logik unverändert,
  nur in `TaskSessionController.RecordSuccess`/`RecordFailure` verschoben, geringes Risiko.
  **Verifiziert** (im Rahmen des Arithmetik-Tests unten mitgeprüft).
- [x] **ArithmeticChallenge, Addition und Subtraktion:** einmal falsch tippen, dann richtig lösen.
  Erwartet: Punkte/Sound/Combo-Verhalten wie bisher (−5/0 bei Falsch, Combo-Reset; +2/8 bei Richtig,
  Combo-Erhöhung, neue Aufgabe). **Verifiziert**, dabei aber Finding #10 aufgedeckt: dieselbe Aufgabe
  20x hintereinander falsch gelöst → Versuchszähler im Feedback stieg nicht, %-Anzeige blieb bei
  "--%". **War kein Phase-1-3b-Regression** (Ursache unabhängig, `Kompetenzniveau`-Properties ohne
  `[JsonInclude]` verloren ihren Wert bei jedem IndexedDB-Reload) - Root Cause bestätigt und
  gefixt, siehe TECH_DEBT.md #10.
- [x] **Nach Fix #10:** kurzer Re-Check ArithmeticChallenge (gerne wieder dieselbe Aufgabe mehrfach
  falsch lösen) - Versuchszähler sollte jetzt hochzählen, ab dem 5. Versuch sollte eine %-Anzeige
  erscheinen statt "--%". **Verifiziert.**
- [x] **Nach Phase 4c (Cutover):** `/` selbst durchgeklickt - Wort wird beim Laden automatisch
  abgespielt, "Anhören"-Button, Options-Grid, Richtig/Falsch-Feedback, Markier-Popup inkl.
  Hover-Timer-Verhalten, Fortlaufen zur nächsten Aufgabe. **Verifiziert**, keine Regression durch
  den Umbau selbst. Dabei zwei vorbestehende, unabhängige Bugs im Markier-Popup gefunden und in
  TECH_DEBT.md #11/#12 geparkt (Popup während des Spicken-Hovers blind bedienbar; bei mehrdeutigem
  Buchstaben-Alignment - z.B. "anfasssen" - wird nur eine von mehreren gültigen Korrekturen
  akzeptiert).
- [x] **`/debug/SilbenChallenge?task=silben&word=...`:** Debug-Override greift wie erwartet nach dem
  Umstieg auf `ChooseAnyAsync`. **Verifiziert.** Dabei eine unabhängige Kleinigkeit gefunden (nicht
  vom Umbau verursacht): bei `skill=read_precise` zeigt es 5 statt der normalen 3 Optionen - siehe
  TECH_DEBT.md #13.
- [x] **`/graphem` nach dem Cutover (4d):** kein "Anhören"-Button, kein Autoplay, Options-Grid/Feedback
  wie gehabt - kein Audio abgespielt. **Verifiziert**, aber eine echte Regression gefunden: die Musik
  wurde nach dem Verlassen einer audio-tragenden Seite nicht mehr auf volle Lautstärke
  zurückgesetzt, wenn man auf `/graphem` landete. Ursache: `SilbenMultipleChoiceView.OnInitializedAsync`
  duckte unconditional auf `Player.SetVolume(0.1)`, unabhängig vom neu eingeführten `_hasAudio` -
  Restore passierte vorher indirekt über `DisposeAsync` der *vorherigen* Seite, aber die neue Seite
  duckte sofort wieder runter. **Gefixt:** Ducken/Restore jetzt in `OnParametersSetAsync` an
  `_hasAudio` gekoppelt (`SetVolume(_hasAudio ? 0.1 : 1.0)`), nicht mehr pauschal in
  `OnInitializedAsync` - nötig sowieso für `/deutsch-mix`, wo Audio- und stille Aufgaben
  task-für-task wechseln können. Build + volle Testsuite (64) grün.
- [x] **`/ArithmeticChallenge` nach dem Cutover (4d):** Ziffern-Grid + virtuelles Numpad, einmal
  falsch dann richtig gelöst - Verhalten wie vor dem Umbau. **Verifiziert**, keine Regression.
- [x] **Nachtrag zum `/graphem`-Fund oben - Lautstärke-Fix war selbst noch fehlerhaft:** nach dem
  ersten Fix (`SetVolume(_hasAudio ? 0.1 : 1.0)`) wurde die Musik auf stillen Seiten zwar nicht
  mehr leise gelassen, aber jetzt hart auf 100% gesetzt - auch wenn der Nutzer selbst über den
  `SidPlayerWidget`-Slider eine andere Lautstärke eingestellt hatte. **Root Cause:** `SidWidgetService`
  hatte nur ein einziges `_volume`-Feld, das sowohl von der manuellen Slider-Bedienung als auch vom
  Ducking der Challenge-Seiten überschrieben wurde - keine Unterscheidung zwischen "Nutzer hat das
  bewusst gewählt" und "Seite duckt gerade temporär". Vorbestehend, keine Neuerfindung durch diesen
  Umbau (die alte `SilbenChallenge.DisposeAsync` hatte exakt dasselbe Problem, hart `SetVolume(1.0)`,
  nur seltener ausgelöst, da nur beim Seitenwechsel statt bei jedem Aufgabenwechsel). Zusätzlich kam
  heraus, dass der Slider in `SidPlayerWidget.razor` (`MudSlider`) direkt eine lokale Methode rief
  und `SidWidgetService` über manuelle Änderungen nie informierte - selbst wenn ein Restore
  "auf den letzten Wert" versucht hätte, hätte es den falschen Wert gehabt.
  **Gefixt:** `SidWidgetService` unterscheidet jetzt `_baseVolume` (Nutzerwahl, via `SetVolume`) von
  `_duckedVolume` (temporär, via neue `Duck`/`Restore`-Methoden) - `Volume` liefert `_duckedVolume ??
  _baseVolume`. `SilbenMultipleChoiceView` ruft jetzt `Duck(0.1)`/`Restore()` statt `SetVolume(0.1)`/
  `SetVolume(1.0)`. `SidPlayerWidget`s Slider ruft jetzt `Player.SetVolume(...)` statt einer rein
  lokalen Methode, damit manuelle Änderungen im Service ankommen. Build + volle Testsuite (64) grün.
- [x] **`/mathe-mix`, `/deutsch-mix`, `/bestenmix` (Phase 5/6, neue Menüpunkte):** durchgeklickt -
  plausible gemischte Aufgaben aus dem erwarteten Skill-Pool, kein Crash, kein Turbo10-Kandidat
  aufgetaucht. Live-Logger zeigt die gemischten Aufgaben sauber an. **Verifiziert**, dabei aber ein
  deutlicher Fund: **Combo-Counter zählte in Deutsch-Mix und `/` gar nicht, und setzte sich in
  Bestenmix jedes Mal zurück, wenn von einer Deutsch- auf eine Mathe-Aufgabe gewechselt wurde.**
  Zwei unabhängige Root Causes:
  1. `HudStateService` (Combo) wurde bisher nur von `ArithmeticChallenge`/`TurboArithChallenge`
     direkt angefasst - Silben/Graphem haben `Hud.IncrementCombo()`/`SetCombo(0)` nie aufgerufen,
     weder vorher noch nach dem Cutover (keine Regression, aber eine echte Lücke, jetzt erstmals
     durch den gemeinsamen Mix sichtbar). **Gefixt:** in `TaskSessionController.RecordSuccess`/
     `RecordFailure` verschoben - genauso domänenunabhängig wie `Score.AddPoints`, läuft jetzt für
     jede View. `ArithNumpadView`s direkte `Hud.IncrementCombo()`/`SetCombo(0)`-Aufrufe entfernt
     (sonst doppelt gezählt).
  2. **Echte Regression:** `ArithNumpadView.OnInitializedAsync` rief `Hud.ResetAll()` - unter der
     alten Seiten-Architektur lief das genau einmal pro Seitenbesuch. Unter `TaskHost` mit
     `DynamicComponent` wird die View-Komponente aber bei jedem Typwechsel (z.B. Bestenmix:
     "silben-multiple-choice" → "arith-numpad") neu instanziiert, `OnInitializedAsync` feuert also
     bei jedem Domänenwechsel erneut und hat den Combo mittendrin gekillt. **Gefixt:** `Hud.ResetAll()`
     nach `TaskHost.OnInitializedAsync` verschoben - das ist die einzige Komponente, die wirklich nur
     einmal pro Seitenbesuch instanziiert wird, unabhängig davon, wie oft die View darunter wechselt.
  Build + volle Testsuite (64) grün.
  
  **Notiert, nicht gefixt (Produkt-/Tuning-Frage, keine Regression):** Lautstärke-Duck/Restore
  wechselt in Deutsch-Mix jetzt (korrekterweise) mit jeder Aufgabe zwischen leise/normal, was beim
  schnellen Aufgabenwechsel als störend empfunden werden kann. Funktioniert wie entworfen, aber
  eventuell zu abrupt - z.B. eine sanfte Überblendung statt Sofort-Umschaltung wäre eine Option,
  falls gewünscht.

## Problem

Eine Challenge-Page (`SilbenChallenge`, `ArithmeticChallenge`, ...) ist heute drei Dinge gleichzeitig,
fest verdrahtet:

1. **Welche Task-Typen sie anfragt** – `SilbenChallenge` ruft hart
   `ChooseTaskAsync<SilbenTaskDefinition>([Skill.ReadSyllables, Skill.ReadPrecise])` auf. Ein neuer
   Task-Typ für `read_syllables` (z.B. "Silben zusammenschieben": Wort anhören, Silben-Kacheln in
   vorbereitete Slots ziehen) kann nicht dazukommen, ohne dass die Page ihn explizit kennt.
2. **Die Payload-Form** – `SilbenTaskDefinition.Generator` liefert fest `(string correct, string[]
   options)`, implizit "Multiple-Choice mit Audio". "Silben zusammenschieben" braucht eine andere
   Form (Wort in Silben zerlegt, gemischte Kacheln, Slot-Anzahl) – passt nicht in diese Signatur.
3. **Die Scoring-/Logging-Glue** – `SilbenChallenge.CheckAnswer` und
   `ArithmeticChallenge.Evaluate` machen fast identische Dinge (Log aus IndexedDB holen,
   `Kompetenzniveau.AddRichtig/AddFalsch`, `_currentTaskDef.Success/Fail`, `Score.AddPoints`,
   `Logger.Log`, Store zurückschreiben), aber komplett dupliziert.

Ergebnis: "Durchmischen" verschiedener Aufgabentypen scheitert nicht nur am Rendering, sondern
daran, dass Task-Auswahl, Payload-Form und Scoring alle an dieselbe Page-Klasse gekettet sind.

## Kernidee

Trennen, was heute verschmolzen ist:

- **Was trainiert wird** (Skill, Difficulty) – existiert schon, bleibt.
- **Wie die Aufgabe aussieht** (Payload) – weiterhin pro Task-Definition-Subtyp, wie heute schon
  (`ArithTaskDefinition` vs. `SilbenTaskDefinition` sind bereits unterschiedliche Payload-Shapes).
- **Wie sie dargestellt wird** (Presentation) – fehlt komplett, kommt neu dazu, als eigenes
  Attribut der Task-Definition statt als Eigenschaft der Page.

`AdaptiveTaskGenerator.ChooseTaskAsync<T>()` ist heute generisch über *eine* konkrete `T :
BaseTaskDefinition`. Für "Durchmischen" braucht es einen **typerasierten Picker**, der über
*mehrere* `BaseTaskDefinition`-Subtypen hinweg innerhalb eines Skill-Pools auswählen kann, plus ein
generisches Host-Component, das anhand eines Presentation-Keys die passende Blazor-Komponente
dynamisch lädt – analog zu `DebugPageWrapper.razor`, das per Reflection + `DynamicComponent` heute
schon eine Page anhand eines Namens lädt. Kein neues Muster im Projekt, nur eine Erweiterung eines
vorhandenen.

## Bausteine

### 1. `View` auf jeder Task-Definition

Jede `BaseTaskDefinition`-Subtype bekommt zusätzlich zu `Skills`/`DifficultyLevel` ein Feld, das
sagt, welche UI sie braucht:

```csharp
public required string View { get; init; } // "silben-multiple-choice", "silben-assembly", "arith-numpad"
```

String-Key statt Enum, passt zum bestehenden Stil (`TaskDomain`, `Skill` sind auch
String-Konstanten).

### 2. `TaskPresentationRegistry`

Statische Zuordnung `View`-Key → Komponenten-`Type`, im gleichen Stil wie
`TaskDebugOverrideRegistry`:

```csharp
public static class TaskPresentationRegistry
{
    // "silben-multiple-choice" -> typeof(SilbenMultipleChoiceView)
    // "silben-assembly"        -> typeof(SilbenAssemblyView)
}
```

Neue Task-Typen registrieren hier ihre View – der Host selbst muss dafür nie angefasst werden.

### 3. Type-erasiertes "Chosen Task"-Objekt

`LearningTask<T>` ist stark typisiert – gut für die einzelne Page, der generische Host kennt `T`
aber nicht zur Compile-Zeit. Also ein schmales Interface, das `LearningTask<T>` zusätzlich
implementiert:

```csharp
public interface IChosenTask
{
    object Payload { get; }
    string View { get; }
    IReadOnlyList<string> Skills { get; }
    Difficulty Difficulty { get; }
    Task Success(Kompetenzniveau k);
    Task Fail(Kompetenzniveau k);
}
```

**Wichtig, sonst Lücke in Baustein 4:** Heute ruft *die Page* `_currentTaskDef.Task.Generator(_rng)`
selbst auf, nachdem `ChooseTaskAsync<T>` zurückgekommen ist – sie kennt `T` ja. Der generische
`TaskHost` kennt kein `T` mehr und kann `Generator` daher nicht selbst aufrufen; `Payload` muss also
schon *innerhalb* des Pickers erzeugt und geboxt werden, an der einzigen Stelle, die `T` noch kennt:
in der konkreten Task-Definition-Subtype selbst. Jede `BaseTaskDefinition`-Subtype braucht dafür
einen virtuellen Dispatch-Punkt:

```csharp
public abstract class BaseTaskDefinition
{
    public required string[] Skills { get; init; }
    public int DifficultyLevel { get; init; }
    public required string View { get; init; }

    internal abstract IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store);
}

// in SilbenTaskDefinition:
internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
{
    var (correct, options) = Generator(rng);
    return new LearningTask<SilbenTaskDefinition>(this, difficulty, store) { /* Payload = (correct, options) */ };
}
```

Ohne das müsste `ChooseAnyAsync` (Baustein 4) selbst per Typ-Switch auf jede Subtype reagieren –
genau der Kopplungspunkt, den Baustein 2 ("Host muss dafür nie angefasst werden") eigentlich
vermeiden soll. `LearningTask<T>` muss dafür zusätzlich zum bisherigen `T Task` auch das generierte
Payload aufnehmen (aktuell wird das gar nicht auf `LearningTask<T>` gehalten, sondern bleibt lokal
in der Page).

### 4. Domain-/skill-übergreifender Picker mit Filterung

```csharp
Task<IChosenTask> ChooseAnyAsync(IReadOnlyCollection<string>? skills = null)
```

Gleiche Signatur-Form wie das bestehende `ChooseTaskAsync<T>`, Kandidatenpool ist `TaskRegistry.All`
statt `TaskRegistry.GetTasks<T>()`. `skills = null` heißt "wirklich alles", über alle Domains
hinweg.

**Wichtig:** `ChooseAnyAsync` braucht selbst keinen eigenen Domain-/Category-Parameter. Menüs lösen
das über das bereits vorhandene `SkillRegistry.ByDomain(...)` / `SkillRegistry.ByCategory(...)` zu
einer Skill-Id-Liste auf, bevor sie `ChooseAnyAsync` rufen:

- Hauptmenüpunkt "Mathe" → `SkillRegistry.ByDomain(TaskDomain.Math).Select(s => s.Id)`
- Hauptmenüpunkt "Deutsch" → `SkillRegistry.ByDomain(TaskDomain.Reading).Select(s => s.Id)`
- Untermenüpunkt, z.B. "phonetics" → `SkillRegistry.ByCategory("phonetics")`

Kein neuer Mechanismus, nur Wiederverwendung von dem, was schon da ist. `TaskHost` bekommt einfach
`[Parameter] IReadOnlyCollection<string>? Skills` und reicht das 1:1 an `ChooseAnyAsync` durch –
Menü-Filter-Seiten, Domain-Seiten und die komplett ungefilterte Seite sind dann alle dieselbe
Komponente mit unterschiedlichem Parameterwert.

**Rework nötig, aber erst bis Phase 4 (nicht Blocker für Phase 2):** `ITaskDebugOverride.TryForce<T>`
(`Model/Tasks/SilbenDebugOverride.cs`) prüft aktuell `candidates is not
IReadOnlyCollection<SilbenTaskDefinition>`. Das funktioniert nur, weil `ChooseTaskAsync<T>` heute
eine *statisch* als `IReadOnlyCollection<SilbenTaskDefinition>` typisierte Liste übergibt. Sobald
`ChooseAnyAsync` mit einem als `BaseTaskDefinition` typisierten, gemischten Pool arbeitet, ist dieser
Cast immer `false` – auch wenn zur Laufzeit zufällig nur Silben-Instanzen drinstünden, weil eine
`List<BaseTaskDefinition>` nie `IReadOnlyCollection<SilbenTaskDefinition>` implementiert, unabhängig
vom Inhalt. Ohne Fix schaltet das `/debug/...` (siehe CLAUDE.md) für jede auf `TaskHost` migrierte
Page lautlos ab, ohne Fehler. Muss spätestens in Phase 4 mit gelöst werden, z.B. indem der Override
nicht mehr über den generischen Collection-Typ, sondern explizit über die konkrete
`BaseTaskDefinition`-Subtype (`candidate.GetType()`) dispatcht wird. Konkretes Interface-Design dafür
folgt erst in Phase 4, kein Vorgriff jetzt.

### 5. Generisches `TaskHost`-Component (der "Mixer")

Ersetzt/ergänzt die heutigen Challenge-Pages als Container:

- ruft `ChooseAnyAsync(skills)`
- schlägt `View` in `TaskPresentationRegistry` nach
- rendert per `DynamicComponent` mit `ChosenTask` (`IChosenTask`) + `OnNext` (`EventCallback`) als
  Parameter (Vertrag: `ITaskView`)

**Präzisiert beim Bauen (Phase 4a):** Ursprünglich stand hier "TaskHost ruft die gemeinsame
Scoring-Glue auf" - das geht so nicht, weil `TaskSessionController.RecordSuccess/Fail` ein
`Kompetenzniveau` braucht, und das kommt aus dem domänenspezifischen Log-Entity, das laut Baustein 6
absichtlich *nicht* vereinheitlicht wird. `TaskHost` kennt dieses Entity gar nicht. Stattdessen: die
View injiziert `TaskSessionController` selbst (genau wie die Pages es heute tun) und ruft
`RecordSuccess`/`RecordFailure` direkt auf. `TaskHost`s Vertrag mit der View ist dadurch minimal -
nur `ChosenTask` (das `IChosenTask`) rein, `OnNext` (parameterloser `EventCallback`) raus, ausgelöst
wenn die View mit dieser Aufgabe fertig ist und die nächste will. Timing (z.B. Silbens
900ms-Verzögerung nach richtiger Antwort) bleibt UI-Präsentationsdetail der View, nicht von
`TaskHost` vorgegeben. Umgesetzt in `Model/Tasks/ITaskView.cs`, `Model/Tasks/TaskPresentationRegistry.cs`,
`Components/TaskHost.razor` - noch ohne konkrete View, daher noch nirgendwo gerendert.
Property heißt bewusst `ChosenTask`, nicht `Task` - Views sind async-lastig (`CheckAnswer` etc.),
ein `Task`-Property hätte ständig mit `System.Threading.Tasks.Task` kollidiert.

### 6. Scoring-/Logging-Glue extrahieren

Die aktuell doppelte Logik aus `CheckAnswer`/`Evaluate` in einen gemeinsamen Baustein ziehen (z.B.
`TaskSessionController`), der `IChosenTask.Success/Fail`, `ScoreService.AddPoints`,
`Kompetenzniveau`, `LoggerService.Log` kapselt. Domain-spezifisch bleibt nur die Frage "was ist die
stabile ID dieser konkreten Aufgabe für Kompetenzniveau-Tracking" (bei Silben das Wort, bei
Arithmetik `"5+6"`) – die Payload könnte das selbst mitliefern, z.B. über eine kleine
`ITaskPayload { string LogId { get; } }`-Konvention.

**Klarstellung, kein neuer Baustein:** `ArithmeticChallenge` war der erste implementierte Task-Typ
und wurde seither nur mittelmäßig an die sich weiterentwickelnde Logik angepasst – ihr aktuelles
Verhalten ist Referenz für "was gibt's an Code", nicht für "wie soll's sein". `SilbenChallenge`
bleibt der Verhaltens-Goldstandard. Konkret heißt das für die Glue:

- **`Fail()` muss für jeden falschen Versuch aufgerufen werden, in jeder Domain.**
  `SilbenChallenge.CheckAnswer` ruft bei falscher Antwort aktuell nie `_currentTaskDef.Fail(...)` –
  nur `Success()` wird beim letztendlich richtigen Versuch aufgerufen, mit der Anzahl der Fehlversuche
  indirekt über `Kompetenzniveau.AddFalsch()` verrechnet. Das ist ein Bug, kein bewusst zweites
  Interaktionsmodell: die Mastery-Logik kann Fehlerarten nur sinnvoll auswerten, wenn sie über jeden
  einzelnen Fehlversuch informiert wird. Die neue Glue ruft `Fail()` pro falschem Versuch auf – auch
  wenn (wie bei Silben) dieselbe Task-Instanz für einen Retry aktiv bleibt, statt wie bei Arithmetik
  jedes Mal eine neue Instanz zu ziehen. Der Retry-vs-neue-Instanz-Unterschied selbst bleibt
  Payload-/View-spezifisches Verhalten, nur der `Fail()`-Aufruf wird einheitlich.
- **Die Aggregat-Statistik-Ebene (`ArithemticLogStats`, IndexedDB-Id `"0"`) hat kein Silben-Äquivalent**
  und wird dort nur als In-Memory-Zähler auf der Page nachgebildet (`Logger.Erfolgreich`/
  `GesamtAnzahl`). Gehört explizit in den Scope von Baustein 6, sonst wird die Lücke erst mitten im
  Umbau entdeckt.
- **Punktwerte sind aktuell hart pro Page verdrahtet** (`AddPoints(5,5)` bei Silben,
  `AddPoints(2,8)`/`(-10*n,-10*n)`/`(-5,0)` bei Arithmetik – dazu gibt's schon einen TODO-Kommentar
  in `ArithmeticChallenge.razor.cs`). Für diesen Umbau reicht es, die Werte 1:1 in die Payload/
  Task-Definition zu verschieben (Verhalten unverändert); *dynamische* Punktvergabe nach effektiver
  Schwierigkeit ist bewusst out of scope, siehe TECH_DEBT.md.

## Konkret: "Silben zusammenschieben"

- Neue Definition `SilbenAssemblyTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition` (analog
  zu `SilbenTaskDefinition`), `View = "silben-assembly"`,
  `Generator: Func<Random, SilbenAssemblyPayload>` mit z.B.
  `record SilbenAssemblyPayload(string Word, string AudioFile, string[] ShuffledSyllables, int SlotCount)`.
- In `TaskRegistry.Tasks` registrieren (der Fail-Fast-Check beim Start bleibt erhalten).
- Neue Komponente `SilbenAssemblyView.razor` (Drag&Drop-UI), in `TaskPresentationRegistry` unter
  `"silben-assembly"` eingetragen.
- Fertig – der Skill-Pool `[Skill.ReadSyllables, Skill.ReadPrecise]` liefert dem `TaskHost` jetzt
  sowohl `SilbenTaskDefinition`- als auch `SilbenAssemblyTaskDefinition`-Instanzen gemischt, je nach
  Mastery/Difficulty-Gewichtung wie bisher.

## Neue Seite: ungefiltertes "Bestenmix" nach Mastery

`TaskHost` mit `Skills = null` → `ChooseAnyAsync(null)` → Kandidatenpool ist buchstäblich
`TaskRegistry.All`, Mathe und Deutsch gemischt. Die bestehende Weighting-Logik
(`WeakestSkillMastery`, `MasteryWeight`, Difficulty-Banding über `easiestDifficulty`) funktioniert
unverändert, weil sie nur die `Skills`-Liste der jeweiligen Definition anschaut, unabhängig von der
Domain.

Zu beachten, kein Blocker: Difficulty-Bänder werden relativ zum jeweiligen Kandidatenpool
berechnet. Bei einem Pool, der wirklich alles enthält, ist `DifficultyLevel 1` bei Mathe nicht
zwangsläufig gleich schwer kalibriert wie `DifficultyLevel 1` bei Deutsch – dieselbe relative Logik
wie heute, nur über einen größeren Pool.

## Offener Punkt: feingranulare Filter ("t/d-Übungen", "w/v/f-Übungen")

Geht mit dem oben beschriebenen Mechanismus **noch nicht**. Diese Gruppen sind heute keine Skills
oder Categories – sie entstehen erst zur Laufzeit im `GraphemPhonem`-Generator
(`SilbenTaskRegistry.cs`), der zufällig eine `g2p-v-f|<key>`-Tag-Gruppe aus `WordMeta.Data` zieht,
bevor überhaupt ein Wort gewählt wird. Es gibt aktuell keine ID, an der ein Filter "nur t/d"
ansetzen könnte, ohne den Generator umzubauen.

Zwei Wege, das später zu lösen (eigene Phase, hier noch keine Entscheidung getroffen):

- **Option A** – jede Verwechslungsgruppe wird ein echter `Skill` in `SkillRegistry` (z.B.
  `graphem_t_d`, `graphem_w_v_f`), mit eigenem `SilbenTaskDefinition`-Eintrag statt einer
  Zufallswahl im Generator. Vorteil: fügt sich nahtlos ins bestehende Skill/Filter/Mastery-Muster
  ein, Mastery wird dann auch pro Verwechslung getrackt statt als ein Blob über alle. Nachteil:
  mehr `SkillDefinition`-Einträge.
- **Option B** – ein von Skill/Mastery unabhängiges "Topic"-Tag nur fürs Filtern, Mastery bleibt
  gröber bei `GraphemPhonem` gebündelt. Braucht einen zweiten, parallelen Filtermechanismus neben
  Skills.

Tendenz zu Option A (kein zweiter Mechanismus nötig), aber nicht Teil der jetzigen Umbauphase.

## Offener Punkt: Event-artige Session-Bausteine im Mixer (Turbo als erster Fall)

`TurboArithChallenge` passt nicht in den `ITaskView`-Vertrag (`ChosenTask` rein, `OnNext` raus, ein
neuer Pick pro Aufgabe): es ist eine feste 3-Minuten-Zeitrunde auf einem einzigen, fest verdrahteten
Skill (`Turbo10`) mit eigenem Start/Running/Zusammenfassung-Zustand und Punktevergabe am Rundenende
statt pro Aufgabe - kein "eine Aufgabe, eine Darstellung"-Fall.

Product-Vision dazu (noch nicht spezifiziert, hier nur festgehalten, damit sie nicht verloren
geht): Turbo soll kein Sonderfall mit eigener Route bleiben, sondern zu einem allgemeinen
"Event"-Baustein werden, den der Mixer/`TaskHost` selbst mitten in einer normalen Session
auslösen kann - z.B. nach ein paar normalen Rechenaufgaben unangekündigt 20 Sekunden Turbo
einstreuen, danach wieder normale (eventuell sogar domänenübergreifend andere) Aufgaben, mit
einstellbarer/gewichteter Wahrscheinlichkeit fürs Einstreuen. Turbo wäre damit nur die erste
Ausprägung eines allgemeineren "kurze Sonder-Session mitten im Mix" Musters, nicht arithmetik-
spezifisch.

Damit müsste der `TaskHost`/`ITaskView`-Vertrag um ein Konzept erweitert werden, das heute fehlt:
etwas wie ein "Session"-View, das mehrere zusammenhängende Aufgaben-Picks selbst orchestriert
(eigener Timer, eigene Zusammenfassung), statt dass `TaskHost` bei jedem `OnNext` blind neu pickt -
und der Mixer bräuchte eine Instanz, die zwischen normalem Einzelaufgaben-Picking und "jetzt ein
Event triggern" entscheidet. Konkretes Design dafür steht aus (Auswahl-/Trigger-Mechanismus,
Vertragserweiterung für `ITaskView`, wie `TaskHost` zwischen beidem umschaltet) - bewusst **nicht**
Teil der aktuellen Umbauphase (4/5/6). `TurboArithChallenge` bleibt bis dahin eine eigene, nicht auf
`TaskHost` migrierte Seite/Route.

## Phasenplan (inkrementell)

1. [x] `View`-Feld auf bestehenden Task-Definitions ergänzen (`"silben-multiple-choice"`,
   `"arith-numpad"`, `"arith-turbo"` für die Turbo-Variante mit fehlendem Operanden), Verhalten
   unverändert. Build + volle Testsuite grün.
2. [x] `IChosenTask` + `ChooseAnyAsync(skills)` gebaut, Kandidatenpool `TaskRegistry.All`,
   unit-testbar (`ChooseAnyAsyncTests`), vorerst ungenutzt. Dabei gleich den in den Entscheidungen
   oben beschriebenen `BaseTaskDefinition.Choose(...)`-Dispatch mitgebaut (Problem 1 gelöst) -
   `DebugOverride` bewusst noch nicht angebunden (Problem 2, siehe Baustein 4, folgt in Phase 4).
3. Scoring-/Logging-Glue aus `SilbenChallenge.CheckAnswer` und `ArithmeticChallenge.Evaluate` in
   einen gemeinsamen Baustein ziehen, ohne die Pages umzubauen.
   - [x] **3a (Bugfix, erledigt):** `Fail()` wird jetzt bei jedem Fehlversuch aufgerufen
     (`SilbenChallenge`, `GraphemChallenge`), nicht nur `Success()` am Ende einer Runde. Siehe
     Commit "Fix: call LearningTask.Fail() on wrong Silben/Graphem answers".
   - [x] **3b (eigentliche Extraktion, erledigt):** Der wirklich gemeinsame Kern (`Score.AddPoints`,
     `Affirmation.Play*`, `IChosenTask.Success/Fail`) ist jetzt in `Services/TaskSessionController.cs`
     (scoped, `RecordSuccess`/`RecordFailure`) gezogen und wird von `SilbenChallenge`,
     `GraphemChallenge` und `ArithmeticChallenge` genutzt. Bewusst nicht angefasst:
     Log-Entity-Laden/Speichern (`SilbenLog` vs. `ArithemticLog`, unterschiedliche Shapes) und
     `Logger.Erfolgreich`/`GesamtAnzahl`-Bookkeeping (Silben: In-Memory-Zähler, Arithmetik:
     persistierte `ArithemticLogStats`, siehe TECH_DEBT.md #9) bleiben bewusst domänenspezifisch,
     siehe Entscheidungen oben. `TurboArithChallenge` bewusst nicht mit umgestellt (siehe
     TECH_DEBT.md #5, eigene Baustelle). Punktwerte bleiben unverändert hart in den Aufrufstellen
     (nicht in die Task-Definition verschoben) - reine Glue-Extraktion, kein Verhaltens- oder
     Datenmodell-Wechsel. Build + volle Testsuite grün.
4. `TaskHost` + `TaskPresentationRegistry` bauen, `SilbenChallenge`-UI in
   `SilbenMultipleChoiceView` extrahieren, `SilbenChallenge`-Page auf `TaskHost` umstellen.
   - [x] **4a (Gerüst, erledigt):** `ITaskView`, `TaskPresentationRegistry`, `Components/TaskHost.razor`
     gebaut, additiv, noch ohne konkrete View (siehe Entscheidungen oben zur `ChosenTask`-Umbenennung).
   - [x] **4b (Extraktion, erledigt - auf Nebenroute, "/" unangetastet):** `SilbenChallenge.razor(.cs)`
     (650 Zeilen inkl. ~350 Zeilen Popup-CSS) aufgeteilt in zwei neue, kleinere Komponenten unter
     `Components/TaskViews/`:
     - `SilbenMultipleChoiceView` (`ITaskView`-Implementierung, registriert als
       `"silben-multiple-choice"`) - Audio, Options-Grid, Feedback, ruft `TaskSessionController`
       direkt auf (siehe Baustein-5-Präzisierung oben). `OnParametersSetAsync` erkennt eine neue
       `ChosenTask`-Referenz und baut daraufhin Optionen/Audio neu auf und spielt die Datei ab -
       ein einziger Pfad für "erstes Wort" und "nächstes Wort nach Erfolg", wo die alte Page zwei
       separate Call-Sites brauchte (`OnInitializedAsync` + der `Task.Delay(900)`-Continuation).
     - `MarkierPopup` - das komplette Fehler-Markier-Popup (Markierungen, Buchstaben-Korrektur,
       Lücken, Hints, Hover-Timer) als eigene, in sich geschlossene Komponente mit eigenem
       `CorrectWord`/`WrongWord`/`OnResolved`-Vertrag. Reine Größen-/Lesbarkeits-Aufteilung, kein
       neuer Wiederverwendungsmechanismus (offene Frage dazu bleibt wie entschieden beantwortet).
     - Neue **temporäre** Vorschau-Route `/taskhost-silben` (`Pages/TaskHostSilbenPreview.razor`) →
       `<TaskHost Skills="[read_syllables, read_precise]" />`, exakt der Skill-Pool, den `/` heute
       nutzt. `/` selbst (die alte `SilbenChallenge`) bleibt vollständig unverändert, bis diese Route
       von dir verifiziert ist - siehe Abnahme-Checkliste. Wird beim eigentlichen Cutover (4c)
       wieder entfernt.
     - Build + volle Testsuite (63) grün. Kein automatisiertes UI-Testing (siehe
       [[feedback_no_playwright_ui_testing]]).
   - [x] **4c (Cutover, erledigt):** `/` auf `TaskHost` umgestellt (`Pages/SilbenChallenge/SilbenChallenge.razor`
     enthält jetzt nur noch `<TaskHost Skills="[read_syllables, read_precise]" />`, Klassenname bleibt
     `SilbenChallenge` fürs Debug-Wrapper-Reflection-Lookup), alte `SilbenChallenge.razor.cs` entfernt,
     `/taskhost-silben`-Vorschau-Route wieder entfernt (Datei umbenannt statt neu angelegt). `SilbenLog`
     dabei in eine eigene Datei gezogen (wird weiterhin von `GraphemChallenge` und
     `SilbenMultipleChoiceView` gebraucht). Bewusst **nicht** einzeln vor dem Cutover manuell verifiziert
     - siehe Abnahme-Checkliste oben, das gesammelte manuelle Testen ist an den Schluss der gesamten
     Umbauphase verschoben.
     Dabei gleich die in Baustein 4 vorgemerkte Lücke geschlossen: `AdaptiveTaskGenerator.ChooseAnyAsync`
     rief `DebugOverride` bisher nie auf, und `SilbenDebugOverride.TryForce` prüfte den *gesamten*
     Kandidaten-Typ (`candidates is IReadOnlyCollection<SilbenTaskDefinition>`) statt einzelner
     Elemente - das wäre bei einem gemischten `BaseTaskDefinition`-Pool nie erfüllt gewesen, egal was
     drinsteht. `TryForce` filtert jetzt per `candidates.OfType<SilbenTaskDefinition>()`, `ChooseAnyAsync`
     konsultiert `DebugOverride` genau wie `ChooseTaskAsync<T>`. Build + volle Testsuite (63) grün.
   - [x] **4d (nachgezogen, über den ursprünglichen Silben-Scope hinaus):** Phase 5 braucht für
     Mathe UND Deutsch je eine registrierte View - `GraphemChallenge` (`/graphem`) und
     `ArithmeticChallenge` (`/ArithmeticChallenge`) wurden deshalb ebenfalls auf `TaskHost`
     umgestellt, bevor Phase 5 losgeht:
     - `GraphemChallenge` nutzt für `GraphemPhonem` bereits `View = "silben-multiple-choice"` -
       Cutover war rein mechanisch (analog 4c), bis auf einen echten Unterschied: die alte Seite
       hatte nie einen "Anhören"-Button/Autoplay (stiller visueller Unterscheidungs-Test, kein
       Hörverstehen) und ihr `Generator`s `correct` ist der `WordMeta`-Dictionary-Key, nicht das
       `.filename` (anders als bei `ReadSyllables`/`ReadPrecise`) - ein blindes Wiederverwenden
       hätte also einen ungewollten Audio-Button samt potenziell falscher/404-Datei eingeführt.
       Fix: `SilbenMultipleChoiceView` blendet Audio-Box und Titel jetzt skill-abhängig ein/aus
       (`_hasAudio`, gleiches Muster wie das bereits bestehende `ReadPrecise`-only-Popup) - GraphemPhonem
       bleibt exakt stumm wie vorher.
     - `ArithmeticChallenge` bekam dafür eine neue `ArithNumpadView` (`Components/TaskViews/ArithNumpadView.razor(.cs)`,
       registriert als `"arith-numpad"`), extrahiert aus der alten Page - Ziffern-Grid, virtuelles
       Numpad, `TaskSessionController`-Aufrufe, unverändertes Verhalten (kein Erfolgs-Feedback-Text,
       direkt weiter zur nächsten Aufgabe wie bisher). `ArithTaskRegistry.SimpleSkills` bleibt der
       Skill-Pool (Turbo10 explizit ausgeschlossen).
     - `TurboArithChallenge` bewusst **nicht** migriert - passt strukturell nicht in den
       `ITaskView`-Vertrag, siehe "Offener Punkt: Event-artige Session-Bausteine im Mixer" unten.
     - Build + volle Testsuite (63) grün nach jedem Einzelschritt.
5. [x] **Hauptmenü: Filter-Einträge (erledigt).** Zwei neue, additive Routen (`/mathe-mix`,
   `/deutsch-mix` - `Pages/MatheMix.razor`, `Pages/DeutschMix.razor`), je nur `<TaskHost Skills="@(SkillRegistry.ByDomain(...).Select(s => s.Id).ToList())" />`
   und ein Menüpunkt in `MainLayout.razor` ("Mathe-Mix", "Deutsch-Mix"). **Bewusst additiv, keine
   der bestehenden Routen ersetzt** - "/", "/graphem" und "/ArithmeticChallenge" bleiben als
   gezielt engere Übungsformen bestehen; "Mathe-Mix"/"Deutsch-Mix" sind eine zusätzliche, breitere
   Option, keine Ablösung. Ob das langfristig so bleibt oder die engeren Routen mal aufgeräumt
   werden, ist eine eigene, hier noch nicht getroffene Entscheidung.

   Dabei einen Bug gefunden, der ohne Fix beim ersten Pick eines Turbo10-Kandidaten zur Laufzeit
   gecrasht wäre: `TaskRegistry.All` enthält `ArithTaskRegistry.Turbo` (Skill `Turbo10`, View
   `"arith-turbo"`) mit, aber es gibt (bewusst, siehe 4d) keine registrierte `"arith-turbo"`-View.
   Ein `SkillRegistry.ByDomain(Math)`-Filter enthält `Turbo10` mit, und `Skills = null`
   (Bestenmix, Phase 6) filtert nach Skill überhaupt nicht - beide Pools hätten also gelegentlich
   einen Turbo10-Kandidaten gezogen und `TaskPresentationRegistry.Resolve("arith-turbo")` hätte
   geworfen. Fix: `TaskPresentationRegistry.IsRegistered(view)` neu, `AdaptiveTaskGenerator.ChooseAnyAsync`
   filtert Kandidaten jetzt zusätzlich danach - nicht (noch) darstellbare Task-Typen werden aus
   gemischten Pools still ausgeklammert, statt zur Laufzeit zu crashen. Trifft zwei bestehende
   Tests, die zufällig `Skill.Math.Turbo10` als Testskill für `ChooseAnyAsync` genutzt hatten (auf
   `Skill.Math.Add15` umgestellt) plus ein neuer Regressionstest
   (`ChooseAnyAsync_NeverReturnsATaskWithAnUnregisteredView`). Build + volle Testsuite (64) grün.
6. [x] **Neue Seite "Bestenmix" (erledigt).** `/bestenmix` (`Pages/Bestenmix.razor`) → `<TaskHost />`
   ohne `Skills` (Default `null`), Mathe und Deutsch komplett gemischt nach Mastery/Difficulty,
   Turbo10 automatisch ausgeklammert (siehe Fund unter Phase 5). Menüpunkt in `MainLayout.razor`
   ergänzt. Build + volle Testsuite (64) grün.
7. *Separat, danach:* feine Granularität t/d, w/v/f – Option A vs. B entscheiden.
8. *Separat, danach:* neuer Task-Typ "Silben zusammenschieben" als eigentliches Feature obendrauf.

## Entscheidungen (vormals offene Fragen)

- **`TaskHost` ersetzt die bestehenden Pages**, kein paralleler Pfad daneben – sonst bleibt genau
  die Duplikation bestehen, die Baustein 6 eigentlich auflösen soll. Bestehende Pages
  (`SilbenChallenge`, `ArithmeticChallenge`, ...) werden strukturell identisch zu den in Phase 5
  geplanten Menü-Filter-Einträgen: `TaskHost` mit einer fest verdrahteten statt einer aus dem Menü
  kommenden Skill-Liste. Kein Sonderfall, derselbe Mechanismus.

- **`View`-Granularität: pro Task-Definition, nicht pro Payload-Form.** Nur die Task-Definition
  selbst weiß, wie sie sich sinnvoll darstellen lässt – deshalb bleibt `View` ein explizites Feld
  auf jeder `BaseTaskDefinition`-Subtype-Instanz (wie in Baustein 1 skizziert), kein aus der
  Payload-Shape abgeleiteter Wert. Wiederverwendung passiert dadurch, dass zwei Definitionen
  bewusst denselben `View`-String wählen (z.B. `read_precise` und `read_syllables` beide
  `"silben-multiple-choice"`), nicht automatisch über Shape-Kompatibilität. Das erlaubt auch den
  Fall, dass zwei Definitionen mit *derselben* Payload-Form trotzdem unterschiedliche Views
  brauchen – z.B. könnte `read_syllables` künftig sowohl über Multiple-Choice als auch über ein
  Drag&Drop-Silbenzusammenbau (`"silben-assembly"`) laufen, je nach konkretem Task-Typ, nicht je
  nach Payload-Shape.

- **Mark-your-mistake-Popup bleibt vorerst Teil von `SilbenMultipleChoiceView`**, kein eigener
  wiederverwendbarer Baustein (YAGNI, es existiert noch kein zweiter Anwendungsfall). Perspektivisch
  ist aber beabsichtigt, dass *alle* Task-Typen bei Fehlern ein pädagogisches
  "im-Detail-korrigieren"-Muster bekommen sollen, nicht nur Silben – bewusst out of scope für diesen
  Umbau, sollte aber bei künftigen Architektur-/Design-Entscheidungen (insbesondere Baustein 3
  `IChosenTask.Fail` und Baustein 6 Scoring-Glue) im Hinterkopf bleiben, damit dort keine
  Silben-spezifische Sackgasse entsteht.
