"""
Generates wwwroot/data/silben-hammer-words.json for the "Silbenhammer" game mode - a large,
auto-generated German word list with syllable boundaries, replacing a hand-curated list that
would never reach the vocabulary size a Grundschule kid actually has (~5000+ words).

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
     a good approximation of spoken syllable boundaries for a reading-practice game).
  6. Split into Grundschule/Schlaukopf tiers by frequency rank.
  7. Write the result as JSON, consumed at runtime via HttpClient (like wwwroot/sids/sidfiles.json
     and wwwroot/audio/affirmations/affirmations.json) rather than baked into C# source - there
     are thousands of entries, too many for a reasonable source-code literal.

Usage: uv run python WaveSplit/GenerateSilbenHammerWords.py
"""

import json
import re
from pathlib import Path

import pyphen
import spacy

FREQUENCY_LIST = Path("WaveSplit/data/de_50k.txt")
BLOCKLIST_FILE = Path("WaveSplit/data/silben-hammer-blocklist.txt")
OUTPUT_FILE = Path("wwwroot/data/silben-hammer-words.json")

FREQUENCY_CUTOFF = 8000  # raw tokens considered, before POS/blocklist filtering
GRUNDSCHULE_RANK_CUTOFF = 3000  # of the *surviving* words, by frequency rank

ALPHA_ONLY = re.compile(r"^[a-zäöüßA-ZÄÖÜ]+$")
KEEP_POS = {"NOUN", "VERB", "ADJ", "ADV"}
MIN_WORD_LENGTH = 2


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


def main() -> None:
    print("Lade Frequenzwortliste ...")
    candidates = load_frequency_words()
    print(f"  {len(candidates)} Kandidaten (Cutoff {FREQUENCY_CUTOFF})")

    blocklist = load_blocklist()
    print(f"Sperrliste: {len(blocklist)} Einträge")

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

        hyphenated = dic.inserted(display_word)
        syllables = hyphenated.split("-")

        entries.append({"word": display_word, "syllables": syllables, "rank": len(entries)})

    print(f"{len(entries)} Wörter nach Filterung/Silbentrennung")

    output = []
    for entry in entries:
        tier = "Grundschule" if entry["rank"] < GRUNDSCHULE_RANK_CUTOFF else "Schlaukopf"
        output.append({"word": entry["word"], "syllables": entry["syllables"], "tier": tier})

    OUTPUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_FILE.open("w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, separators=(",", ":"))

    grund_count = sum(1 for o in output if o["tier"] == "Grundschule")
    schlau_count = len(output) - grund_count
    print(f"Geschrieben: {OUTPUT_FILE} ({len(output)} Wörter, {grund_count} Grundschule / {schlau_count} Schlaukopf)")


if __name__ == "__main__":
    main()
