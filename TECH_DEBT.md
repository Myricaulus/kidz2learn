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

`TurboArithChallenge` (`Pages/ArithmeticChallenge/TurboArithChallenge.razor`) nutzt vermutlich
nicht durchgängig dieselbe Task-/Skill-Infrastruktur wie die "normalen" Challenges (`ArithTaskRegistry.Turbo`
existiert separat von `ArithTaskRegistry.Simple`/`All`). Muss geprüft werden, was genau fehlt
oder unsauber integriert ist, sobald Punkt 1 angegangen wird.

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
