# Lese-Merkmale: Erkennung von Leseschwierigkeiten

Kidz4Learn schätzt aus den normalen Aufgaben, ob ein Kind eine der typischen Leseschwierigkeiten
zeigt. Das ist keine Diagnose, sondern eine plausible Einschätzung, um die richtigen nächsten
Schritte einzuleiten (Lehrkraft, LRS-Diagnostik, Orthoptist). Nur Erkennung - am Picker ändert
das nichts, und Übungen, die gezielt fördern, sind ein eigenes Thema.

## Prinzip: das Kind mit sich selbst vergleichen

Ohne Normdaten vieler Kinder gibt es keine "Wahrscheinlichkeit für LRS". Jedes Merkmal vergleicht
deshalb dieselbe Aufgabe unter zwei Bedingungen (z. B. gespiegelte Fehlschreibung gegen andere
Fehlschreibungen) oder eine Rate gegen eine feste Grenze. Jede Quelle hat zwei Hypothesen: "Problem"
(Effekt mindestens `hoch`, z. B. 15 Prozentpunkte mehr Fehler) und "keins" (höchstens `tief`, ein
Drittel davon). Ihre Evidenz ist der Likelihood-Quotient - wie viel besser die Beobachtungen zur
besser passenden Rate unter "Problem" passen als zur besser passenden unter "keins" - und

    Wert = tanh(ln(Quotient) / 3,55)   ∈ [-1, +1]     (Quotient 3 → ±0,3, 7 → ±0,5, 100 → ±0,86)

(`Model/Merkmale/Statistik.cs`). Raten werden binomial gerechnet, Zeiten log-normal, Steigungen mit
kleinsten Quadraten; bei Mittelwert und Steigung wird die Varianz auf die der t-Verteilung
verbreitert. Ohne Prior entscheidet die Datenmenge: wenige Beobachtungen passen zu beidem etwa
gleich gut und bleiben nahe 0, null Fehler können nie für ein Problem sprechen, ein seltener Fehler
mehrmals in wenigen Aufgaben (12-3=11 dreimal in acht) ist trotzdem ein klarer Hinweis, und ein
oft gemessenes "kein Unterschied" ergibt -1. Die 3,55 statt 2 (echte A-posteriori-Chancen) dämpfen,
weil Antworten innerhalb einer Sitzung nicht unabhängig sind.

Die Evidenz mehrerer Quellen eines Merkmals addiert sich (unabhängige Quotienten multiplizieren
sich). Zustände, nach dem auf zwei Stellen gerundeten Wert:

| Zustand | Bedeutung |
|---|---|
| keine Daten | keine Quelle hat ihre Mindestzahl |
| unklar | \|Wert\| < 0,3 |
| Hinweis dafür / spricht dagegen | Wert ≥ +0,3 / ≤ -0,3 |
| widersprüchlich | eine Quelle ≥ +0,5 und eine ≤ -0,5 - wird gezeigt, nicht gegeneinander verrechnet |

## Daten

- Jede beantwortete Aufgabe wird als `Beobachtung` in IndexedDB-Store `Beobachtungen` gespeichert
  (`Entities/Beobachtung.cs`, von `TaskSessionController.RecordSuccess/RecordFailure`). Die View
  liefert die aufgabenspezifischen Felder (`BeobachtungsDaten`: Ziel, Antwort, Optionen,
  Mindest-Lesezeit, Werte, Items), der Controller den Rest (Zeit, Versuch, Dauer, wie oft
  vorgespielt, welche Wörter in `LeseSatz` angetippt wurden). Unbegrenzt: ~15-20 MB im Jahr.
- Die Telemetrie bekommt dieselbe Beobachtung mit (`TelemetryEntry.Beobachtung`), damit offline
  geprüft werden kann, ob die App richtig schätzt. Sie ersetzt die In-App-Erkennung nicht.
- `/profile` rechnet über die letzten 28 Tage, zeigt daneben die 28 Tage davor
  (Erfolgskontrolle) und legt pro ISO-Woche einen Schnappschuss in `MerkmalVerlauf` ab, der als
  kleiner Verlauf erscheint.
- `MindestLeseMs` = 250 ms pro Wort (`Lesezeit`): schneller kann auch ein Erwachsener nicht
  gelesen haben, eine falsche Antwort darunter gilt als geraten.

## Merkmale

+ = Hinweis auf das Problem. "Quelle" = was heute schon Daten liefert.

