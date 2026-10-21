# Recording pipeline and asset manifests

## Python tooling

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
needs to be dropped into the folder (plus its length line, see `SidPlayerService` in `docs/services.md`). Keep `AssetManifests.g.cs` checked in:
MSBuild collects the sources before the script runs, so only an existing file is compiled.

## Data generation pipeline (WaveSplit/, offline/authoring only)

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
