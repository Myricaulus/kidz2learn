import os

# Pfade
names_file = "WaveSplit/names.txt"
audio_folder = "wwwroot/audio"
output_file = "WaveSplit/names_cleaned.txt"

# 1. Originale Zeilen aus names.txt lesen
with open(names_file, "r", encoding="utf-8") as f:
    names = [line.rstrip("\n") for line in f]

# 2. Dateinamen aus Audio-Ordner ohne Endung
audio_files = [
    os.path.splitext(f)[0] for f in os.listdir(audio_folder)
    if os.path.isfile(os.path.join(audio_folder, f))
]

# 5. Duplikate aus der Originaldatei erkennen und Ursache bestimmen
duplicates = []
unique_names_lower = set(x.lower() for x in audio_files[:len(audio_files)])
seen_in_file = set()
for idx, name in enumerate(names, start=1):
    name_lower = name.lower()
    if name_lower in seen_in_file:
        duplicates.append((idx, name, "aus Datei doppelt"))
    elif name_lower in unique_names_lower:
        duplicates.append((idx, name, "wegen Audio-Datei"))
    else:
        seen_in_file.add(name_lower)

# 7. In neue Datei schreiben, Duplikate überspringen
with open(output_file, "w", encoding="utf-8") as f:
    for idx, name in enumerate(names, start=1):
        if any(idx == d[0] for d in duplicates):
            continue
        f.write(name + "\n")

# 8. Duplikat-Log auf Konsole tabellarisch ausgeben
if duplicates:
    print("Entfernte Duplikate:")
    # Spaltenbreiten berechnen
    max_idx_len = max(len(str(d[0])) for d in duplicates)
    max_name_len = max(len(d[1]) for d in duplicates)
    
    # Header
    print(f"{'Zeile'.ljust(max_idx_len)}  {'Name'.ljust(max_name_len)}  Grund")
    print("-" * (max_idx_len + max_name_len + 8))
    
    # Einträge
    for idx, name, reason in duplicates:
        print(f"{str(idx).ljust(max_idx_len)}  {name.ljust(max_name_len)}  {reason}")
else:
    print("Keine Duplikate gefunden.")

print(f"\nNeue Datei erstellt: {output_file}")
