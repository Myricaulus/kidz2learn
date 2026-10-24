# Tasks, views, mastery and the reading profile in code

Details behind the rules in `CLAUDE.md`. For adding a new task type, use the `/neue-aufgabe` skill.

## Task system (Model/Tasks)

Challenges are generic over a task-definition type deriving from `BaseTaskDefinition` (e.g. `ArithTaskDefinition`, `SilbenTaskDefinition`). Each definition carries a `Generator` func that produces a concrete task instance (numbers to add, or a word/options pair) and declares which `Skill` IDs it trains plus a `DifficultyLevel`.

- `TaskRegistry` statically maps each concrete `BaseTaskDefinition` subtype to its list of definitions (`ArithTaskRegistry.All`, `SilbenTaskRegistry.All`) and throws at type-init time if a new subtype in the assembly isn't registered — when adding a new task domain, register it in `TaskRegistry.Tasks` or the app will fail fast on startup.
- `AdaptiveTaskGenerator.ChooseTaskAsync<T>()` (in `Model/TaskGenerator.cs`) picks a task, weighting toward the learner's weakest skills and easier difficulties (weighting logic currently has the "weakest skill" filtering commented out — see TODOs in that file).
- A chosen task is wrapped in `LearningTask<T>`, which times the attempt and reports `Success`/`Fail` back into skill mastery (`SkillMasteryStore`) for every skill the task declares.

## Skill mastery (Model/Skills.cs, Model/Kompetenzniveau.cs, Entities/SkillStates.cs)

- `SkillRegistry` is the static catalog of `SkillDefinition`s (id, domain — `TaskDomain.Math`/`Reading` —, category, difficulty, display name). `Skill` / `Skill.Math` hold the string ID constants used throughout.
- `SkillMasteryStore` (backed by IndexedDB store `"SkillStates"`) adjusts a per-skill `Mastery` float (0–1) on each attempt, combining a base delta (by `Difficulty`: Normal/Hard/Extreme) with streak factors (`SkillRowFactor`), a per-specific-task streak factor (`TaskRowFactor`, from `Kompetenzniveau`), and a time-based factor. Several mastery-model TODOs (effective difficulty, Bayesian knowledge tracing, fail-reason inference) are intentionally unimplemented — don't assume they exist elsewhere.
- A task definition's `SkillWeights` scale the mastery change per skill (default 1): word problems
  to calculate count fully for `read_story_math` and half for `read_wh_question` and the matching
  arithmetic skill,
  so they also show up in the Lesedetektiv, the Mathe-Mix and `/ArithmeticChallenge`.
