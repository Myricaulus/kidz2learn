# Konzept: Task-Auswahl und -Darstellung entkoppeln

Status: Umsetzung läuft inkrementell, siehe Phasenplan unten. Dient als Grundlage für ein
inkrementelles Umbauvorhaben.

## Manuelle Abnahme-Checkliste

UI-Verhalten wird nicht automatisiert durch Claude im Browser getestet (siehe
[[feedback_no_playwright_ui_testing]]) - hier sammeln sich stattdessen die Punkte, die du selbst
einmal durchklicken solltest, sobald du Zeit hast. Build + volle Unit-Testsuite sind für jeden
Punkt unten bereits grün.

- [ ] **SilbenChallenge, `read_syllables`/`GraphemPhonem`:** eine falsche Option anklicken, danach
  die richtige. Erwartet: Feedback "Nochmal versuchen!" bei Falsch, danach normaler Ablauf wie
  bisher (keine sichtbare Änderung erwartet - der Unterschied ist nur, dass jetzt auch bei der
  falschen Antwort ein `SkillState`-Adjust in IndexedDB passiert, sichtbar z.B. via
  `SkillMasteryStore`s `Console.WriteLine` in der Browser-Devtools-Konsole).
- [ ] **SilbenChallenge, `read_precise`:** falsche Option anklicken → Markier-Popup öffnet sich wie
  bisher, Ablauf/Optik unverändert.
- [ ] **GraphemChallenge:** dieselbe Prüfung wie oben (falsch → richtig), keine sichtbare
  Verhaltensänderung erwartet.

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
- rendert per `DynamicComponent` mit `Payload` + `EventCallback<TaskAnswer>` als Parameter
- bekommt die Antwort zurück und ruft die gemeinsame Scoring-Glue auf

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
4. `TaskHost` + `TaskPresentationRegistry` bauen, `SilbenChallenge`-UI in
   `SilbenMultipleChoiceView` extrahieren, `SilbenChallenge`-Page auf `TaskHost` umstellen.
5. Hauptmenü: Filter-Einträge (Domain/Category via `SkillRegistry.ByDomain`/`ByCategory`) →
   `TaskHost` mit entsprechender Skill-Liste.
6. Neue Seite "Bestenmix" (Name offen) → `TaskHost` mit `Skills = null`.
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
