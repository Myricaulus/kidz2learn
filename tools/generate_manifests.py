"""Regenerates the file manifests the app loads at runtime from what is actually on disk.

    wwwroot/sids/sidfiles.json                    every *.sid in wwwroot/sids
    wwwroot/audio/affirmations/affirmations.json  {"erfolg": [...], "misserfolg": [...]} audio files

Runs before every build (Kidz2Learn.csproj, target RegenerateAudioManifests), locally and on
Netlify, so dropping in a new file and rebuilding is enough. Standard library only. Files are
sorted for a stable diff and only rewritten when their content changes.

    python3 tools/generate_manifests.py
"""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SIDS = ROOT / "wwwroot" / "sids"
AFFIRMATIONS = ROOT / "wwwroot" / "audio" / "affirmations"
AUDIO_SUFFIXES = {".opus", ".ogg", ".mp3", ".wav", ".m4a", ".webm"}


def names(folder: Path, suffixes: set[str]) -> list[str]:
    if not folder.is_dir():
        return []
    return sorted(
        (f.name for f in folder.iterdir()
         if f.is_file() and not f.name.startswith(".") and f.suffix.lower() in suffixes),
        key=str.casefold,
    )


def write_if_changed(path: Path, data) -> None:
    text = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    if path.exists() and path.read_text(encoding="utf-8-sig") == text:
        return
    path.write_text(text, encoding="utf-8")
    print(f"generate_manifests: updated {path.relative_to(ROOT)}")


def main() -> int:
    if not SIDS.is_dir():
        print(f"generate_manifests: error: {SIDS} not found", file=sys.stderr)
        return 1
    sids = names(SIDS, {".sid"})
    if not sids:
        print(f"generate_manifests: error: no .sid files in {SIDS}", file=sys.stderr)
        return 1
    write_if_changed(SIDS / "sidfiles.json", sids)

    write_if_changed(AFFIRMATIONS / "affirmations.json", {
        "erfolg": names(AFFIRMATIONS / "erfolg", AUDIO_SUFFIXES),
        "misserfolg": names(AFFIRMATIONS / "misserfolg", AUDIO_SUFFIXES),
    })
    return 0


if __name__ == "__main__":
    sys.exit(main())