| Id | Merkmal | Quellen | Fehlt noch |
|---|---|---|---|
| M1 | Crowding | Spiegelgitter eng gegen weit gesetzt (gleiche Schriftgröße, falsche Felder, ab 8 % Unterschied; grob, ohne Zeitdruck) | Kurz-Einblendung eng/weit (Schritt 4) |
| M2 | Sehschärfe/Schriftgröße | – | Kurz-Einblendung klein/groß (Schritt 4) |
| M3 | Spiegelbuchstaben | Hören→Schreibweise: Anziehung b/d, p/q; Spiegelgitter weit gesetzt, ohne Zeitdruck (eng gehört zu M1) | Bedingung "kurz eingeblendet" |
| M4 | Lesen am Wortbild | Lese-Turbo: Quatschwort nah an echtem Wort gegen fernes; vertauschtes echtes Wort mit gleichem Umriss (Birne → Brine) gegen anderem (Bild → Blid), je für echt gehalten | Nachbarn gegen de_50k |
| M5 | Kurze ähnliche Wörter | W-Frage kurzes gegen langes Fragewort; Lese-Turbo ≤4 gegen ≥6 Buchstaben | Nachbarzahl aus de_50k |
| M6 | Nicht automatisiert | Lese-Turbo: Zeit pro Wort (1,2/1,8 s), Längeneffekt (50/150 ms pro Buchstabe) | – |
| M7 | Vokalfolge ei/ie/eu | Anziehung Vokalfolge + Diphthong (ei→ie/eu/e/i, ie→ei, eu→ei/au, au→eu/a) | – |
| M8 | Reihenfolge & Auslassung | Anziehung Buchstaben-/Silbenvertauschung, Buchstaben-/Silbenauslassung, Einfügung | – |
| M9 | Laute unterscheiden | Anziehung stimmhaft/stimmlos (d/t am Wortende zählt nicht: Dialekt) | Nur-Hören-Aufgaben mi/im, Reime, gleich/anders (Schritt 3) |
| M10 | Erkennen schwächer als Erzeugen | – | "Wort sehen, Aufnahme wählen" (Schritt 3) |
| M11 | Vorlesen hilft | W-Frage: richtig nach vorgespieltem Fragewort, gegen Zufall | – |
| M12 | Raten | schnelle gegen langsamere Antworten; Lese-Turbo unter 0,4 s | – |
| M13 | Ermüdung | Fehler ab Minute 15 gegen die ersten 10 (Sitzung = Pause < 20 min) | – |
| M14 | Frust/Aufgeben | schnell & falsch nach zwei Fehlern; Suchgitter mit vielen Fehltippern | Abbruch/Seitenwechsel protokollieren |
| M15 | Lesen schwächer als Rechnen | erste Antworten Lesen gegen Rechnen (grob) | – |
| M16 | Kleiner-vom-Größeren | Minus mit Zehnerübergang, Antwort = Einer verkehrt (12-3=11) | – |
| M17 | Arbeitsgedächtnis | – | noch keine isolierende Aufgabe |

"Anziehung" (`MerkmalRechner.Anziehung`): in `read_precise`/`read_syllables`, erste Antworten -
wie oft eine angebotene Fehlschreibung einer Art gewählt wird, pro Mal, dass sie angeboten war,
gegen alle anderen Einzelbuchstaben-Arten. Die Art bestimmt `FehlerartRechner` aus Ziel und
Option; b↔p ist zugleich gekippt und stimmhaft/stimmlos und zählt für keins von beiden.

`read_precise` sorgt selbst dafür, dass jede Art oft genug angeboten wird
(`ErstleserDistraktorGenerator`): Es wählt erst eine `Distraktorart` (Spiegel, Vokalpaar,
Reihenfolge, Auslassung, Stimmhaft, Sonstige), dann ein Wort, das sie hergibt, und als zweite
Fehlschreibung eine andere Art. So kommen b/d-Optionen in gut einem Viertel der Aufgaben vor
statt nur bei zufällig passenden Wörtern. Fehlschreibungen, die genauso klingen wie das Wort
(d/t am Wortende, f/v, ei/ai, äu/eu), bietet es nicht an - beim Hören wären sie nicht zu
unterscheiden.

Gleichmäßig verteilt sind auch die anderen Bedingungen: W-Frage fragt in jeder Reihe nach einem
Satzteil mit langem Fragewort, wenn der Satz einen hat (sonst überwiegen Wer/Was/Wo); jedes
zweite Spiegelgitter ist eng gesetzt; ein Drittel der Lese-Turbo-Quatschwörter sind vertauschte
echte Wörter, halb mit gleichem, halb mit anderem Umriss (`LeseTurboWords.VertauschtGleich`/
`VertauschtAnders`, höchstens 3 pro vertauschtem Buchstabenpaar, damit das Kind kein Muster wie
"tn" lernt).

## Bekanntes aus der Zeit vor den Beobachtungen

Nicht in der App, aber als Kontext: mi/im wurde 2025-11 beim Hören/Sprechen verwechselt, beim
Sortieren nicht (phonologische Seite, M9); Kofferpacken mit 14 Gegenständen (spricht gegen M17);
d↔t am Wortende ist Dialekt. Telemetrie 2026-10 (7 Kinder-Sitzungen): "Wo" 7 von 14 falsch,
"Wer" 1 von 9 (M5); Silbenfehler mit ähnlicher Gestalt (unter→munter, M4).

## Schritte

1. Beobachtungsmodell, Auswertung, Profil mit Verlauf; bestehende Aufgaben protokollieren mehr. ✔
2. Nur neue Generatoren in bestehenden Views: Lese-Turbo-Vertauschungen (M4), Silben-Vertauschung
   und -Auslassung (M8), ei/eu in `read_precise` (M7), Kachelabstand im Spiegelgitter (M1 grob).
   Jede Aufgabe verteilt ihre Bedingungen selbst gleichmäßig - der Picker bleibt unberührt. ✔
3. Nur-Hören-Aufgaben und Schrift→Hören mit vorhandenen Aufnahmen (mi/im, da/de/di/do,
   die/der/das, Haus/Maus/Haut) für M9, M10.
4. Kurz-Einblendung (Fixierkreuz, 150-400 ms, Maske, Auswahl; Dauer pro Kind auf ~80 % geregelt)
   für M1, M2, M3.

Ein Vergleichskind (z. B. ein Klassenkamerad mit eigenem Gerät/Browserprofil, Eltern
einverstanden) gäbe den ersten echten Normwert.
