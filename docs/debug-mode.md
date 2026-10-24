# Debug mode

## Debug mode (Pages/Debug/) — forcing a specific task for manual testing

`/debug/{PageName}?task=<kind>&...` (`DebugPageWrapper.razor`) embeds any existing page/component by
name (via reflection over the assembly) and, if a `task` query param names a registered domain,
forces `AdaptiveTaskGenerator.ChooseTaskAsync<T>()` to hand back one specific task instead of
picking adaptively. This exists mainly so a specific word/skill edge case (e.g. an unusually long
word breaking a layout) can be reproduced directly by URL — in particular for testing through the
Chrome extension — instead of reloading the real page repeatedly until the adaptive picker happens
to serve it.

- `task=skill&skill=<id>[&level=<n>]` (`SkillDebugOverride`, any domain) forces the definition
  that trains that skill, each task freshly generated - e.g.
  `/debug/Lesedetektiv?task=skill&skill=read_mirror_letters&level=2&skipSplash=true&telemetrie=off`
  (`telemetrie=off` keeps a test run out of the telemetry).
- `layout=touch` / `layout=desktop` (any page) forces `MainLayout`'s touch or desktop layout,
  which otherwise follows the device's primary pointer (`pointer: coarse` = tablet/phone). The
  touch layout keeps menu (with SID player and GitHub) and Live Logger in drawers that slide over
  the task, and puts the HUD in a slim column (landscape) or a bottom strip (portrait).
- `task=silben` (`SilbenDebugOverrideFactory` in
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