- `Kompetenzniveau` is a small ring-style history (last 20 attempts as a `'R'/'F'` string) tracked per specific task instance (e.g. per "5+6"), distinct from the per-skill `SkillState` in IndexedDB.
- `SkillMigrationHelper` initializes/migrates the IndexedDB skill schema on startup, including a one-time migration path from the legacy per-arithmetic-log storage (`ArithemticLog`/`ArithemticLogStats` in `Entities/Arithmetic.cs`) via `SkillInference.FromLegacyLog`. Bump `SkillMigrationHelper.CurrentSchemaVersion` and extend `MigrateAsync` when changing the skill schema.
- Tests: `Kidz2Learn.Tests` covers the pure-logic pieces: task/skill registries, `Kompetenzniveau`,
  `WordDiff`, `RingBuffer`, `StringAbbreviator`, and — via the `ISkillMasteryStore` abstraction +
  `FakeSkillMasteryStore` — `AdaptiveTaskGenerator` itself (see `AdaptiveTaskGeneratorTests.cs`).
  What's still untestable here is the mastery *formula* itself: it lives entirely in
  `SkillMasteryStore.Adjust` (`Model/Skills.cs`), hard-wired against a real `IndexedDbStore` instead
  of being a pure, extractable function - tracked as a GitHub issue ("Mastery & adaptive
  Aufgabenauswahl"), not in-repo.

## IndexedDB persistence

Uses `Tavenem.Blazor.IndexedDB`. The database (`AufgabenDB`, versioned in `Program.cs`) has value stores `ArithmetikAufgaben`, `LeseAufgaben`, `SkillMeta`, `SkillStates`,
`SilbenHammerRatings`, `Beobachtungen`, `MerkmalVerlauf`. Components inject the DB via `[Inject(Key = "AufgabenDB")] IndexedDb`, then index into a specific store (e.g. `AufgabenDb["ArithmetikAufgaben"]`). `Shared/Extensions/IndexedDbExtensions.ToDictionaryAsync<T>` is a helper for snapshotting a whole store keyed by `IIdItem.Id`.

## Task presentation: TaskHost + ITaskView (Components/TaskHost.razor, Components/TaskViews/)

Most challenge routes (`/` = `Lesedetektiv`, `/silben` = `SilbenChallenge`, `/graphem` = `GraphemChallenge`,
`/ArithmeticChallenge`) are a one-line page - `<TaskHost Skills="[...]" />` - instead of bespoke per-page logic; see
`TASK_PRESENTATION_REDESIGN.md` for the full design rationale and phase history.

- `BaseTaskDefinition` subtypes declare a `View` string key (e.g. `"silben-multiple-choice"`,
  `"arith-numpad"`); `TaskPresentationRegistry` maps that key to the Blazor component type that
  renders it. New task presentations register themselves there - `TaskHost` itself never needs to
  change for a new view.
- `AdaptiveTaskGenerator.ChooseAnyAsync(skills)` (type-erased counterpart to `ChooseTaskAsync<T>()`)
  picks across every registered `BaseTaskDefinition` subtype at once, filtered to a skill-id pool
  (or `null` for everything, across every domain). `TaskHost` calls this on init and again whenever
  the active view raises `OnNext`, resolves the returned `IChosenTask.View` via
  `TaskPresentationRegistry`, and renders that component type via `DynamicComponent`.
  It picks in two steps: a group first (definitions grouped by their first skill, weighted by the
  group's easiest level and that skill's mastery), then a definition within it - so an exercise with
  several variants (word problems: Rechnen leicht / Rechnen / Ablesen) counts once in the mix. 🎈/💪/💣
  stay relative to the whole pool (+2 and up is a 💣; the HUD shows the exact "+N" next to it). A
  variant above its group's easiest level is only picked once the group's skill has the mastery
  for it (`AdaptiveTaskGenerator.IsUnlocked`: +1 from 0.25, +2 from 0.5, +3 from 0.7, +4 from 0.85).
  `TaskHost` keeps long tasks apart (`Model/LangeAufgaben.cs`): a definition that took over 30 s on
  this page visit, or has a `FesteDauer` (the Turbo rounds), is left out for the next 3 picks after
  a long one.
- A view implements `ITaskView`: `[Parameter] IChosenTask ChosenTask` in, `[Parameter] EventCallback OnNext`
  out (raised when the view is done with the current task and wants the next one - timing, e.g.
  a delay after a correct answer, is the view's own business, not `TaskHost`'s). Views inject
  `TaskSessionController` themselves and call `RecordSuccess`/`RecordFailure` directly - `TaskHost`
  doesn't know about domain-specific log entities (`SilbenLog` vs. `ArithemticLog`).
- `SilbenMultipleChoiceView` (`"silben-multiple-choice"`) drives the multiple-choice UI shared by
  `read_syllables`/`read_precise`/`GraphemPhonem`: audio playback + options grid, and - only for
  `read_precise` - an interactive "mark your mistake" popup (`MarkierPopup`, computed by
  `Model/WordDiff.cs`'s Wagner-Fischer alignment) that the child must resolve before continuing.
  Audio/title are gated on the definition's `BrauchtTon` (`GraphemPhonem` is a silent
  visual-discrimination exercise without a listen button). A listening task's options stay locked
  until its word has started playing; without audio, `TaskHost` ends it unscored and only picks
  tasks without `BrauchtTon` (see `AudioStatusService` in `docs/services.md`).
- `ArithNumpadView` (`"arith-numpad"`) drives the written-addition/subtraction digit grid + virtual
  numpad shared by all non-Turbo arithmetic skills (`ArithTaskRegistry.SimpleSkills`).
- `QuatschDetektorView` (`"quatsch-detektor"`, skill `read_sentence_truth`) and `WFrageView`
  (`"w-frage"`, skill `read_wh_question`) are the sentence-comprehension tasks on the `/`
  start page ("Lesedetektiv"), built for slow/imprecise readers: no pictures, no writing, short
  sentences from the known word list. Content is hand-written/templated in `Model/Lesen/`
  (`QuatschSaetze`: true-vs-nonsense sentences; `WFragen`: one sentence and a series of 2-3 questions
  about it (`WFrageSerie`), each a color-coded W-question in its own box with *all of that sentence's
  own parts* as options, so keyword-matching never suffices; every question is its own scored
  attempt, `TaskSessionController.BeginTask` per question). Question words: Wer, Wen, Wem, Was, Wo,
  Wohin, Woher, Womit, Mit wem, Wann, Wie, Wie viele; sentences pair the look-alikes (Wo + Wohin)
  on purpose.
  The same view shows word problems (`Model/Lesen/Rechengeschichten.cs`, skill `read_story_math`,
  own page `/textaufgaben` "Textaufgaben"): a story about getting or giving away things (before /
  change / now), two numbers in the text, one "Wie viele ...?" question; options add the wrong
  operation's result. Level 2 calculates "jetzt" with the subject repeated, level 3 any number with
  a pronoun, and a level-2 "Ablesen" kind asks for a number in the text (no arithmetic skill), so
  the question has to be read. Numbers stay <= 9 for now.
  `RaetselView` (`"raetsel"`, skill `read_riddle`, "Wer bin ich?") joins them there: three clues
  from general to specific (`Model/Lesen/Raetsel.cs`, hand-written). Level 1 takes its wrong options
  from other `RaetselBereich`s, so the first clue already decides (`Unpassend` lists cross-kind
  pairs a clue would still fit). Level 2 uses each riddle's hand-picked `Aehnliche`, which the first
  clue fits too, so only the last clue decides. A wrong pick in W-Frage or Rätsel only greys that
  option out - nothing in the text gets marked, so the answer still has to be read. In W-Frage it
  also costs points (`DifficultyInfo.Penalty`: -10 per wrong pick so far as in Silben, weighted ×1.5
  / ×0.5 / ×0.2 for 🎈 / 💪 / 💣) and makes the question word blink and play (once `wer`/`wo`/...
  are recorded) while the options stay locked for 1.6 s.
  All three render sentences via `LeseSatz`, where words in `WordMeta` can be tapped to hear them
  (`WordAudio.PathFor`). Answer words without a recording yet are queued for the next recording
  session in the local, gitignored `WaveSplit/names.txt`.
  `SuchRasterView` (`"such-raster"`) is the search grid there: tap every matching tile, hand in
  with "Fertig!". Two kinds share it via the `SuchRaster` payload (`Model/Lesen/SuchRaster.cs`):
  "Nomen finden" (skill `read_nouns`, `NomenSammlung`) - all words in lower case; word lists are
  hand-picked to be unambiguous that way (no "Decke"/"decke", "Essen"/"essen"). Two knobs add up
  to its level (`NomenRaster.Stufe`, 1-5, nine definitions): grid size 3×3 / 4×3 / 4×4 and words -
  only touchable nouns / half abstract ones ("Sprung", "Geist") / only abstract nouns, each next to
  a non-noun of its word family ("springt") - and "Spiegelbuchstaben" (skill `read_mirror_letters`, `Spiegelbuchstaben`): find
  one of b/d/p/q among the others, 4×4 at level 2, 5×5 at level 3. No read-aloud. Only the first
  hand-in is scored (`SuchRasterWertung`: full / half / zero points for 0 / 1 / 2 mistakes - a
  wrong tap or a missed tile - and a point off for each one beyond; up to one mistake counts as
  solved for mastery). After a hand-in with mistakes they light up for 1.5 s and the child fixes
  them: "Weiter" unlocks once everything is right or after 10 s; pressing it with mistakes left
  marks them for good, and the grid still has to be fixed before going on.
  Points scale with word count (`LesePunkte`, calibrated to a 5-10 min session to the reward), and
  apart from the search grid and W-Frage wrong answers cost nothing. The old Silben
  tasks are mixed in via `TaskHost`'s `FillerSkills`/`FillerChance` (fixed ~1-in-8 rate, independent of
  mastery).
