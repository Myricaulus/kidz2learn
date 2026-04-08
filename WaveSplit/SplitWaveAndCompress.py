
import re
import threading
from typing import cast
import numpy as np
import spacy
from phonemizer import phonemize
from pathlib import Path
from pydub import AudioSegment
from pydub.silence import detect_nonsilent
from pydub.playback import play
import os
import sys
import simpleaudio as sa
import sounddevice as sd
import soundfile as sf
import readchar
import queue
import argparse

# =======================
# KONFIGURATION
# =======================
INPUT_AUDIO = "WaveSplit/input.opus"
NAMES_FILE = "WaveSplit/names_cleaned.txt"
OUTPUT_DIR = "wwwroot/audio"

MIN_SILENCE_LEN = 250     # ms – Pause zwischen Wörtern
KEEP_PADDING = 100         # ms – etwas Luft am Rand behalten
THRESHOLD_OFFSET = -37     # dB unter Durchschnittslautstärke
BITRATE = "24k"           # Opus Bitrate für Sprache
REVIEW_FINETUNE_STEP = 20  # ms
BUFFER_SIZE = 1024*64
BLOCK_SIZE = 1024*64

# =======================
# Startup

q = queue.Queue(maxsize=BUFFER_SIZE)
event = threading.Event()
parser = argparse.ArgumentParser(add_help=False)
parser.add_argument(
    '-l', '--list-devices', action='store_true',
    help='show list of audio devices and exit')
args, remaining = parser.parse_known_args()

# =======================
# HILFSFUNKTIONEN
# =======================

def fatal(msg):
    print(f"\n❌ FEHLER: {msg}")
    sys.exit(1)

def load_names(path):
    if not os.path.exists(path):
        fatal(f"{path} nicht gefunden")

    with open(path, encoding="utf-8") as f:
        names = [l.strip() for l in f if l.strip()]

    if not names:
        fatal("names.txt ist leer")

    return names


def dubPlay(segment: AudioSegment):
    safe = (
        segment
        #.set_frame_rate(44100)
        #.set_channels(1)
        .set_sample_width(2)  # 16-bit PCM -> segment hat vorher sample_width=4, gibt aber chipmunk und schmiert dann mit SIGSEGV ab...
    )
    # pydub.playback 
    play(safe) # spielt gut ab, schmiert danach ab

def play_segment(segment: AudioSegment):
    safe = (
        segment
        #.set_frame_rate(44100)
        #.set_channels(1)
        .set_sample_width(2)
    )

    # simpleaudio
    play_obj = sa.play_buffer(
        safe.raw_data,
        num_channels=safe.channels,
        bytes_per_sample=safe.sample_width,
        sample_rate=safe.frame_rate
    )
    play_obj.wait_done() # spielt gut ab, schmiert danach ab

def DevicePlay(segment: AudioSegment):
    # Bekomm ich einfach nicht auf die Reihe, aus dem Audiosegment die infos so für Audiodevice vorzubereiten, 
    # dass er das abspielt, schmiert aber sicher danach trotzdem ab -.-
    # Ich befürchte einfach dass mein Pipewire irgentwie probleme verursacht beim abspielen.
        # 1️⃣ Hart normalisieren (keine halben Sachen)
    seg = (
        segment
        #.set_frame_rate(48000)
        #.set_channels(2)
        #.set_sample_width(4)   # 16-bit signed PCM
    )

    # 2️⃣ raw_data -> numpy int16
    samples = np.frombuffer(seg.raw_data, dtype=np.int32) # type: ignore

    # 3️⃣ shape korrekt machen (frames, channels)
    samples = samples.reshape(-1, seg.channels)

    # 4️⃣ Playback
    sd.play(samples, samplerate=seg.frame_rate, blocking=True)


