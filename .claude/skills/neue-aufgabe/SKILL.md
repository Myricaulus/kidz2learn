---
name: neue-aufgabe
description: Workflow for adding a new task type, a new variant/level of an existing one, or a new
  reading-profile source to kidz2learn - from the didactic design to wiring and docs. Use whenever
  the user wants a new exercise, a new Aufgabentyp, a new level, or a task that should feed a
  Lese-Merkmal.
---

# New task type

Order matters: what is expensive to change later comes first. Every answer is stored as a
`Beobachtung` for good - what gets logged wrong can't be repaired afterwards. Views and services
are cheap and come later. Each phase ends green (build + tests) on its own.

## Phase 1 - Design in prose, then STOP for the user's OK

Write a short design (German, in chat, no code) covering:

1. **Didactics:** what the child practises, for which learner problem (see the learner profile
   memory), what counts as a mistake, what a level 1 / 2 / 3 looks like, and what makes it
   impossible to solve without reading (keyword matching, guessing, picture cues).
2. **Skill:** an existing skill id, or a new one in `SkillRegistry` (domain, category); which page
   / pool it joins (`TaskHost Skills=[...]`), its level relative to that pool (🎈/💪/💣), and whether
   it is a long task (`FesteDauer`, `Model/LangeAufgaben.cs`).
3. **Reading profile:** which feature(s) M1-M17 it feeds (`LESE_MERKMALE.md`) and with which
   *comparison* - two conditions of the same task, or a rate against a fixed bound. No clear
   comparison = it feeds no feature, say so. The task spreads its conditions evenly itself; the
   picker stays untouched for now (choosing tasks by the profile comes later - don't build it in
   unasked).
4. **Logged data:** the exact `BeobachtungsDaten` (target, answer, options, `MindestLeseMs`,
   `Werte` keys, `Items`) - enough to recompute every planned source offline from telemetry.
5. **Words / audio:** new words, and which of them have no recording yet (`WordMeta`, `,false`).
6. **Persistence:** new IndexedDB store or skill-schema change? Usually neither - say which.
7. **Effort:** if the work needs more than medium thinking (e.g. exact timing, new JS interop), say
   so and recommend a level.

Then stop and wait. Don't let corrections of the design happen via code.

## Phase 2 - Model: definition, generator, registry (no view yet)

- Content / generator in `Model/` (`Model/Lesen/` for reading), task definition in
  `Model/Tasks/TaskDefs/`, registered in its registry and `TaskRegistry.Tasks`, skill in
  `SkillRegistry`; new store / skill schema only if Phase 1 said so.
- xUnit tests for the generator: every level, seeded `Random`, conditions spread as designed,
  edge cases (longest word, missing recordings). Build + tests green.

## Phase 3 - View and logging

- View implementing `ITaskView`, registered in `TaskPresentationRegistry`; `RecordSuccess`/
  `RecordFailure` with Normal-level points; `BeginTask` per scored attempt; builds the
  `BeobachtungsDaten` from Phase 1 in one small, static helper the tests can call too.
- Skill on the right page(s) / mixes; missing recordings into `WaveSplit/names.txt`.
- Build without new warnings in touched files, then a smoke test (see below).

## Phase 4 - Reading-profile source and scenarios

- The source as a function in `MerkmalRechner`'s `Definitionen`.
- Scenario tests in `MerkmalRechnerTests` built through the same helper the view uses (never a
  hand-made `Beobachtung` shape), run through `MerkmalRechner.Berechne` like `/profile` does:
  child with the problem -> "dafür", unremarkable -> "dagegen"/"unklar", too few -> "keine Daten".
  Name them so a peer reviewer would understand them.

## Smoke test and hand-over

- Smoke test in headless Chromium on an own server, port 5199 (never touch the user's 5171):
  `/debug/<Page>?task=skill&skill=<id>&level=<n>&skipSplash=true&telemetrie=off`, every level;
  read the screenshot and the console. Mandatory for a new view/page, DB schema or JS interop.
- No click-through. Hand back a short acceptance checklist of what only clicking shows, and what
  to look at on `/profile`. After the first evenings: check that the `Beobachtung`s arrive in the
  telemetry as designed.

## Docs and memory

- [ ] `docs/tasks.md`: the view, its levels, scoring
- [ ] `LESE_MERKMALE.md`: feature table (source moves from "Fehlt noch" to "Quellen")
- [ ] `CLAUDE.md` only if a *rule* changed - details belong in `docs/`
- [ ] Deferred ideas onto the todo pile memory, never GitHub issues
- [ ] Commit on branch `claude`, don't merge to master
