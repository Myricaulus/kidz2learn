# CLAUDE.md

Kidz4Learn is a German-language Blazor WebAssembly (.NET 8) learning game for kids: small
math/reading challenges with points, combos and a C64-style SID music player. Static and fully
client-side (IndexedDB, no backend), auto-deployed to Netlify. `WaveSplit/` is an offline Python
toolchain for word audio and `Model/WordMeta.g.cs`, not part of the app.

## Comment style

Comments describe the *current* state of the code (or, as a `TODO`, a concrete *better* future
state) - never the past. Don't narrate what a former version did, why it was wrong, or that
something "used to be" some other way; git history/blame already covers that, and a comment that
half-describes a dead design just wastes tokens on every re-read. When refactoring, rewrite
comments to state the new invariant/rationale directly rather than framing it as a change from the
old one.

## Commands

```bash
dotnet build Kidz2Learn.csproj
dotnet run                       # http://localhost:5171
dotnet test Kidz2Learn.Tests/Kidz2Learn.Tests.csproj
dotnet test Kidz2Learn.Tests/Kidz2Learn.Tests.csproj --filter "FullyQualifiedName~WordDiffTests"
uv sync                          # Python tooling (WaveSplit/, tools/), needs Python >= 3.13
```

- `Kidz2Learn.Tests` (xUnit, pure logic, no browser) lives in a subfolder of the main project, so
  `Kidz2Learn.csproj` excludes it (`<Compile Remove="Kidz2Learn.Tests/**/*.cs" />` etc.). Keep that,
  and exclude any new test-only folder under the repo root the same way.
- Every build first runs `tools/generate_manifests.py` (needs `python3`; a failure fails the build).
  Keep its output `Shared/AssetManifests.g.cs` checked in.

## Architecture in brief

- **Tasks** (`Model/Tasks`): one `BaseTaskDefinition` subtype per domain; each definition has a
  `Generator`, the `Skill` ids it trains, a `DifficultyLevel` and a `View` key. Register a new
  subtype in `TaskRegistry.Tasks` (startup fails fast otherwise), a new view in
  `TaskPresentationRegistry`. New task type: use the `/neue-aufgabe` skill.
- **Picking**: challenge pages are one line, `<TaskHost Skills="[...]" />`.
  `AdaptiveTaskGenerator.ChooseAnyAsync` picks a group (a definition's first skill), then a
  definition in it; harder variants unlock with that skill's mastery. 🎈/💪/💣 are relative to the
  pool, not to the learner.
- **Views** implement `ITaskView` (`ChosenTask` in, `OnNext` out), handle their own timing, and
  record answers themselves through `TaskSessionController.RecordSuccess/RecordFailure`, passing
  `BeobachtungsDaten` for the reading profile. They pass Normal-level points - the controller
  scales them by difficulty (`DifficultyInfo`).
- **Mastery** (`Model/Skills.cs`): `SkillRegistry` is the skill catalog, `SkillMasteryStore` adjusts
  mastery per answer; its mastery-model TODOs are intentionally unimplemented. Changing the skill
  schema means bumping `SkillMigrationHelper.CurrentSchemaVersion` and extending `MigrateAsync`.
- **IndexedDB** (`AufgabenDB`, versioned in `Program.cs`; a new store needs a higher version):
  stores `ArithmetikAufgaben`, `LeseAufgaben`, `SkillMeta`, `SkillStates`, `SilbenHammerRatings`,
  `Beobachtungen`, `MerkmalVerlauf`; inject with `[Inject(Key = "AufgabenDB")] IndexedDb`.
- **Audio**: `#audioPlayer` and `#affirmationPlayer` are mounted once in `MainLayout.razor` and
  primed by the "Starte App" click; play through `AudioStatusService` (`PlayWordAsync` for word
  clips), never `k4l_playAudioFile` directly, so a missing connection is noticed. A task that can't
  be answered without its clip sets `BrauchtTon`. Keep the two elements separate - one `<audio>`
  plays one source at a time.
- **Words**: `WordMeta.Data` is the word catalog. Keep the `// ###Endmarker for replacement###` in
  `Model/WordMeta.g.cs`; a duplicate key crashes its static initializer. A word a task needs that
  has no recording yet goes into `WaveSplit/names.txt` (local, gitignored).
- **Telemetry**: the endpoint never goes into the repo. Dev runs (environment `Development`) send to
  the parent's server; `telemetrie=off` in the URL keeps a run out of it.
- **Debug**: `/debug/<Page>?task=skill&skill=<id>[&level=<n>]&skipSplash=true&telemetrie=off`
  forces one kind of task.

## More docs - read the one that fits the task

| File | Read when working on |
|---|---|
| `.claude/skills/neue-aufgabe/SKILL.md` | a new task type, level or reading-profile source (`/neue-aufgabe`) |
| `docs/tasks.md` | a specific exercise or view, points/penalties, picker and mastery details, how views feed the reading profile |
| `docs/services.md` | score, HUD, difficulty rewards, SID player, affirmations, telemetry pipeline, shared audio elements |
| `docs/debug-mode.md` | forcing tasks via `/debug/...`, adding a debug override |
| `docs/recording-pipeline.md` | recording new words, `WordMeta.g.cs`, asset manifests, SID song lengths |
| `LESE_MERKMALE.md` | the reading profile's design (M1-M17, statistics), German |
| `TASK_PRESENTATION_REDESIGN.md` | why TaskHost looks the way it does (history) |
