# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Kidz4Learn is a German-language Blazor WebAssembly (.NET 8) learning game for kids, combining small math/reading challenges with points, combos, and a Commodore-64-style SID music player. It's a static, fully client-side app (IndexedDB for persistence, no backend), auto-deployed to Netlify. A separate Python toolchain (`WaveSplit/`) is used offline to prepare syllable audio clips and generate `Model/WordMeta.g.cs`; it is not part of the app runtime.

## Commands

Build, run, and watch (also available as VS Code tasks in `.vscode/tasks.json`):

```bash
dotnet build Kidz2Learn.csproj
dotnet run                       # serves at http://localhost:5171
dotnet watch run --project Kidz2Learn.csproj
dotnet publish Kidz2Learn.csproj
```

There is no test project in this repo — verify changes by running the app and exercising the affected challenge in the browser.

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

### Pages / challenges (Pages/)

Each challenge is a routable Blazor page split into `.razor` (markup) + `.razor.cs` (partial class code-behind), following the pattern in `ArithmeticChallenge` and `SilbenChallenge`:
- `OnInitializedAsync` wires up the relevant IndexedDB store and generates the first task via `AdaptiveTaskGenerator`.
- Answer evaluation updates `Kompetenzniveau`/`SkillState`, awards/deducts points via `ScoreService`, updates combo state via `HudStateService`, and appends a `RenderFragment` log entry via `LoggerService`.
- `SilbenChallenge` additionally drives a syllable-reading UI backed by `Model/WordMeta.g.cs` (autogenerated word/IPA/tag data) and, on a wrong answer, opens an interactive "mark your mistake" popup computed by `Model/WordDiff.cs` (Wagner-Fischer alignment producing required marks/substitutions/gaps the child must reproduce before continuing).
- `GraphemChallenge` and `TurboArithChallenge` are variant challenge pages layered on the same task/skill infrastructure.

### Cross-cutting services (Services/, singleton unless noted)

- `ScoreService` — base + decaying bonus score (bonus decays on a `System.Threading.Timer`); also self-adjusts a difficulty/pacing target from elapsed time between answers.
- `HudStateService` — combo count / timer visibility / difficulty, exposed via `OnChange` for HUD components to subscribe to.
- `LoggerService` — broadcasts `RenderFragment` log entries (`OnLogAppended`) for an in-page live log.
- `SidWidgetService` — current player volume, exposed via `OnVolumeChanged` so pages can duck/restore music (e.g. `SilbenChallenge` lowers volume while an exercise is active).
- `SidPlayerService` (scoped) — thin JS-interop wrapper around `wwwroot/js/jsSID.js`'s `window.sidPlayer` (a C64 SID chiptune player) for the `SidPlayerWidget` component; SID files live in `wwwroot/sids`, cataloged in `sidfiles.json`.

### Data generation pipeline (WaveSplit/, offline/authoring only)

`SplitWaveAndCompress.py` splits a raw recording (`input.opus`) into per-word `.opus` clips under `wwwroot/audio/` using silence detection, with an interactive review/trim step, then runs each word through spaCy (POS tags) + espeak/phonemizer (IPA + stress/vowel-length tags) + custom grapheme-cluster tagging to (re)generate `Model/WordMeta.g.cs`. That file is checked in as generated-but-editable: regeneration replaces everything after the `// ###Endmarker for replacement###` marker in place, so keep that marker intact if hand-editing entries. `WordMeta.Data` (and its `Tags`, e.g. `"g2p-v-f|..."` grapheme/phoneme tags) is what `SilbenTaskRegistry`'s task generators query to pick words and build distractor options.