def interactive_review(audio, ranges, names ):
    accepted = []
    range_index = 0
    print("\n" + "="*20+ "Record Reviewer" + "="*20)
    print("[Enter]=OK  [Space]=Replay  [Backspace]=Verwerfen")
    print("[PgUp]=Start-20  [Insert]=Start+20  [Del]=Ende-20  [PgDown]=Ende+20")
    
    for names_index, ( name) in enumerate(names):
        if range_index >= len(ranges):
            return accepted
        while True:
            (start, end) = ranges[range_index]
            if(end-start<100):
                print(f"✘ Segment {range_index} mit länge ({end-start})ms Verworfen                                                ")
                range_index+=1
                if range_index >= len(ranges):
                    return accepted
            else:
                break    


        padded_start = max(0, start - KEEP_PADDING)
        padded_end = min(len(audio), end + KEEP_PADDING)

        cur_start = padded_start
        cur_end = padded_end
        if name.lower().endswith(("t", "d", "ch")):
            cur_end += 100
        print(f"{names_index+1}/{len(ranges)}  Name: {name}                                                ")
        segment:AudioSegment = audio[cur_start:cur_end]
        print(f"Länge: {end-start}ms | Start: {cur_start}ms | Ende: {cur_end}ms | Pad.Länge: {cur_end-cur_start}ms                          ", end='\r', flush=True)
        while True:

            DevicePlay(segment)

            key = readchar.readkey()

            if key == readchar.key.ENTER:
                accepted.append((name, cur_start, cur_end))
                range_index+=1
                break

            elif key == readchar.key.SPACE:
                continue  # einfach nochmal abspielen

            elif key == readchar.key.BACKSPACE:
                print(f"✘ Segment {range_index} mit länge ({end-start})ms Verworfen                                                ")
                range_index+=1
                if range_index >= len(ranges):
                    return accepted
                (start, end) = ranges[range_index]
                padded_start = max(0, start - KEEP_PADDING)
                padded_end = min(len(audio), end + KEEP_PADDING)

                cur_start = padded_start
                cur_end = padded_end

            elif key == readchar.key.INSERT:
                cur_start = max(0, cur_start - REVIEW_FINETUNE_STEP)

            elif key == readchar.key.PAGE_UP:
                cur_start = min(cur_end - 50, cur_start + REVIEW_FINETUNE_STEP)

            elif key == readchar.key.PAGE_DOWN:
                cur_end = max(cur_start + 50, cur_end - REVIEW_FINETUNE_STEP)

            elif key == readchar.key.DELETE:
                cur_end = min(len(audio), cur_end + REVIEW_FINETUNE_STEP)

            segment:AudioSegment = audio[cur_start:cur_end]
            print(f"Länge: {end-start}ms | Start: {cur_start}ms | Ende: {cur_end}ms | Pad.Länge: {cur_end-cur_start}ms", end='\r', flush=True)

    return accepted


# =======================
# START
# =======================

print("▶ Lade Audio…")
if not os.path.exists(INPUT_AUDIO):
    fatal(f"{INPUT_AUDIO} nicht gefunden")

audio = AudioSegment.from_file(INPUT_AUDIO)

names = load_names(NAMES_FILE)

os.makedirs(OUTPUT_DIR, exist_ok=True)

print(f"▶ Durchschnittspegel: {audio.dBFS:.1f} dBFS")
print(f"▶ Silence Threshold: {THRESHOLD_OFFSET:.1f} dBFS")

# =======================
# SEGMENTIERUNG
# =======================

print("▶ Erkenne Sprachsegmente…")

ranges = detect_nonsilent(
    audio,
    min_silence_len=MIN_SILENCE_LEN,
    silence_thresh=THRESHOLD_OFFSET
)

if not ranges:
    fatal("Keine Segmente erkannt – Threshold zu streng?")

print(f"▶ Erkannte Segmente: {len(ranges)}")
print(f"▶ Erwartete Namen:   {len(names)}")

# =======================
# Review
# =======================

print("▶ Starte interaktives Review…")
if len(ranges)<len(names):
    fatal(f"Nicht genug Segmente erkannt. {len(ranges)}<{len(names)}")
approved = interactive_review(audio, ranges, names)
if approved is None:
    fatal(f"Review fehlgeschlagen")
    exit(1)

