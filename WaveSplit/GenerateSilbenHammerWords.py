"""
Generates Model/SilbenHammerWords.g.cs for the "Silbenhammer" game mode - a large, auto-generated
German word list with syllable boundaries, replacing a hand-curated list that would never reach
the vocabulary size a Grundschule kid actually has (~5000+ words).

Pipeline (see the Silbenhammer plan doc for the full rationale):
  1. Read WaveSplit/data/de_50k.txt - a German word-frequency list (word, count), sourced once
     from hermitdave/FrequencyWords (MIT-licensed code, OpenSubtitles-derived data) and checked
     into the repo for reproducible regeneration without a network dependency at build time.
     Token-based, not lemma-based, so plural forms ("Haus"/"Häuser") already appear as
     independent entries with their own frequency/rank - no separate morphology step needed.
  2. Take the most frequent tokens (FREQUENCY_CUTOFF).
  3. POS-filter via spaCy (already a WaveSplit dependency) - keep nouns/verbs/adjectives/adverbs,
     drop proper nouns/function words/numbers/symbols.
  4. Drop anything in data/silben-hammer-blocklist.txt (kid-safety - see that file's header).
  5. Hyphenate via pyphen's bundled de_DE pattern (typographic, not phonologically perfect, but
     a good approximation of spoken syllable boundaries for a reading-practice game). A pyphen
     chunk with no vowel at all (e.g. "glü-ck-lich") is not a pronounceable syllable, so it's
     merged into the preceding chunk automatically (see merge_vowelless_syllables) - and anything
     that still looks wrong can be fixed by hand in data/silben-hammer-corrections.txt, applied
     last and taking priority over both pyphen and the auto-merge.
  6. Split into Grundschule/Schlaukopf tiers by frequency rank.
  7. Write the result as a generated C# source file (Model/SilbenHammerWords.g.cs, same idea as
     Model/WordMeta.g.cs) - a plain static array literal that's part of the compiled assembly, so
     the app has the whole catalog in memory at startup with no HTTP fetch and no JSON parse at
     runtime. An earlier version shipped this as a runtime-fetched JSON asset instead; moved to
     compile time once the catalog's size stopped being a "too big for source code" concern and
     the fetch/parse round trip turned out to be the more expensive path in practice.

Usage: uv run python WaveSplit/GenerateSilbenHammerWords.py
"""

import re
from pathlib import Path

import pyphen
import spacy

FREQUENCY_LIST = Path("WaveSplit/data/de_50k.txt")
BLOCKLIST_FILE = Path("WaveSplit/data/silben-hammer-blocklist.txt")
CORRECTIONS_FILE = Path("WaveSplit/data/silben-hammer-corrections.txt")
OUTPUT_FILE = Path("Model/SilbenHammerWords.g.cs")

FREQUENCY_CUTOFF = 15000  # raw tokens considered, before POS/blocklist filtering
GRUNDSCHULE_RANK_CUTOFF = 6000  # of the *surviving* words, by frequency rank

ALPHA_ONLY = re.compile(r"^[a-zäöüßA-ZÄÖÜ]+$")
KEEP_POS = {"NOUN", "VERB", "ADJ", "ADV"}
MIN_WORD_LENGTH = 2
VOWELS = set("aeiouäöüyAEIOUÄÖÜY")


def load_frequency_words() -> list[str]:
    words = []
    with FREQUENCY_LIST.open(encoding="utf-8") as f:
        for line in f:
            parts = line.strip().split()
            if not parts:
                continue
            word = parts[0]
            if ALPHA_ONLY.match(word) and len(word) >= MIN_WORD_LENGTH:
                words.append(word)
            if len(words) >= FREQUENCY_CUTOFF:
                break
    return words


def load_blocklist() -> set[str]:
    blocked = set()
    with BLOCKLIST_FILE.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip().lower()
            if line and not line.startswith("#"):
                blocked.add(line)
    return blocked


def capitalized_if_noun(word: str, pos: str) -> str:
    if pos in ("NOUN", "PROPN"):
        return word[0].upper() + word[1:]
    return word


def has_vowel(chunk: str) -> bool:
    return any(c in VOWELS for c in chunk)


def merge_vowelless_syllables(syllables: list[str]) -> list[str]:
    """Pyphen occasionally splits off a bare consonant cluster with no vowel at all as its own
    "syllable" (e.g. "glücklich" -> glü-ck-lich, the "ck" isn't pronounceable on its own). Such a
    cluster is almost always a coda that belongs to the *preceding* syllable in German ("glück",
    not "glü"+"ck") - merge it there, or into the following one if it's the very first chunk.
    Not a full linguistic fix (a cluster that's actually an onset, e.g. inside "un-glücklich",
    still ends up glued to the wrong side sometimes) - data/silben-hammer-corrections.txt is the
    place to hand-fix anything this heuristic still gets wrong."""
    result = list(syllables)
    i = 0
    while i < len(result):
        if has_vowel(result[i]) or len(result) == 1:
            i += 1
            continue
        if i > 0:
            result[i - 1] += result[i]
            del result[i]
        else:
            result[i + 1] = result[i] + result[i + 1]
            del result[i]
    return result


