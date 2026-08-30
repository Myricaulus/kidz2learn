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

`generate_sid_json.ps.ps1` (PowerShell) regenerates `wwwroot/sids/sidfiles.json` from the `.sid` files in `wwwroot/sids`.

## Architecture

### Task system (Model/Tasks)

Challenges are generic over a task-definition type deriving from `BaseTaskDefinition` (e.g. `ArithTaskDefinition`, `SilbenTaskDefinition`). Each definition carries a `Generator` func that produces a concrete task instance (numbers to add, or a word/options pair) and declares which `Skill` IDs it trains plus a `DifficultyLevel`.

- `TaskRegistry` statically maps each concrete `BaseTaskDefinition` subtype to its list of definitions (`ArithTaskRegistry.All`, `SilbenTaskRegistry.All`) and throws at type-init time if a new subtype in the assembly isn't registered — when adding a new task domain, register it in `TaskRegistry.Tasks` or the app will fail fast on startup.
- `AdaptiveTaskGenerator.ChooseTaskAsync<T>()` (in `Model/TaskGenerator.cs`) picks a task, weighting toward the learner's weakest skills and easier difficulties (weighting logic currently has the "weakest skill" filtering commented out — see TODOs in that file).
- A chosen task is wrapped in `LearningTask<T>`, which times the attempt and reports `Success`/`Fail` back into skill mastery (`SkillMasteryStore`) for every skill the task declares.

### Skill mastery (Model/Skills.cs, Model/Kompetenzniveau.cs, Entities/SkillStates.cs)

- `SkillRegistry` is the static catalog of `SkillDefinition`s (id, domain — `TaskDomain.Math`/`Reading` —, category, difficulty, display name). `Skill` / `Skill.Math` hold the string ID constants used throughout.
- `SkillMasteryStore` (backed by IndexedDB store `"SkillStates"`) adjusts a per-skill `Mastery` float (0–1) on each attempt, combining a base delta (by `Difficulty`: Normal/Hard/Extreme) with streak factors (`SkillRowFactor`), a per-specific-task streak factor (`TaskRowFactor`, from `Kompetenzniveau`), and a time-based factor. Several mastery-model TODOs (effective difficulty, Bayesian knowledge tracing, fail-reason inference) are intentionally unimplemented — don't assume they exist elsewhere.
- `Kompetenzniveau` is a small ring-style history (last 20 attempts as a `'R'/'F'` string) tracked per specific task instance (e.g. per "5+6"), distinct from the per-skill `SkillState` in IndexedDB.
- `SkillMigrationHelper` initializes/migrates the IndexedDB skill schema on startup, including a one-time migration path from the legacy per-arithmetic-log storage (`ArithemticLog`/`ArithemticLogStats` in `Entities/Arithmetic.cs`) via `SkillInference.FromLegacyLog`. Bump `SkillMigrationHelper.CurrentSchemaVersion` and extend `MigrateAsync` when changing the skill schema.

### IndexedDB persistence

Uses `Tavenem.Blazor.IndexedDB`. The database (`AufgabenDB`, versioned in `Program.cs`) has value stores `ArithmetikAufgaben`, `LeseAufgaben`, `SkillMeta`, `SkillStates`. Components inject the DB via `[Inject(Key = "AufgabenDB")] IndexedDb`, then index into a specific store (e.g. `AufgabenDb["ArithmetikAufgaben"]`). `Shared/Extensions/IndexedDbExtensions.ToDictionaryAsync<T>` is a helper for snapshotting a whole store keyed by `IIdItem.Id`.

### Task presentation: TaskHost + ITaskView (Components/TaskHost.razor, Components/TaskViews/)

Most challenge routes (`/` = `SilbenChallenge`, `/graphem` = `GraphemChallenge`, `/ArithmeticChallenge`)
are now a one-line page - `<TaskHost Skills="[...]" />` - instead of bespoke per-page logic; see
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
- `TurboArithChallenge` is deliberately **not** on `TaskHost` - it's a fixed 3-minute timed round on
  one hardcoded skill (`Turbo10`) with its own start/running/summary state and end-of-round scoring,
  not a "one task, one presentation" case. See `TASK_PRESENTATION_REDESIGN.md`, "Offener Punkt:
  Event-artige Session-Bausteine im Mixer" for where this is headed - turning Turbo into a general
  "event" the mixer can inject mid-session is tracked as its own GitHub issue ("Turbo als Event im
  Mixer"), not in-repo.

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
- `HudStateService` — combo count / timer visibility / difficulty, exposed via `OnChange` for HUD components to subscribe to.
- `LoggerService` — broadcasts `RenderFragment` log entries (`OnLogAppended`) for an in-page live log.
- `SidWidgetService` — current player volume, exposed via `OnVolumeChanged` so pages/views can duck/restore music (e.g. `SilbenMultipleChoiceView` lowers volume while an exercise is active).
- `SidPlayerService` (scoped) — thin JS-interop wrapper around `wwwroot/js/jsSID.js`'s `window.sidPlayer` (a C64 SID chiptune player) for the `SidPlayerWidget` component; SID files live in `wwwroot/sids`, cataloged in `sidfiles.json`.
- `AffirmationService` (scoped — depends on the scoped `HttpClient`) — plays a random success/failure sound after a task is answered, called from `TaskSessionController` (covering every `TaskHost`-based view) and directly from `TurboArithChallenge`, which isn't on `TaskHost`. Files live in `wwwroot/audio/affirmations/{erfolg,misserfolg}/`, cataloged in `affirmations.json` (regenerated by `generate_affirmations_json.ps1`, wired into `Kidz2Learn.csproj` as a `BeforeBuild` target alongside the SID one — both need `pwsh` and just warn-and-skip if it's not installed). Both folders are currently empty, so playback silently no-ops until sound files are actually recorded and dropped in.

### Shared audio elements (MainLayout.razor)

`#audioPlayer` (word/syllable playback) and `#affirmationPlayer` (success/failure sounds) are both mounted once in `MainLayout.razor`, outside the splash/main-layout `@if` branches, so they're stable DOM nodes across the whole app lifetime — not owned by individual challenge pages. `StartApp()`'s "Starte App" click primes both synchronously (`k4l_primeAudioPlayer` in `wwwroot/js/interop.js`) so the browser ties audio-autoplay permission to that gesture; `k4l_playAudioFile(id, src)` is what pages call afterward to actually play a file. Keeping the two elements separate matters because a single `<audio>` element can only play one source at a time — sharing one between word narration and affirmations would let either abort the other.

### Data generation pipeline (WaveSplit/, offline/authoring only)

`SplitWaveAndCompress.py` splits a raw recording (`input.opus`) into per-word `.opus` clips under `wwwroot/audio/` using silence detection, with an interactive review/trim step, then runs each word through spaCy (POS tags) + espeak/phonemizer (IPA + stress/vowel-length tags) + custom grapheme-cluster tagging to (re)generate `Model/WordMeta.g.cs`. That file is checked in as generated-but-editable: regeneration replaces everything after the `// ###Endmarker for replacement###` marker in place, so keep that marker intact if hand-editing entries. `WordMeta.Data` (and its `Tags`, e.g. `"g2p-v-f|..."` grapheme/phoneme tags) is what `SilbenTaskRegistry`'s task generators query to pick words and build distractor options.
