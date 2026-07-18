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

## [ ] 2. Bug: SilbenChallenge — `LeseAufgaben` IndexedDB Store nicht bekannt

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
(Noch nicht angewendet — nur diagnostiziert, s. Anfrage vom 2026-07-18.)

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