def load_corrections() -> dict[str, list[str]]:
    corrections: dict[str, list[str]] = {}
    if not CORRECTIONS_FILE.exists():
        return corrections
    with CORRECTIONS_FILE.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            word, _, hyphenated = line.partition("=")
            word, hyphenated = word.strip(), hyphenated.strip()
            if word and hyphenated:
                corrections[word.lower()] = hyphenated.split("-")
    return corrections


def apply_correction(display_word: str, corrections: dict[str, list[str]]) -> list[str] | None:
    syllables = corrections.get(display_word.lower())
    if syllables is None:
        return None
    syllables = list(syllables)
    if display_word[:1].isupper() and syllables:
        syllables[0] = syllables[0][:1].upper() + syllables[0][1:]
    return syllables


def main() -> None:
    print("Lade Frequenzwortliste ...")
    candidates = load_frequency_words()
    print(f"  {len(candidates)} Kandidaten (Cutoff {FREQUENCY_CUTOFF})")

    blocklist = load_blocklist()
    print(f"Sperrliste: {len(blocklist)} Einträge")

    corrections = load_corrections()
    print(f"Silbentrennungs-Korrekturen: {len(corrections)} Einträge")

    print("Lade spaCy-Modell (de_core_news_md) ...")
    nlp = spacy.load("de_core_news_md", disable=["parser", "ner", "lemmatizer"])

    dic = pyphen.Pyphen(lang="de_DE")

    entries = []
    seen_words: set[str] = set()

    print("POS-Filter + Silbentrennung ...")
    # nlp.pipe over the raw (lowercase) tokens - German POS tagging is less reliable on
    # lowercase input than on properly-cased text (capitalization is a strong noun signal in
    # German), but the source list has no casing information to begin with.
    for token in nlp.pipe(candidates, batch_size=256):
        if len(token) == 0:
            continue
        t = token[0]
        if t.pos_ not in KEEP_POS:
            continue

        lower = t.text.lower()
        if lower in blocklist:
            continue

        display_word = capitalized_if_noun(t.text, t.pos_)
        if display_word in seen_words:
            continue
        seen_words.add(display_word)

        syllables = apply_correction(display_word, corrections)
        if syllables is None:
            syllables = merge_vowelless_syllables(dic.inserted(display_word).split("-"))

        # A word with no vowel anywhere (abbreviations like "Lkw"/"Tv" that slipped through the
        # POS filter) can't be merged into a real syllable and isn't useful for reading practice
        # regardless of hyphenation quality - drop it instead of shipping an unpronounceable entry.
        if not any(has_vowel(s) for s in syllables):
            continue

        entries.append({"word": display_word, "syllables": syllables, "rank": len(entries)})

    print(f"{len(entries)} Wörter nach Filterung/Silbentrennung")

    write_csharp(entries)

    grund_count = sum(1 for e in entries if e["rank"] < GRUNDSCHULE_RANK_CUTOFF)
    schlau_count = len(entries) - grund_count
    print(f"Geschrieben: {OUTPUT_FILE} ({len(entries)} Wörter, {grund_count} Grundschule / {schlau_count} Schlaukopf)")


def cs_string(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def write_csharp(entries: list[dict]) -> None:
    lines = [
        "// <auto-generated>",
        "//   Generated by WaveSplit/GenerateSilbenHammerWords.py - do not edit by hand.",
        "//   Regenerate: uv run python WaveSplit/GenerateSilbenHammerWords.py",
        "//   Corrections/exclusions go in WaveSplit/data/silben-hammer-corrections.txt and",
        "//   WaveSplit/data/silben-hammer-blocklist.txt, not in this file.",
        "// </auto-generated>",
        "namespace Kidz2Learn.Model;",
        "",
        "public static class SilbenHammerWords",
        "{",
        "    public static readonly IReadOnlyList<SilbenHammerWordEntry> Data =",
        "    [",
    ]

    for entry in entries:
        tier = "Grundschule" if entry["rank"] < GRUNDSCHULE_RANK_CUTOFF else "Schlaukopf"
        syllables = ",".join(cs_string(s) for s in entry["syllables"])
        lines.append(f"        new({cs_string(entry['word'])}, [{syllables}], WordTier.{tier}),")

    lines += [
        "    ];",
        "",
        "    // Built once, lazily, from Data on first access - not per Silbenhammer burst (see",
        "    // SilbenHammerSyllableIndex.Build's remarks on why that matters).",
        "    private static readonly Lazy<SilbenHammerSyllableIndex> LazyIndex =",
        "        new(() => SilbenHammerSyllableIndex.Build(Data));",
        "",
        "    public static SilbenHammerSyllableIndex Index => LazyIndex.Value;",
        "}",
        "",
    ]

    OUTPUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_FILE.write_text("\n".join(lines), encoding="utf-8")


if __name__ == "__main__":
    main()
