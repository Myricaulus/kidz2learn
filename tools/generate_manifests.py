"""Regenerates the file manifests the app loads at runtime from what is actually on disk.

    wwwroot/sids/sidfiles.json                    every *.sid in wwwroot/sids, with the length of its
                                                  first subtune in seconds (null if unknown) from
                                                  tools/data/sid-songlengths.md5
    wwwroot/audio/affirmations/affirmations.json  {"erfolg": [...], "misserfolg": [...]} audio files

Runs before every build (Kidz2Learn.csproj, target RegenerateAudioManifests), locally and on
Netlify, so dropping in a new file and rebuilding is enough. Standard library only. Files are
sorted for a stable diff and only rewritten when their content changes.

    python3 tools/generate_manifests.py
"""

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SIDS = ROOT / "wwwroot" / "sids"
AFFIRMATIONS = ROOT / "wwwroot" / "audio" / "affirmations"
SONGLENGTHS = ROOT / "tools" / "data" / "sid-songlengths.md5"
AUDIO_SUFFIXES = {".opus", ".ogg", ".mp3", ".wav", ".m4a", ".webm"}


def names(folder: Path, suffixes: set[str]) -> list[str]:
    if not folder.is_dir():
        return []
    return sorted(
        (f.name for f in folder.iterdir()
         if f.is_file() and not f.name.startswith(".") and f.suffix.lower() in suffixes),
        key=str.casefold,
    )


def name_key(file_name: str) -> str:
    """"Dragon's_Ballad.sid" and HVSC's "Dragons_Ballad.sid" are the same tune."""
    return re.sub(r"[^a-z0-9]", "", file_name.lower())


def seconds(length: str) -> float:
    minutes, rest = length.split(":")
    return round(int(minutes) * 60 + float(rest), 1)


def song_lengths() -> tuple[dict[str, float], dict[str, float]]:
    """First-subtune length in seconds, by MD5 and by file name, from SONGLENGTHS."""
    by_md5, by_name, path = {}, {}, None
    if not SONGLENGTHS.is_file():
        return by_md5, by_name
    for line in SONGLENGTHS.read_text(encoding="utf-8").splitlines():
        if line.startswith("; /"):
            path = line[2:].strip()
        elif "=" in line and not line.startswith(";"):
            md5, lengths = line.split("=", 1)
            first = seconds(lengths.split()[0])
            by_md5[md5.strip()] = first
            if path:
                by_name[name_key(path.rsplit("/", 1)[-1])] = first
            path = None
    return by_md5, by_name


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
    by_md5, by_name = song_lengths()
    catalog = []
    for sid in sids:
        md5 = hashlib.md5((SIDS / sid).read_bytes()).hexdigest()
        catalog.append({"file": sid, "seconds": by_md5.get(md5, by_name.get(name_key(sid)))})
    write_if_changed(SIDS / "sidfiles.json", catalog)

    write_if_changed(AFFIRMATIONS / "affirmations.json", {
        "erfolg": names(AFFIRMATIONS / "erfolg", AUDIO_SUFFIXES),
        "misserfolg": names(AFFIRMATIONS / "misserfolg", AUDIO_SUFFIXES),
    })
    return 0


if __name__ == "__main__":
    sys.exit(main())