# =======================
# EXPORT
# =======================
#approved = [('Oma', 827, 1679), ('Arm', 1747, 2346), ('Turm', 2748, 3429), ('Ast', 3773, 4387), ('Mo-tor', 4603, 5440), ('tor', 5667, 6303), ('Nuss', 6695, 7385), ('En-te', 7767, 8389), ('en', 8834, 9276), ('te', 9829, 10283), ('Son-ne', 10780, 11436), ('son', 11895, 12409), ('ne', 13019, 13550), ('See', 14086, 14691), ('Tor-te', 15166, 15769), ('Tan-ne', 16168, 16729), ('tan', 17260, 17728), ('Tan-te', 18314, 18930), ('Na-se', 19285, 20058), ('na', 20408, 20851), ('se', 21300, 21776), ('Di-no', 22435, 22982), ('di', 23458, 23858), ('no', 24433, 24877), ('Do-se', 25452, 26045), ('do', 26474, 26889), ('Mond', 27446, 28070), ('Sand', 28323, 29157), ('Ra-dio', 29259, 29969), ('ra', 30227, 30849), ('dio', 31268, 31763), ('Er-de', 32324, 32919), ('er', 33120, 33694), ('de', 34167, 34567), ('Rad', 35072, 35745), ('Don-ner', 36326, 37152), ('don', 37316, 37810), ('ner', 38225, 38719), ('Mund', 39149, 39866), ('To-ma-te', 40187, 40956), ('to', 41349, 41776), ('Mes-ser', 42401, 42956), ('mes', 43388, 43978), ('ser', 44277, 44806), ('Ro-se', 45535, 46195), ('ro', 46709, 47138), ('Ma-tro-se', 47574, 48503), ('tro', 48774, 49261), ('Ton-ne', 49718, 50334), ('ton', 50744, 51312), ('Nest', 51798, 52388)]
print("▶ Exportiere Clips…")

for name, start, end in approved:
    clip = audio[start:end]

    out_path = os.path.join(OUTPUT_DIR, f"{name}.opus")
    clip.export(
        out_path,
        format="opus",
        codec="libopus",
        bitrate=BITRATE
    )

    duration = (end - start) / 1000
    print(f"  ✔ {name}.opus ({duration:.2f}s)")

if len(approved) != len(names):
    print("\n▶ Segment-Analyse (nach Dauer sortiert):\n")

    rows = []

    for i, (start, end) in enumerate(approved):
        duration = (end - start) / 1000
        name = names[i] if i < len(names) else "<kein Name mehr>"

        rows.append({
            "index": i + 1,
            "name": name,
            "start": start / 1000,
            "end": end / 1000,
            "duration": duration
        })

    # Nach Dauer absteigend sortieren
    rows_sorted = sorted(rows, key=lambda r: r["duration"], reverse=True)

    # Ausgabe
    for r in rows_sorted:
        print(
            f"{r['index']:03d} | "
            f"{r['name']:<20} | "
            f"Start: {r['start']:7.2f}s | "
            f"Ende: {r['end']:7.2f}s | "
            f"Dauer: {r['duration']:5.2f}s"
        )
    fatal("Anzahl Segmente ≠ Anzahl Namen. Aufnahme oder Threshold prüfen.")

print("\n✅ Fertig. Alles sauber verarbeitet.")


# -----------------------------
# Step 2 - 
# Setup
# -----------------------------
nlp = spacy.load("de_core_news_md")

OUTPUT = "Model/WordMeta.g.cs"

# -----------------------------
# Feature extraction
# -----------------------------

def grapheme_tags(word):
    tags = set()

    w = word.lower()

    # Umlaute
    if any(c in w for c in "äöü"):
        tags.add("umlaut")

    # typische Cluster
    clusters = ["sch", "ch", "ck", "tz", "pf", "sp", "st", "tr", "dr", "br", "gr", "kr"]
    for c in clusters:
        if c in w:
            tags.add(f"cluster-{c}")

    # Doppelte Konsonanten
    if re.search(r"(bb|cc|dd|ff|gg|kk|ll|mm|nn|pp|rr|ss|tt)", w):
        tags.add("double-consonant")

    # Dehnungsgraphen
    if "ie" in w:
        tags.add("ie")

    if "ei" in w or "ai" in w:
        tags.add("diphthong-ei")

    if "au" in w:
        tags.add("diphthong-au")

    return tags