- `TurboArithChallenge` (`/ArithmeticTurbo`) stays its own 3-minute page, not on `TaskHost`. The
  mixer has Turbo as an event instead: `TurboRundeView` (`"turbo-runde"`, `EventTaskRegistry`, skill
  `turbo_10`, level 3 - a 💣 next to normal arithmetic) runs 20-30 s of `ArithTaskRegistry.TurboAufgabe`
  after a "Los!" and scores the round once (`Model/Tasks/TurboRunde.cs`: solved with >= 3 right and
  at most a quarter wrong; right answers earn points either way). GitHub issue "Turbo als Event im
  Mixer" holds the rest (triggering it mid-session independent of the picker).
- `LeseTurboView` (`"lese-turbo"`, `EventTaskRegistry`, skill `read_turbo`, level 3, on the
  Lesedetektiv) is the reading counterpart: 20-30 s of "echtes Wort oder Quatschwort?" (buttons or
  ←/→), no speed feedback during the round, a word left 5 s counts as wrong, scored once at the end
  (`Model/Lesen/LeseTurbo.cs`: >= 8 words, >= 80 % right). Words are generated offline into
  `Model/Lesen/LeseTurboWords.g.cs` by `uv run python WaveSplit/GenerateLeseTurboWords.py`: real
  words from WordMeta, nonsense words from their syllables and real words with two inner letters
  swapped (`VertauschtGleich`/`VertauschtAnders`: same or changed outline, a third of the nonsense
  words, pronounceable), checked against `de_50k.txt`, the Silbenhammer catalog and WordMeta;
  anything wrong either way goes in `WaveSplit/data/lese-turbo-blocklist.txt`.

