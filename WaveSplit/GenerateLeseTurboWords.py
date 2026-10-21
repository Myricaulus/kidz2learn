"""
Generates Model/Lesen/LeseTurboWords.g.cs for the "Lese-Turbo" (skill read_turbo): a speeded
real-word-or-nonsense decision, so it needs real words the child knows and nonsense words that
read like German but aren't.

  - Echt: the words of Model/WordMeta.g.cs (recorded for the kid, so known) - nouns with a
    capital letter, other words of 4+ letters that the frequency list confirms, and every
    multi-syllable word; WordMeta's bare syllable entries ("de", "tor") are left out.
  - Quatsch: the first syllable of one of those multi-syllable words plus the last syllable of
    another ("Ta-sche" + "E-sel" -> "tasel"), kept only if neither the word nor an inflected
    look-alike (stem +/- e, en, n, s, er, ...) is in WaveSplit/data/de_50k.txt, the Silbenhammer
    catalog or WordMeta, it contains no whole known word of 4+ letters, its letters follow
    simple German spelling (no x/y/c outside ch/sch, ß only after a vowel, no vowel clash like
    "sauo"), and it isn't in
    data/lese-turbo-blocklist.txt (which also takes unsuitable real words out of Echt: names,
    misspelled or wrongly capitalized WordMeta entries). Generated with a fixed seed, so a rerun only changes the list
    when its inputs change.
  - VertauschtGleich / VertauschtAnders: real words from Echt with two neighboring letters inside
    swapped (Birne -> Binre), passing the same checks as Quatsch plus a pronounceability rule
    (every consonant cluster splits into a German coda and onset). Gleich swaps two letters of the
    same height - both ascenders, both descenders or both neither - so the word's outline stays the
    same; Anders changes it (Bild -> Blid). At most one of each per real word and at most
    PAIR_LIMIT per swapped pair, so no family of words ("unter", "hinter", "drunter" -> "nt" to
    "tn") fills a list with one pattern the child could learn to spot. Reading profile, M4: a child
    reading by the word's shape takes Gleich for real more often than Anders.

Usage: uv run python WaveSplit/GenerateLeseTurboWords.py
"""

import random
import re
from pathlib import Path

WORDMETA_FILE = Path("Model/WordMeta.g.cs")
SILBENHAMMER_FILE = Path("Model/SilbenHammerWords.g.cs")
FREQUENCY_LIST = Path("WaveSplit/data/de_50k.txt")
BLOCKLIST_FILE = Path("WaveSplit/data/lese-turbo-blocklist.txt")
OUTPUT_FILE = Path("Model/Lesen/LeseTurboWords.g.cs")

QUATSCH_COUNT = 200
MIN_LENGTH = 4
MAX_LENGTH = 7
SEED = 4711
ENDINGS = ["", "e", "en", "n", "s", "er", "es", "st", "t", "el"]
WORDMETA_ENTRY = re.compile(r'\["([^"]+)"\] = new WordInfo\("([^"]+)", "[^"]*", \d+ , new\[\] \{([^}]*)\}')
ODD_SPELLING = re.compile(r"(.)\1\1|x|y|q|(?<!s)c(?!h)|(?<![aeiouäöü])ß|[^a-zäöüß]")
VOWEL_PAIR = re.compile(r"[aeiouäöü]{2,}")
GERMAN_VOWEL_PAIRS = {"ei", "ie", "au", "eu", "äu", "aa", "ee", "oo"}
VOWELS = "aeiouäöüy"
ONSETS = {"", "b", "bl", "br", "ch", "d", "dr", "f", "fl", "fr", "g", "gl", "gn", "gr", "h", "j", "k", "kl", "kn",
          "kr", "l", "m", "n", "p", "pf", "pfl", "pfr", "pl", "pr", "qu", "r", "s", "sch", "schl", "schm", "schn",
          "schr", "schw", "sk", "sp", "spl", "spr", "st", "str", "t", "tr", "v", "w", "wr", "z", "zw"}
