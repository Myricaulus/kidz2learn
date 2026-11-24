# Auffälligkeiten in Design, Architektur und Code

Beim Bauen gesehen, nicht behoben. Jede Zeile: was auffällt, wo, und was es kostet.
Erledigtes löschen statt abhaken.

## Duplikate

- **Log-Gerüst in jeder Lese-View.** `QuatschDetektorView`, `WFrageView`, `RaetselView`,
  `SuchRasterView`, `AbcSortierenView` (und die Silben-Views, insgesamt 8 Stellen) wiederholen
  dieselben ~10 Zeilen: Store `LeseAufgaben` holen, `SilbenLog.GenId`, `GetItemAsync<SilbenLog>`,
  `Kompetenz.AddRichtig/AddFalsch`, außer im Koop speichern, `Logger.Log`. Ein Helfer
  (`await Lernlog.Antwort(skill, schluessel, richtig, ...)`) würde das auf eine Zeile bringen.
- **`SilbenLog` ist der Lernlog aller Leseaufgaben**, liegt aber in `Pages/SilbenChallenge/` und
  heißt nach den Silben. Wer eine neue Aufgabe baut, sucht ihn dort nicht.
- **Gleiches CSS in jeder Lese-View.** `.lese-container`, `.lese-titel`, `.lese-feedback(-richtig/-falsch)`,
  `.lese-fortschritt` stehen fast gleich in vier `*.razor.css` (Scoped CSS erzwingt die Kopie);
  `AbcSortierenView.razor.css` hat sie als `.abc-*` ein fünftes Mal. Ein gemeinsamer
  Rahmen (`LeseRahmen.razor` mit Titel, Feedback, Zähler) oder eine globale Datei in `wwwroot/css`
  würde reichen.
- **Zwei-Versuche-Logik je View neu.** "Falsch → nochmal → irgendwann Auflösung" ist in Rätsel,
  W-Frage und ABC-Sortieren jeweils eigen gebaut, mit unterschiedlichen Regeln (Option ausgrauen,
  Strafpunkte, Sperre, Auflösung). Ob die Unterschiede gewollt sind, steht nirgends.
- **Richtig/Fehler-Zähler** (`_correctCount`/`_wrongCount`) in jeder View, obwohl
  `TaskSessionController` dieselben Zahlen führt und der HUD sie zeigt.

## Registrierung an drei Stellen

- Ein neuer Definitionstyp muss in `TaskRegistry.Tasks` (Dictionary), als `AllXyz`-Property und in
  der Summe von `TaskRegistryTests.All_IsUnionOfArithAndSilbenTasks` nachgetragen werden. Der Test
  prüft nur die eigene Liste und schlägt bei jeder neuen Aufgabe fehl, ohne etwas zu finden, was der
  statische Konstruktor nicht schon fände.
- `LeseVerstehenTaskRegistry` wird zum Sammelbecken für alles auf dem Lesedetektiv (Quatsch,
  W-Frage, Rätsel, Suchraster, ABC). Der Name passt zu Suchraster und ABC nur noch halb.

## Veraltete Kommentare (gegen die Kommentar-Regel in CLAUDE.md)

- `BaseTaskDefinition.View`: "Not yet consumed anywhere - part of the incremental
  task-presentation redesign" - wird längst von `TaskPresentationRegistry`/`TaskHost` benutzt.
- `IChosenTask`: "for callers (like the future generic TaskHost ...)" - den gibt es.
- `ITaskView`: "(it injects TaskSessionController itself, exactly like the pages did before)" -
  beschreibt den alten Stand.
- `BaseTaskDefinition.Choose`: "matching how pages call `Generator(rng)` with their own rng
  today"; `LearningTask`-Konstruktor: "The legacy `ChooseTaskAsync<T>` path above ...".

## Design

- **Zwei Auswahlpfade.** `AdaptiveTaskGenerator.ChooseTaskAsync<T>` (alt, Payload erzeugt die
  Seite) neben `ChooseAnyAsync` (Payload erzeugt `Choose`). Der alte Pfad bekommt die Mastery nicht
  und kann z. B. ABC-Sortieren nicht richtig erzeugen. Nutzer: nur noch `TurboArithChallenge`
  (`/ArithmeticTurbo`) und die `AdaptiveTaskGeneratorTests`.
- **Mastery nur über `Choose`.** ABC-Sortieren braucht die Mastery zum Erzeugen; dafür hat
  `Choose` einen Parameter `mastery` (die der ersten Skill-Gruppe) bekommen, den alle anderen
  Definitionen ignorieren. Ein Kontextobjekt (`AuswahlKontext` mit rng, Schwierigkeit, Mastery,
  Stufen über der leichtesten) wäre beim nächsten Parameter die sauberere Form.
- **Debug-Pfad immer `Difficulty.Normal`.** Ein erzwungenes Level 3 zeigt 🎈 und zahlt
  Normal-Punkte; Smoke-Tests sehen dadurch nicht, wie die Aufgabe im echten Mix aussieht.
- **Strafen uneinheitlich.** Suchraster zieht Punkte ab, W-Frage über `DifficultyInfo.Penalty`,
  Quatsch/Rätsel/ABC kosten nichts. `docs/tasks.md` beschreibt es, eine Regel dahinter fehlt.
