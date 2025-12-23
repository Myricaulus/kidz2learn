import os
import json
import librosa
import numpy as np

INPUT_DIR = "wwwroot/audio"
OUTPUT_DIR = "wwwroot/audio_mfcc"

SAMPLE_RATE = 16000
N_MFCC = 13

os.makedirs(OUTPUT_DIR, exist_ok=True)

def extract_mfcc(path):
    y, sr = librosa.load(path, sr=SAMPLE_RATE, mono=True)

    # Silence trimming (sehr wichtig)
    y, _ = librosa.effects.trim(y, top_db=25)

    mfcc = librosa.feature.mfcc(
        y=y,
        sr=sr,
        n_mfcc=N_MFCC,
        n_fft=400,
        hop_length=160
    )

    # Transponieren: time x coeffs
    return mfcc.T.tolist(), len(y) / sr * 1000

for file in os.listdir(INPUT_DIR):
    if not file.lower().endswith(".mp3"):
        continue

    syllable = os.path.splitext(file)[0]
    path = os.path.join(INPUT_DIR, file)

    mfcc, duration_ms = extract_mfcc(path)

    target = {
        "syllable": syllable,
        "sampleRate": SAMPLE_RATE,
        "mfcc": mfcc,
        "durationMs": round(duration_ms, 1)
    }

    out_path = os.path.join(OUTPUT_DIR, f"{syllable}.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(target, f, ensure_ascii=False)

    print(f"Generated {out_path}")