## Reading profile (Model/Merkmale/, LESE_MERKMALE.md)

Recognition only (no picker changes): `TaskSessionController.RecordSuccess/RecordFailure` store
every answer as a `Beobachtung` in IndexedDB store `Beobachtungen` (unlimited). Views pass the
task-specific part as `BeobachtungsDaten` (target, answer, options, `MindestLeseMs` via `Lesezeit`,
`Werte` like `frage`/`a`/`b`/`op`, `Items` for Lese-Turbo words and search-grid mistakes with their tile index); the
controller adds attempt, duration, audio replays (`NoteVorgespielt`) and words tapped in `LeseSatz`
(`NoteAngetippt`). The telemetry carries the same object. Generators spread the compared
conditions evenly themselves: `read_precise` picks a `Distraktorart` first, then a word that has it
(`Model/Tasks/ErstleserDistraktorGenerator.cs`, classified by `FehlerartRechner`, no homophone
distractors), W-Frage always asks one long question word when the sentence has one, every other
mirror-letter grid is packed tight (`SuchRaster.Eng`, crowding), and a third of the Lese-Turbo
nonsense words are transposed real words with kept/changed outline. `MerkmalRechner.Berechne` (pure, tested)
turns them into 17 features M1-M17, each -1..+1 with a state (keine Daten / unklar / dafür /
dagegen / widersprüchlich), from per-source likelihood ratios in `Statistik` that add up per
feature; `FehlerartRechner` classifies
a wrong written option (Spiegel, Vokalfolge, Auslaut, ...). `/profile` shows them
(`Components/MerkmalProfil.razor`) for the last 28 days next to the 28 before, and stores one
snapshot per ISO week in `MerkmalVerlauf`. A view that wants to feed a feature passes
`BeobachtungsDaten`; a new source is a function in `MerkmalRechner`'s `Definitionen`.
