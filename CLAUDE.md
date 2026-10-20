# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Kidz4Learn is a German-language Blazor WebAssembly (.NET 8) learning game for kids, combining small math/reading challenges with points, combos, and a Commodore-64-style SID music player. It's a static, fully client-side app (IndexedDB for persistence, no backend), auto-deployed to Netlify. A separate Python toolchain (`WaveSplit/`) is used offline to prepare syllable audio clips and generate `Model/WordMeta.g.cs`; it is not part of the app runtime.

## Comment style

Comments describe the *current* state of the code (or, as a `TODO`, a concrete *better* future
state) - never the past. Don't narrate what a former version did, why it was wrong, or that
something "used to be" some other way; git history/blame already covers that, and a comment that
half-describes a dead design just wastes tokens on every re-read. When refactoring, rewrite
comments to state the new invariant/rationale directly rather than framing it as a change from the
old one.

## Commands

Build, run, and watch (also available as VS Code tasks in `.vscode/tasks.json`):

```bash
dotnet build Kidz2Learn.csproj
dotnet run                       # serves at http://localhost:5171
dotnet watch run --project Kidz2Learn.csproj
dotnet publish Kidz2Learn.csproj
```

`Kidz2Learn.Tests` is an xUnit project referencing `Kidz2Learn.csproj` directly (no browser/JS
interop involved), covering the pure-logic pieces: task/skill registries, `Kompetenzniveau`,
`WordDiff`, `RingBuffer`, `StringAbbreviator`, and — via the `ISkillMasteryStore` abstraction +
`FakeSkillMasteryStore` — `AdaptiveTaskGenerator` itself (see `AdaptiveTaskGeneratorTests.cs`).
What's still untestable here is the mastery *formula* itself: it lives entirely in
`SkillMasteryStore.Adjust` (`Model/Skills.cs`), hard-wired against a real `IndexedDbStore` instead
of being a pure, extractable function - tracked as a GitHub issue ("Mastery & adaptive
Aufgabenauswahl"), not in-repo.

```bash
dotnet test Kidz2Learn.Tests/Kidz2Learn.Tests.csproj
dotnet test Kidz2Learn.Tests/Kidz2Learn.Tests.csproj --filter "FullyQualifiedName~WordDiffTests"
```

Important: `Kidz2Learn.Tests` lives in a subfolder of `Kidz2Learn.csproj`'s directory. The
Blazor SDK's implicit file globbing would otherwise also try to compile the test project's `.cs`
files (and its `obj/` output) into the main project — `Kidz2Learn.csproj` has an explicit
`<Compile Remove="Kidz2Learn.Tests/**/*.cs" />` (etc.) to prevent that. Keep that exclude in place
if you add more test-only folders under the repo root.

Python tooling (`WaveSplit/`, `main.py`) uses `uv`:

```bash
uv sync              # install deps from pyproject.toml/uv.lock (requires Python >=3.13)
uv run python WaveSplit/SplitWaveAndCompress.py   # interactive: splits WaveSplit/input.opus into wwwroot/audio/*.opus and regenerates Model/WordMeta.g.cs
uv run python WaveSplit/DeduplicateNames.py
```

The full recording workflow is under "Data generation pipeline" below.

`tools/generate_manifests.py` regenerates `wwwroot/sids/sidfiles.<hash>.json` (every `.sid` in `wwwroot/sids`
with its length from `tools/data/sid-songlengths.md5`, an excerpt of HVSC's `Songlengths.md5`) and
`wwwroot/audio/affirmations/affirmations.<hash>.json`, plus their current URLs in `Shared/AssetManifests.g.cs`.
The content hash in the name means a changed manifest gets a new URL, so no browser ever mixes an old
cached one with a new app. It runs automatically before every build (target `RegenerateAudioManifests` in
`Kidz2Learn.csproj`, before compiling, needs `python3`; a failure fails the build), so a new `.sid` only
needs to be dropped into the folder (plus its length line, see below). Keep `AssetManifests.g.cs` checked in:
MSBuild collects the sources before the script runs, so only an existing file is compiled.

## Architecture

### Task system (Model/Tasks)

Challenges are generic over a task-definition type deriving from `BaseTaskDefinition` (e.g. `ArithTaskDefinition`, `SilbenTaskDefinition`). Each definition carries a `Generator` func that produces a concrete task instance (numbers to add, or a word/options pair) and declares which `Skill` IDs it trains plus a `DifficultyLevel`.

- `TaskRegistry` statically maps each concrete `BaseTaskDefinition` subtype to its list of definitions (`ArithTaskRegistry.All`, `SilbenTaskRegistry.All`) and throws at type-init time if a new subtype in the assembly isn't registered — when adding a new task domain, register it in `TaskRegistry.Tasks` or the app will fail fast on startup.
- `AdaptiveTaskGenerator.ChooseTaskAsync<T>()` (in `Model/TaskGenerator.cs`) picks a task, weighting toward the learner's weakest skills and easier difficulties (weighting logic currently has the "weakest skill" filtering commented out — see TODOs in that file).
- A chosen task is wrapped in `LearningTask<T>`, which times the attempt and reports `Success`/`Fail` back into skill mastery (`SkillMasteryStore`) for every skill the task declares.

### Skill mastery (Model/Skills.cs, Model/Kompetenzniveau.cs, Entities/SkillStates.cs)

- `SkillRegistry` is the static catalog of `SkillDefinition`s (id, domain — `TaskDomain.Math`/`Reading` —, category, difficulty, display name). `Skill` / `Skill.Math` hold the string ID constants used throughout.
- `SkillMasteryStore` (backed by IndexedDB store `"SkillStates"`) adjusts a per-skill `Mastery` float (0–1) on each attempt, combining a base delta (by `Difficulty`: Normal/Hard/Extreme) with streak factors (`SkillRowFactor`), a per-specific-task streak factor (`TaskRowFactor`, from `Kompetenzniveau`), and a time-based factor. Several mastery-model TODOs (effective difficulty, Bayesian knowledge tracing, fail-reason inference) are intentionally unimplemented — don't assume they exist elsewhere.
- A task definition's `SkillWeights` scale the mastery change per skill (default 1): word problems
  to calculate count fully for `read_story_math` and half for `read_wh_question` and the matching
  arithmetic skill,
  so they also show up in the Lesedetektiv, the Mathe-Mix and `/ArithmeticChallenge`.
- `Kompetenzniveau` is a small ring-style history (last 20 attempts as a `'R'/'F'` string) tracked per specific task instance (e.g. per "5+6"), distinct from the per-skill `SkillState` in IndexedDB.
- `SkillMigrationHelper` initializes/migrates the IndexedDB skill schema on startup, including a one-time migration path from the legacy per-arithmetic-log storage (`ArithemticLog`/`ArithemticLogStats` in `Entities/Arithmetic.cs`) via `SkillInference.FromLegacyLog`. Bump `SkillMigrationHelper.CurrentSchemaVersion` and extend `MigrateAsync` when changing the skill schema.

### IndexedDB persistence

Uses `Tavenem.Blazor.IndexedDB`. The database (`AufgabenDB`, versioned in `Program.cs`) has value stores `ArithmetikAufgaben`, `LeseAufgaben`, `SkillMeta`, `SkillStates`. Components inject the DB via `[Inject(Key = "AufgabenDB")] IndexedDb`, then index into a specific store (e.g. `AufgabenDb["ArithmetikAufgaben"]`). `Shared/Extensions/IndexedDbExtensions.ToDictionaryAsync<T>` is a helper for snapshotting a whole store keyed by `IIdItem.Id`.

### Task presentation: TaskHost + ITaskView (Components/TaskHost.razor, Components/TaskViews/)

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
  stay relative to the whole pool.
- A view implements `ITaskView`: `[Parameter] IChosenTask ChosenTask` in, `[Parameter] EventCallback OnNext`
  out (raised when the view is done with the current task and wants the next one - timing, e.g.
  a delay after a correct answer, is the view's own business, not `TaskHost`'s). Views inject
  `TaskSessionController` themselves and call `RecordSuccess`/`RecordFailure` directly - `TaskHost`
  doesn't know about domain-specific log entities (`SilbenLog` vs. `ArithemticLog`).
- `SilbenMultipleChoiceView` (`"silben-multiple-choice"`) drives the multiple-choice UI shared by
  `read_syllables`/`read_precise`/`GraphemPhonem`: audio playback + options grid, and - only for
  `read_precise` - an interactive "mark your mistake" popup (`MarkierPopup`, computed by
  `Model/WordDiff.cs`'s Wagner-Fischer alignment) that the child must resolve before continuing.
  Audio/title are gated on the skill list (`GraphemPhonem` is a silent visual-discrimination
  exercise, never had a listen button).
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
  "Nomen finden" (skill `read_nouns`, `NomenSammlung`) - 16 words, 6-8 of them nouns, all in
  lower case; word lists are hand-picked to be unambiguous that way (no
  "Decke"/"decke", "Essen"/"essen"); level 1 only touchable nouns, level 2 mixes in abstract ones
  ("Sprung", "Geist"), level 3 only abstract nouns, each next to a non-noun of its word family
  ("springt") - and "Spiegelbuchstaben" (skill `read_mirror_letters`, `Spiegelbuchstaben`): find
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
  words from WordMeta, nonsense words from their syllables, checked against `de_50k.txt`, the
  Silbenhammer catalog and WordMeta; anything wrong either way goes in
  `WaveSplit/data/lese-turbo-blocklist.txt`.

### Debug mode (Pages/Debug/) — forcing a specific task for manual testing

`/debug/{PageName}?task=<kind>&...` (`DebugPageWrapper.razor`) embeds any existing page/component by
name (via reflection over the assembly) and, if a `task` query param names a registered domain,
forces `AdaptiveTaskGenerator.ChooseTaskAsync<T>()` to hand back one specific task instead of
picking adaptively. This exists mainly so a specific word/skill edge case (e.g. an unusually long
word breaking a layout) can be reproduced directly by URL — in particular for testing through the
Chrome extension — instead of reloading the real page repeatedly until the adaptive picker happens
to serve it.

- `task=silben` (the only registered kind so far; `SilbenDebugOverrideFactory` in
  `Model/Tasks/SilbenDebugOverride.cs`) forces a Silben task: `word=<Word>` (required, matched
  case-insensitively against `WordMeta.Data` keys) picks the target, `skill=<id>` (optional, e.g.
  `read_precise` or `read_syllables`) picks which `SilbenTaskDefinition` to force, `options=a,b,c`
  (optional) fixes the distractor options instead of generating them via
  `ErstleserDistraktorGenerator`.
- Example: `localhost:5171/debug/SilbenChallenge?task=silben&word=Sonnensystem&skill=read_precise`
  reproduces the "Sonnensystem" options-layout edge case directly, on the first render, without
  hitting "Anhören" and rerolling until it comes up.
- New task domains (e.g. Arithmetik) register their own `ITaskDebugOverrideFactory` in
  `TaskDebugOverrideRegistry`'s static constructor; `DebugPageWrapper.razor` itself never needs to
  change for that.
- The override lives in a static hook (`AdaptiveTaskGenerator.DebugOverride`), reset to `null` when
  the debug page is disposed — it does not persist once you navigate away from `/debug/...`, and
  since it's static/shared process state, avoid relying on it if the debug wrapper is ever loaded
  in more than one place at once.

### Cross-cutting services (Services/, singleton unless noted)

- `ScoreService` — base + decaying bonus score (bonus decays on a `System.Threading.Timer`); also self-adjusts a difficulty/pacing target from elapsed time between answers.
- `HudStateService` — combo count / timer visibility / current task difficulty, exposed via `OnChange` for HUD components to subscribe to. `TaskHost` sets the difficulty for every task it loads (and clears it on dispose); `DifficultyIndicator` in the HUD sidebar shows it under the combo.
- Difficulty rewards: `Model/DifficultyInfo.cs` holds the kid-facing icon/label (🎈 Spielen, 💪 Arbeiten, 💣 Bombe) and the point factor (×1 / ×1.5 / ×2) per `Difficulty`. `TaskSessionController.RecordSuccess` scales every success's points by it, so views pass Normal-level points; only points awarded outside of it (Silbenhammer's per-word `Score.AddPoints`) call `DifficultyInfo.Scale` themselves. `Difficulty` is relative to the candidate pool (easiest `DifficultyLevel` = Normal), not to the learner's mastery - see GitHub issue #1.
- `LoggerService` — broadcasts `RenderFragment` log entries (`OnLogAppended`) for an in-page live log.
- `SidWidgetService` — current player volume, exposed via `OnVolumeChanged` so pages/views can duck/restore music (e.g. `SilbenMultipleChoiceView` lowers volume while an exercise is active).
- `SidPlayerService` (scoped) — thin JS-interop wrapper around `wwwroot/js/jsSID.js`'s `window.sidPlayer` (a C64 SID chiptune player) for the `SidPlayerWidget` component; SID files live in `wwwroot/sids`, cataloged in `sidfiles.<hash>.json`. SID tunes loop forever, so the widget switches to a random other tune once the current one has played its HVSC length (`k4l_sidOnEnd`, 3 min if unknown); a new `.sid` needs its two lines from HVSC's `Songlengths.md5` in `tools/data/sid-songlengths.md5`.
- `AffirmationService` (scoped — depends on the scoped `HttpClient`) — plays a random success/failure sound after a task is answered, called from `TaskSessionController` (covering every `TaskHost`-based view) and directly from `TurboArithChallenge`, which isn't on `TaskHost`. Files live in `wwwroot/audio/affirmations/{erfolg,misserfolg}/`, cataloged in `affirmations.<hash>.json` (regenerated before every build by `tools/generate_manifests.py`, see Commands). Both folders are currently empty, so playback silently no-ops until sound files are actually recorded and dropped in.

- `TelemetryService` (scoped) — opt-in per-device session log (every `RecordSuccess`/`RecordFailure` via `TaskSessionController`, with a human-readable `detail`, the time since `TaskHost` showed the task, the definition's `level`, the pool-relative `difficulty`, the `attempt` number within the task, and each skill's mastery before/after scoring - the raw material for tuning the mastery formula). Only devices opened once with `?telemetrie=<url>` (`=off` undoes it) send anything; everywhere else it only logs each attempt to the browser console (as on every device; `copy(k4l_telemetrySession)` there copies the session as JSON) - a fallback for field tests without opt-in, and the endpoint lives only in that device's localStorage, never in the repo. Local dev runs (`dotnet run`/Rider, environment `Development`) always send: `MainLayout` falls back to the `Telemetrie` key of `wwwroot/appsettings.Development.json`, which is gitignored and exists only on the parent's PC. Every session carries `App` (the app's base URL), so `localhost` test sessions stand apart from the kids' ones. On opted-in devices, a failed upload makes the YouTube button ask "Wirklich zu YouTube wechseln?" so the console can be inspected first. The receiving side is `tools/telemetry_receiver.py` (stdlib Python, HTTPS, one JSON file per session), run as a systemd service on the parent's home server. Sessions are fetched without SSH by `tools/telemetry_fetch.py` (token-authenticated `GET`/`DELETE /sessions` on the same receiver; the server holds only the token's SHA-256, the token and URL live in `~/.config/kidz2learn/telemetrie-abruf.json`) into `inbox/telemetrie/`, deleting each session on the server once it's stored locally and untouched for an hour.

### Shared audio elements (MainLayout.razor)

`#audioPlayer` (word/syllable playback) and `#affirmationPlayer` (success/failure sounds) are both mounted once in `MainLayout.razor`, outside the splash/main-layout `@if` branches, so they're stable DOM nodes across the whole app lifetime — not owned by individual challenge pages. `StartApp()`'s "Starte App" click primes both synchronously (`k4l_primeAudioPlayer` in `wwwroot/js/interop.js`) so the browser ties audio-autoplay permission to that gesture; `k4l_playAudioFile(id, src)` is what pages call afterward to actually play a file. Keeping the two elements separate matters because a single `<audio>` element can only play one source at a time — sharing one between word narration and affirmations would let either abort the other.

### Data generation pipeline (WaveSplit/, offline/authoring only)

Recording workflow for new word clips (run from the repo root, the scripts use repo-relative paths):

1. **Queue words** in `WaveSplit/names.txt`, one per line in syllable spelling (`Gar-ten`,
   `Zi-tro-ne`); the hyphens become `WordInfo.filename` and the syllable count. Everything under
   `WaveSplit/` except the scripts and `data/` is gitignored, so this list lives only on the
   parent's machine. Whenever a task needs a word that has no recording yet, add it here.
2. **Deduplicate:** `uv run python WaveSplit/DeduplicateNames.py` writes `names_cleaned.txt`,
   dropping lines repeated within the list and words that already have a clip in `wwwroot/audio/`.
3. **Record** all words of `names_cleaned.txt` in order, with a short pause between them, as
   `WaveSplit/input.opus`.
4. **Split:** `uv run python WaveSplit/SplitWaveAndCompress.py` cuts `input.opus` into one clip per
   word via silence detection (it aborts if the segment count doesn't match the list), lets you
   review/trim each clip interactively, exports `wwwroot/audio/<Syl-la-bles>.opus`, then runs each
   word through spaCy (POS tags) + espeak/phonemizer (IPA + stress/vowel-length tags) + custom
   grapheme-cluster tagging and inserts the new entries into `Model/WordMeta.g.cs` at the
   `// ###Endmarker for replacement###` marker - keep that marker intact when hand-editing entries.
   An existing entry with the same key (case-insensitive) is replaced, so recording a word that is
   listed without audio (the `,false` entries) just upgrades it - a duplicate key would crash
   `WordMeta`'s static initializer.

`WordMeta.Data` (and its `Tags`, e.g. `"g2p-v-f|..."` grapheme/phoneme tags) is what
`SilbenTaskRegistry`'s task generators query to pick words and build distractor options.