def ipa_tags(word) -> tuple[str,set[str]]:
    ipa = cast(str,phonemize(word, language="de", backend="espeak", strip=True, njobs=1,with_stress=True))   
        
    tags: set[str] = set()

    vowels = set("iyɪʏeøɛœaɑɔouʊəɐ")

    # Betonung, hautpakzent
    count = 1
    for char in ipa:
        if char == "ˈ":
            break
        if char in vowels:
            count += 1

    if count>1:
        tags.add(f"pri-stressed-vocal-{count}")
        tags.add("pri-uncommon-stress")

    #Betonung, nebenakzent
    count = 1
    found = False
    for char in ipa:
        if char == "ˌ":
            found = True
        if char in vowels:
            count += 1

    if found and count>1:
        tags.add(f"sec-stressed-vocal-{count}")
        tags.add("sec-stress")

    # Vokallänge
    if "ː" in ipa:
        tags.add("long-vowel")

    return ipa, tags


def pos_tags(word):
    doc = nlp(word)
    token = doc[0]

    mapping = {
        "NOUN": "noun",
        "VERB": "verb",
        "ADJ": "adj",
        "ADV": "adv",
        "DET": "det",
        "PRON": "pron",
        "ADP": "prep",
        "CCONJ": "conj",
        "SCONJ": "conj",
        "NUM": "num"
    }

    return {mapping.get(token.pos_, "other")}


# -----------------------------
# Main processing
# -----------------------------

def process_word(word: str):
    syllables = word.count("-") + 1

    tags = set()

    tags |= grapheme_tags(word.replace("-",""))
    tags |= pos_tags(word.replace("-",""))

    ipa, ipa_t = ipa_tags(word.replace("-",""))
    tags |= ipa_t

    return {
        "word": word,
        "ipa": ipa,
        "syllables": syllables,
        "tags": sorted(tags)
    }


# -----------------------------
# C# Export
# -----------------------------

def generate_csharp(entries):
    lines = []
    lines.append("using System.Collections.Generic;")
    lines.append("")
    lines.append("// Autogenerated File by SplitWaveAndCompress.py. Content should be editable, as long as the Endmarker stays intact.")
    lines.append("public record WordInfo(string filename, string Ipa, int syllables, string[] Tags);")
    lines.append("")
    lines.append("public static class WordMeta")
    lines.append("{")
    lines.append("    public static readonly Dictionary<string, WordInfo> Data = new()")
    lines.append("    {")
    
    for e in entries:
        taglist = ", ".join(f"\"{t}\"" for t in e["tags"])
        lines.append(f"        [\"{e['word'].replace('-','')}\"] = new WordInfo(\"{e['word']}\", \"{e['ipa']}\", {e['syllables']} , new[] {{ {taglist} }}),")

    lines.append("// ###Endmarker for replacement###")
    lines.append("    };")
    lines.append("}")
    lines.append("")
    return "\n".join(lines)

def update_charp(entries):
    lines = []
    for e in entries:
        taglist = ", ".join(f"\"{t}\"" for t in e["tags"])
        lines.append(f"        [\"{e['word'].replace('-','')}\"] = new WordInfo(\"{e['word']}\", \"{e['ipa']}\", {e['syllables']} , new[] {{ {taglist} }}),")

    lines.append("// ###Endmarker for replacement###")
    lines.append("    };")
    lines.append("}")
    lines.append("")
    return "\n".join(lines)

def main():
    entries = [process_word(w) for w in sorted(names)]
    if not os.path.isfile(OUTPUT):
        cs = generate_csharp(entries)
        Path(OUTPUT).write_text(cs, encoding="utf8")
        print(f"Generated {OUTPUT} with {len(entries)} entries")
    else :
        cs = update_charp(entries)
        # Read in the file
        with open(OUTPUT, 'r',encoding="utf8") as file:
           filedata = file.read()

        # Replace the target string
        filedata = filedata.replace('// ###Endmarker for replacement###\n', cs)

        # Write the file out again
        with open(OUTPUT, 'w',encoding="utf8") as file:
            file.write(filedata)
        print(f"Updated {OUTPUT} with {len(entries)} entries")


if __name__ == "__main__":
    main()