CODAS = {"", "b", "ch", "chs", "cht", "chts", "ck", "cks", "d", "f", "ff", "ft", "g", "k", "l", "lb", "lch", "ld",
         "lf", "lk", "ll", "lm", "ls", "lt", "lz", "m", "mm", "mp", "ms", "mt", "n", "nch", "nd", "ng", "ngs", "nk",
         "nks", "nn", "ns", "nst", "nt", "nz", "p", "pf", "pp", "r", "rb", "rch", "rcht", "rd", "rf", "rg", "rk", "rl",
         "rm", "rn", "rr", "rs", "rst", "rt", "rz", "s", "sch", "ss", "st", "t", "tt", "tz", "x", "z", "ß"}
PAIR_LIMIT = 3
ASCENDERS = set("bdfhkltß")
DESCENDERS = set("gjpqy")


def load_known() -> set[str]:
    known = set()
    with FREQUENCY_LIST.open(encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if parts:
                known.add(parts[0].lower())
    known |= {w.lower() for w in re.findall(r'new\("([^"]+)", \[', SILBENHAMMER_FILE.read_text(encoding="utf-8"))}
    return known


def load_blocklist() -> set[str]:
    lines = BLOCKLIST_FILE.read_text(encoding="utf-8").splitlines()
    return {l.strip().lower() for l in lines if l.strip() and not l.startswith("#")}


def is_echt(word: str, filename: str, tags: str, known: set[str]) -> bool:
    if not word.isalpha() or len(word) < 3:
        return False
    if '"noun"' in tags:
        return word[0].isupper()
    return "-" in filename or (len(word) >= MIN_LENGTH and word.lower() in known)


def looks_real(word: str, known: set[str]) -> bool:
    for ending in ENDINGS:
        if not word.endswith(ending):
            continue
        stem = word[: len(word) - len(ending)] if ending else word
        if len(stem) >= 3 and any(stem + other in known for other in ENDINGS):
            return True
    # A whole known word inside ("sauauto") reads as a compound, not as nonsense.
    return any(word[i:j] in known for i in range(len(word)) for j in range(i + 4, len(word) + 1)
               if (i, j) != (0, len(word)))


def coda_ok(cluster: str) -> bool:
    # A lengthening h ("Fahrrad", "Lehrer") goes in front of any coda.
    return cluster in CODAS or (cluster.startswith("h") and cluster[1:] in CODAS)


def pronounceable(word: str) -> bool:
    runs = re.findall(f"[{VOWELS}]+|[^{VOWELS}]+", word)
    for i, run in enumerate(runs):
        if run[0] in VOWELS:
            continue
        if i == 0:
            if run not in ONSETS:
                return False
        elif i == len(runs) - 1:
            if not coda_ok(run):
                return False
        elif not any(coda_ok(run[:k]) and run[k:] in ONSETS for k in range(len(run) + 1)):
            return False
    return True


def outline(c: str) -> str:
    return "ascender" if c in ASCENDERS else "descender" if c in DESCENDERS else "x-height"


def transposed(echt: list[str], known: set[str], blocklist: set[str], rng: random.Random):
    chosen: dict[bool, list[str]] = {True: [], False: []}
    pairs: dict[bool, dict[str, int]] = {True: {}, False: {}}
    for word in rng.sample(echt, len(echt)):
        lower = word.lower()
        by_kind: dict[bool, list[tuple[str, str]]] = {True: [], False: []}
        for i in range(1, len(lower) - 2):
            a, b = lower[i], lower[i + 1]
            if a == b or (a in VOWELS and b in VOWELS):
                continue
            t = lower[:i] + b + a + lower[i + 2:]
            if (len(t) >= MIN_LENGTH and t not in known and t not in blocklist and not ODD_SPELLING.search(t)
                    and not looks_real(t, known)
                    and all(v in GERMAN_VOWEL_PAIRS for v in VOWEL_PAIR.findall(t)) and pronounceable(t)):
                by_kind[outline(a) == outline(b)].append((word[0] + t[1:], a + b))
        for kind, candidates in by_kind.items():
            candidates = [c for c in candidates if pairs[kind].get(c[1], 0) < PAIR_LIMIT]
            if candidates:
                text, pair = rng.choice(candidates)
                chosen[kind].append(text)
                pairs[kind][pair] = pairs[kind].get(pair, 0) + 1
    return sorted(chosen[True]), sorted(chosen[False])


def main() -> None:
    known = load_known()
    blocklist = load_blocklist()
    entries = WORDMETA_ENTRY.findall(WORDMETA_FILE.read_text(encoding="utf-8"))
    known |= {word.lower() for word, _, _ in entries}

    echt = sorted({word for word, filename, tags in entries
                   if is_echt(word, filename, tags, known) and word.lower() not in blocklist})
    silben = [filename.lower().split("-") for word, filename, tags in entries
              if "-" in filename and is_echt(word, filename, tags, known)]
    anfaenge = sorted({s[0] for s in silben})
    enden = sorted({s[-1] for s in silben})

    rng = random.Random(SEED)
    quatsch: set[str] = set()
    for _ in range(200_000):
        if len(quatsch) >= QUATSCH_COUNT:
            break
        word = rng.choice(anfaenge) + rng.choice(enden)
        if (MIN_LENGTH <= len(word) <= MAX_LENGTH and word not in blocklist
                and not ODD_SPELLING.search(word) and not looks_real(word, known)
                and all(v in GERMAN_VOWEL_PAIRS for v in VOWEL_PAIR.findall(word))):
            quatsch.add(word)

    gleich, anders = transposed(echt, known, blocklist, random.Random(SEED + 1))

    def literal(words) -> str:
        return ",\n".join(f'        "{w}"' for w in words)

    OUTPUT_FILE.write_text(
        "// <auto-generated>\n"
        "//   Generated by WaveSplit/GenerateLeseTurboWords.py - do not edit by hand.\n"
        "//   Regenerate: uv run python WaveSplit/GenerateLeseTurboWords.py\n"
        "//   Nonsense words that turn out to be real go in WaveSplit/data/lese-turbo-blocklist.txt.\n"
        "// </auto-generated>\n"
        "namespace Kidz2Learn.Model.Lesen;\n\n"
        "public static class LeseTurboWords\n{\n"
        "    /// <summary>Real words the child knows, in their own spelling (nouns capitalized).</summary>\n"
        f"    public static readonly IReadOnlyList<string> Echt =\n    [\n{literal(echt)}\n    ];\n\n"
        "    /// <summary>German-looking nonsense words, lower case; the round capitalizes some at random.</summary>\n"
        f"    public static readonly IReadOnlyList<string> Quatsch =\n    [\n{literal(sorted(quatsch))}\n    ];\n\n"
        "    /// <summary>\n"
        "    ///     Real words with two neighboring letters inside swapped, both of the same height, so the\n"
        "    ///     outline stays (Birne -> Binre); in the real word's case.\n"
        "    /// </summary>\n"
        f"    public static readonly IReadOnlyList<string> VertauschtGleich =\n    [\n{literal(gleich)}\n    ];\n\n"
        "    /// <summary>Like <see cref=\"VertauschtGleich\" />, but the two letters differ in height (Bild -> Blid).</summary>\n"
        f"    public static readonly IReadOnlyList<string> VertauschtAnders =\n    [\n{literal(anders)}\n    ];\n"
        "}\n",
        encoding="utf-8")
    print(f"{len(echt)} echte Wörter, {len(quatsch)} Quatschwörter, {len(gleich)}/{len(anders)} vertauschte "
          f"(gleicher/anderer Umriss) -> {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
