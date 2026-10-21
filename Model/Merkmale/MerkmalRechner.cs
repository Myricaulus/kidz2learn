using Kidz2Learn.Entities;
using Kidz2Learn.Model.Lesen;

namespace Kidz2Learn.Model.Merkmale;

public enum MerkmalStatus
{
    KeineDaten,
    Unklar,
    Dafuer,
    Dagegen,
    Widerspruechlich
}

/// <summary>
///     One source's estimate: <see cref="Evidenz" /> is the log likelihood ratio "problem" against
///     "none" (<see cref="Statistik" />), null while the source has too few observations.
/// </summary>
public sealed record QuellenErgebnis(string Name, double? Evidenz, int N, string Begruendung)
{
    public double? Wert => Evidenz is { } e ? Statistik.Wert(e) : null;
}

/// <param name="Wert">-1 (clear evidence against) to +1 (clear evidence for); null without data.</param>
/// <param name="Hinweis">Why there is no source yet, for features that need a task not built yet.</param>
public sealed record MerkmalErgebnis(
    string Id,
    string Name,
    string Beschreibung,
    double? Wert,
    MerkmalStatus Status,
    int N,
    IReadOnlyList<QuellenErgebnis> Quellen,
    string? Hinweis);

/// <summary>
///     Turns the stored <see cref="Beobachtung" />s into the reading profile (LESE_MERKMALE.md):
///     every feature compares the child with itself - the same task under two conditions, or a
///     rate against a fixed bound - so it needs no norm data. Each source weighs how much better its
///     observations fit "a problem" than "none"; the evidence of a feature's sources adds up,
///     unless one says clearly yes and another clearly no, which is shown as
///     <see cref="MerkmalStatus.Widerspruechlich" /> instead of cancelling out.
/// </summary>
public static class MerkmalRechner
{
    public const double Schwelle = 0.3;
    private const double Klar = 0.5;
    private const int MindestN = 8;
    private const int MindestGruppe = 5;

    /// <summary>A new session starts after this long without an answer.</summary>
    private static readonly TimeSpan SitzungsPause = TimeSpan.FromMinutes(20);

    private sealed record Definition(
        string Id,
        string Name,
        string Beschreibung,
        Func<IReadOnlyList<Beobachtung>, IEnumerable<QuellenErgebnis>> Quellen,
        string? Hinweis = null);

    private static readonly Definition[] Definitionen =
    [
        new("M1", "Crowding", "Buchstaben im Gedränge schwerer erkennbar als einzeln", Crowding,
            "Grob: das Gitter hat keinen Zeitdruck. Genauer misst es erst die Kurz-Einblendung (noch nicht gebaut)."),
        new("M2", "Sehschärfe / Schriftgröße", "Große Schrift hilft deutlich mehr als Abstand",
            _ => [], "Braucht die Kurz-Einblendung klein/groß (noch nicht gebaut)."),
        new("M3", "Spiegelbuchstaben", "b/d und p/q verwechselt", Spiegeln),
        new("M4", "Lesen am Wortbild", "Erkennt Wörter an der Gestalt statt Buchstabe für Buchstabe", Wortbild),
        new("M5", "Kurze ähnliche Wörter", "Kurze Wörter schwerer als lange", KurzeWoerter),
        new("M6", "Nicht automatisiert", "Wörter werden noch mühsam erlesen", Automatisiert),
        new("M7", "Vokalfolge (ei/ie/eu)", "Vokalpaare vertauscht oder verwechselt",
            b => [Anziehung(b, "Hören → Schreibweise: ei/ie, eu/ei", Fehlerart.Vokalfolge, Fehlerart.Diphthong)]),
        new("M8", "Reihenfolge & Auslassung", "Buchstaben vertauscht, Buchstaben oder Silben ausgelassen/zugefügt",
            b => [Anziehung(b, "Hören → Schreibweise: Buchstaben/Silben vertauscht oder ausgelassen",
                Fehlerart.Vertauschung, Fehlerart.Silbenfolge, Fehlerart.Auslassung, Fehlerart.Einfuegung)]),
        new("M9", "Laute unterscheiden", "Ähnliche Laute (d/t, g/k) beim Hören verwechselt",
            b => [Anziehung(b, "Hören → Schreibweise: stimmhaft/stimmlos", Fehlerart.Stimmhaft)],
            "Die Nur-Hören-Aufgaben (mi/im, Reime) fehlen noch; d/t am Wortende zählt nicht (Dialekt)."),
        new("M10", "Erkennen schwächer als Erzeugen", "Schrift → Laut schwerer als Laut → Schrift",
            _ => [], "Braucht die Aufgabe „Wort sehen, Aufnahme wählen“ (noch nicht gebaut)."),
        new("M11", "Vorlesen hilft", "Nach dem Vorspielen klappt es - das Lesen war das Hindernis", VorlesenHilft),
        new("M12", "Raten", "Schnelle Antworten, bevor gelesen sein kann, sind oft falsch", Raten),
        new("M13", "Ermüdung", "Später in der Sitzung mehr Fehler", Ermuedung),
        new("M14", "Frust / Aufgeben", "Nach Fehlern wird wahllos geantwortet", Frust),
        new("M15", "Lesen schwächer als Rechnen", "Leseaufgaben deutlich öfter falsch als Rechenaufgaben",
            LesenGegenRechnen),
        new("M16", "Kleiner-vom-Größeren", "Bei Minus mit Zehnerübergang die Einer verkehrt herum abgezogen",
            KleinerVomGroesseren),
        new("M17", "Arbeitsgedächtnis", "Mehrere Hinweise gleichzeitig behalten",
            _ => [], "Noch keine Aufgabe, die das isoliert misst.")
    ];

    public static IReadOnlyList<(string Id, string Name)> Katalog { get; } =
        Definitionen.Select(d => (d.Id, d.Name)).ToList();

    /// <summary>The profile over the observations from <paramref name="von" /> up to <paramref name="bis" />.</summary>
    public static IReadOnlyList<MerkmalErgebnis> Berechne(IEnumerable<Beobachtung> alle, DateTime von, DateTime bis)
    {
        var beobachtungen = alle.Where(b => b.Zeit >= von && b.Zeit < bis).OrderBy(b => b.Zeit).ToList();
        return Definitionen.Select(d => Zusammenfassen(d, d.Quellen(beobachtungen).ToList())).ToList();
    }

    private static MerkmalErgebnis Zusammenfassen(Definition d, IReadOnlyList<QuellenErgebnis> quellen)
    {
        var gueltig = quellen.Where(q => q.Wert is not null).ToList();
        var n = quellen.Sum(q => q.N);
        if (gueltig.Count == 0)
            return new MerkmalErgebnis(d.Id, d.Name, d.Beschreibung, null, MerkmalStatus.KeineDaten, n, quellen,
                d.Hinweis);

        // Independent sources multiply their likelihood ratios, so their evidence adds up. Rounded
        // before the state is decided, so the state always matches the value shown.
        var wert = Math.Round(Statistik.Wert(gueltig.Sum(q => q.Evidenz!.Value)), 2);
        var gerundet = gueltig.Select(q => Math.Round(q.Wert!.Value, 2)).ToList();
        var status = gerundet.Any(w => w >= Klar) && gerundet.Any(w => w <= -Klar)
            ? MerkmalStatus.Widerspruechlich
            : wert >= Schwelle
                ? MerkmalStatus.Dafuer
                : wert <= -Schwelle
                    ? MerkmalStatus.Dagegen
                    : MerkmalStatus.Unklar;
        return new MerkmalErgebnis(d.Id, d.Name, d.Beschreibung, wert, status, n, quellen, d.Hinweis);
    }

    // ---- Distractor attraction (read_precise, read_syllables) -----------------------------------

    /// <summary>
    ///     First answers in "hear a word, pick its spelling": how often an option of the given kinds
    ///     gets picked, per time it was on offer, against options of every other single-letter kind.
    ///     d/t at the end of a word (dialect) and whole other words stay out of both sides.
    /// </summary>
    private static QuellenErgebnis Anziehung(IReadOnlyList<Beobachtung> alle, string name, params Fehlerart[] arten)
    {
        int angebotenZiel = 0, gewaehltZiel = 0, angebotenRest = 0, gewaehltRest = 0;
        foreach (var b in alle.Where(b => b.View == "silben-multiple-choice" && b.Versuch == 1 &&
                                          b.Ziel is not null && b.Optionen is not null &&
                                          !b.Skills.Contains(Skill.GraphemPhonem)))
            foreach (var option in b.Optionen!)
            {
                var art = FehlerartRechner.Bestimme(b.Ziel!, option);
                if (art is Fehlerart.Keine or Fehlerart.Auslaut or Fehlerart.AnderesWort)
                    continue;
                var gewaehlt = !b.Richtig && string.Equals(option, b.Antwort, StringComparison.OrdinalIgnoreCase);
                if (arten.Contains(art))
                {
                    angebotenZiel++;
                    gewaehltZiel += gewaehlt ? 1 : 0;
                }
                else
                {
                    angebotenRest++;
                    gewaehltRest += gewaehlt ? 1 : 0;
                }
            }

        var n = angebotenZiel + angebotenRest;
        if (angebotenZiel < MindestGruppe || angebotenRest < MindestGruppe)
            return new QuellenErgebnis(name, null, n,
                $"{angebotenZiel}× angeboten, mindestens {MindestGruppe} nötig (und {MindestGruppe} andere)");
        return new QuellenErgebnis(name, Statistik.KontrastEvidenz(gewaehltZiel, angebotenZiel, gewaehltRest,
                angebotenRest, 0.15), n,
            $"gewählt {gewaehltZiel} von {angebotenZiel} ({Prozent(gewaehltZiel, angebotenZiel)}) " +
            $"gegenüber anderen Fehlschreibungen {gewaehltRest} von {angebotenRest} ({Prozent(gewaehltRest, angebotenRest)})");
    }

    // ---- Features ------------------------------------------------------------------------------

    private static List<Beobachtung> SpiegelGitter(IReadOnlyList<Beobachtung> alle) =>
        alle.Where(b => b.View == "such-raster" && b.Skills.Contains(Skill.ReadMirrorLetters) && b.Versuch == 1)
            .ToList();

    /// <summary>Wrongly tapped plus missed tiles, and all tiles, over the given grids.</summary>
    private static (int Fehler, int Felder) Zaehle(IReadOnlyCollection<Beobachtung> gitter) =>
        (gitter.Sum(b => b.Items?.Count ?? 0), gitter.Sum(b => int.TryParse(b.Wert("felder"), out var f) ? f : 0));

    private static IEnumerable<QuellenErgebnis> Crowding(IReadOnlyList<Beobachtung> alle)
    {
        var gitter = SpiegelGitter(alle).ToLookup(b => b.Wert("abstand"));
        List<Beobachtung> eng = [.. gitter["eng"]], weit = [.. gitter["weit"]];
        const string name = "Spiegelbuchstaben-Gitter: eng gegen weit gesetzt";
        if (eng.Count < 3 || weit.Count < 3)
        {
            yield return new QuellenErgebnis(name, null, eng.Count + weit.Count,
                $"{eng.Count} enge / {weit.Count} weite Gitter, je mindestens 3 nötig");
            yield break;
        }

        var (kEng, nEng) = Zaehle(eng);
        var (kWeit, nWeit) = Zaehle(weit);
        yield return new QuellenErgebnis(name, Statistik.KontrastEvidenz(kEng, nEng, kWeit, nWeit, 0.08), nEng + nWeit,
            $"falsche Felder eng {kEng} von {nEng} ({Prozent(kEng, nEng)}), weit {kWeit} von {nWeit} " +
            $"({Prozent(kWeit, nWeit)}); ab 8 % Unterschied ein Hinweis");
    }

    private static IEnumerable<QuellenErgebnis> Spiegeln(IReadOnlyList<Beobachtung> alle)
    {
        yield return Anziehung(alle, "Hören → Schreibweise: b/d, p/q", Fehlerart.Spiegel);

        // Tight grids belong to M1: crowding mistakes there aren't mirror mistakes.
        var gitter = SpiegelGitter(alle).Where(b => b.Wert("abstand") != "eng").ToList();
        var (fehler, felder) = Zaehle(gitter);
        const string name = "Spiegelbuchstaben-Gitter, weit gesetzt (ohne Zeitdruck)";
        if (gitter.Count < 3)
            yield return new QuellenErgebnis(name, null, felder, $"{gitter.Count} Gitter, mindestens 3 nötig");
        else
            yield return new QuellenErgebnis(name, Statistik.RateEvidenz(fehler, felder, 0.10, 0.03), felder,
                $"{fehler} von {felder} Feldern falsch ({Prozent(fehler, felder)}) in {gitter.Count} Gittern; " +
                "ab 10 % ein Hinweis, unter 3 % keiner");
    }

    private static IReadOnlyList<BeobachtungsItem> TurboWoerter(IReadOnlyList<Beobachtung> alle) =>
        alle.Where(b => b.View == "lese-turbo").SelectMany(b => b.Items ?? []).ToList();

    private static readonly Lazy<HashSet<string>> EchteWoerter = new(() =>
        LeseTurboWords.Echt.Concat(WordMeta.Data.Keys).Select(w => w.ToLowerInvariant()).ToHashSet());

    /// <summary>For a real word with two neighboring letters swapped: whether its outline stayed the same; otherwise null.</summary>
    private static bool? VertauschterUmriss(string text)
    {
        foreach (var echt in EchteWoerter.Value)
            if (FehlerartRechner.VertauschtBei(echt, text) is { } i)
                return FehlerartRechner.GleicherUmriss(echt[i], echt[i + 1]);
        return null;
    }

    private static IEnumerable<QuellenErgebnis> Wortbild(IReadOnlyList<Beobachtung> alle)
    {
        // Real words with two letters swapped go to the outline comparison, the other nonsense words
        // to near/far, so no word counts in both.
        var quatsch = TurboWoerter(alle).Where(w => w.Echt == false)
            .Select(w => (Wort: w, Umriss: VertauschterUmriss(w.Text))).ToList();

        // TODO: mark real-word neighbors in LeseTurboWords.g.cs offline against de_50k.txt instead
        // of the app's own small word lists.
        var andere = quatsch.Where(q => q.Umriss is null).Select(q => q.Wort).ToList();
        var nah = andere.ToLookup(w => EchteWoerter.Value.Any(e => FehlerartRechner.IstNachbar(w.Text, e)));
        yield return Kontrast("Lese-Turbo: Quatschwort nah an echtem Wort gegen fernes",
            nah[true].Count(w => w.Ergebnis == "falsch"), nah[true].Count(),
            nah[false].Count(w => w.Ergebnis == "falsch"), nah[false].Count(), 0.15, "für echt gehalten");

        var gleich = quatsch.Where(q => q.Umriss == true).Select(q => q.Wort).ToList();
        var anders = quatsch.Where(q => q.Umriss == false).Select(q => q.Wort).ToList();
        yield return Kontrast("Lese-Turbo: vertauschtes echtes Wort, gleicher Umriss (Birne → Brine) gegen anderer (Bild → Blid)",
            gleich.Count(w => w.Ergebnis == "falsch"), gleich.Count, anders.Count(w => w.Ergebnis == "falsch"),
            anders.Count, 0.15, "für echt gehalten");
    }

    private static IEnumerable<QuellenErgebnis> KurzeWoerter(IReadOnlyList<Beobachtung> alle)
    {
        var fragen = alle.Where(b => b.View == "w-frage" && b.Versuch == 1 && b.Wert("frage") is not null &&
                                     !b.Skills.Contains(Skill.ReadStoryMath)).ToList();
        var kurz = fragen.Where(b => WFragen.IstKurz(b.Wert("frage")!)).ToList();
        var lang = fragen.Where(b => !WFragen.IstKurz(b.Wert("frage")!)).ToList();
        yield return Kontrast("W-Frage: kurzes Fragewort (Wo, Wer, Was) gegen langes (Wann, Womit)",
            kurz.Count(b => !b.Richtig), kurz.Count, lang.Count(b => !b.Richtig), lang.Count, 0.15, "falsch");

        var echt = TurboWoerter(alle).Where(w => w.Echt == true).ToList();
        var kurzW = echt.Where(w => w.Text.Length <= 4).ToList();
        var langW = echt.Where(w => w.Text.Length >= 6).ToList();
        yield return Kontrast("Lese-Turbo: echte Wörter bis 4 Buchstaben gegen ab 6",
            kurzW.Count(w => w.Ergebnis != "richtig"), kurzW.Count, langW.Count(w => w.Ergebnis != "richtig"),
            langW.Count, 0.15, "falsch oder zu langsam");
    }

    private static IEnumerable<QuellenErgebnis> Automatisiert(IReadOnlyList<Beobachtung> alle)
    {
        var richtig = TurboWoerter(alle).Where(w => w.Echt == true && w.Ergebnis == "richtig" && w.Ms > 0).ToList();
        const string tempo = "Lese-Turbo: Zeit pro echtem Wort";
        if (richtig.Count < MindestN)
        {
            yield return new QuellenErgebnis(tempo, null, richtig.Count,
                $"{richtig.Count} richtig erkannte echte Wörter, mindestens {MindestN} nötig");
            yield break;
        }

        var (mean, var) = Statistik.Mittel(richtig.Select(w => Math.Log(w.Ms)).ToList());
        yield return new QuellenErgebnis(tempo, Statistik.NormalEvidenz(mean, var, Math.Log(1800), Math.Log(1200)),
            richtig.Count, $"typisch {Math.Exp(mean) / 1000:0.0} s pro Wort; ab 1,8 s ein Hinweis, unter 1,2 s keiner");

        var (slope, slopeVar) = Statistik.Steigung(richtig.Select(w => ((double)w.Text.Length, (double)w.Ms)).ToList());
        yield return new QuellenErgebnis("Lese-Turbo: Längeneffekt", Statistik.NormalEvidenz(slope, slopeVar, 150, 50),
            richtig.Count, $"{slope:0} ms mehr pro Buchstabe; ab 150 ms ein Hinweis, unter 50 ms keiner");
    }

    private static IEnumerable<QuellenErgebnis> VorlesenHilft(IReadOnlyList<Beobachtung> alle)
    {
        var danach = alle.Where(b => b.View == "w-frage" && b.Versuch >= 2 && b.Wert("hinweisAudio") == "ja" &&
                                     int.TryParse(b.Wert("optionen"), out _)).ToList();
        const string name = "W-Frage: richtig nach vorgespieltem Fragewort";
        if (danach.Count < 6)
        {
            yield return new QuellenErgebnis(name, null, danach.Count, $"{danach.Count} zweite Versuche, mindestens 6 nötig");
        }
        else
        {
            var zufall = danach.Average(b => 1.0 / Math.Max(1, int.Parse(b.Wert("optionen")!) - (b.Versuch - 1)));
            var k = danach.Count(b => b.Richtig);
            yield return new QuellenErgebnis(name, Statistik.RateEvidenz(k, danach.Count, zufall + 0.25, zufall + 0.08),
                danach.Count, $"{k} von {danach.Count} richtig ({Prozent(k, danach.Count)}), " +
                              $"Raten ergäbe {zufall:P0}");
        }
    }

    private static IEnumerable<QuellenErgebnis> Raten(IReadOnlyList<Beobachtung> alle)
    {
        var lesen = alle.Where(b => b.Versuch == 1 && b.MindestLeseMs is > 0).ToList();
        var schnell = lesen.Where(b => b.DauerMs < b.MindestLeseMs).ToList();
        var langsam = lesen.Where(b => b.DauerMs >= b.MindestLeseMs).ToList();
        yield return Kontrast("Antworten schneller als lesbar gegen langsamere", schnell.Count(b => !b.Richtig),
            schnell.Count, langsam.Count(b => !b.Richtig), langsam.Count, 0.2, "falsch");

        var woerter = TurboWoerter(alle).Where(w => w.Ergebnis != "zeit").ToList();
        var fix = woerter.Where(w => w.Ms < 400).ToList();
        var rest = woerter.Where(w => w.Ms >= 400).ToList();
        yield return Kontrast("Lese-Turbo: Antworten unter 0,4 s gegen langsamere", fix.Count(w => w.Ergebnis == "falsch"),
            fix.Count, rest.Count(w => w.Ergebnis == "falsch"), rest.Count, 0.2, "falsch");
    }

    private static IEnumerable<List<Beobachtung>> Sitzungen(IReadOnlyList<Beobachtung> alle)
    {
        var sitzung = new List<Beobachtung>();
        foreach (var b in alle)
        {
            if (sitzung.Count > 0 && b.Zeit - sitzung[^1].Zeit > SitzungsPause)
            {
                yield return sitzung;
                sitzung = [];
            }

            sitzung.Add(b);
        }

        if (sitzung.Count > 0)
            yield return sitzung;
    }

    private static IEnumerable<QuellenErgebnis> Ermuedung(IReadOnlyList<Beobachtung> alle)
    {
        var frueh = new List<Beobachtung>();
        var spaet = new List<Beobachtung>();
        foreach (var sitzung in Sitzungen(alle))
        {
            var start = sitzung[0].Zeit;
            foreach (var b in sitzung.Where(b => b.Versuch == 1 && b.Items is null))
            {
                var minute = (b.Zeit - start).TotalMinutes;
                if (minute < 10)
                    frueh.Add(b);
                else if (minute >= 15)
                    spaet.Add(b);
            }
        }

        yield return Kontrast("Fehler ab Minute 15 gegen die ersten 10 Minuten", spaet.Count(b => !b.Richtig),
            spaet.Count, frueh.Count(b => !b.Richtig), frueh.Count, 0.15, "falsch");
    }

    private static IEnumerable<QuellenErgebnis> Frust(IReadOnlyList<Beobachtung> alle)
    {
        int nachFehlern = 0, nachFehlernWahllos = 0, sonst = 0, sonstWahllos = 0;
        foreach (var sitzung in Sitzungen(alle))
            for (var i = 0; i < sitzung.Count; i++)
            {
                var b = sitzung[i];
                if (b.MindestLeseMs is not > 0)
                    continue;
                var wahllos = !b.Richtig && b.DauerMs < b.MindestLeseMs;
                if (i >= 2 && !sitzung[i - 1].Richtig && !sitzung[i - 2].Richtig)
                {
                    nachFehlern++;
                    nachFehlernWahllos += wahllos ? 1 : 0;
                }
                else
                {
                    sonst++;
                    sonstWahllos += wahllos ? 1 : 0;
                }
            }

        yield return Kontrast("Schnell & falsch nach zwei Fehlern gegen sonst", nachFehlernWahllos, nachFehlern,
            sonstWahllos, sonst, 0.15, "schnell falsch");

        var gitter = alle.Where(b => b.View == "such-raster" && b.Versuch == 1 &&
                                     int.TryParse(b.Wert("felder"), out _)).ToList();
        var wild = gitter.Count(b =>
            (b.Items?.Count(i => i.Ergebnis == "falsch-markiert") ?? 0) >= Math.Max(3, int.Parse(b.Wert("felder")!) / 4));
        const string name = "Suchgitter: viele falsch angetippte Felder";
        yield return gitter.Count < 3
            ? new QuellenErgebnis(name, null, gitter.Count, $"{gitter.Count} Gitter, mindestens 3 nötig")
            : new QuellenErgebnis(name, Statistik.RateEvidenz(wild, gitter.Count, 0.25, 0.05), gitter.Count,
                $"{wild} von {gitter.Count} Gittern mit mindestens einem Viertel falsch angetippter Felder");
    }

    private static IEnumerable<QuellenErgebnis> LesenGegenRechnen(IReadOnlyList<Beobachtung> alle)
    {
        bool Ist(Beobachtung b, string domain) =>
            b.Items is null && b.Skills.All(s => SkillRegistry.All.TryGetValue(s, out var d) && d.Domain == domain);

        var lesen = alle.Where(b => b.Versuch == 1 && Ist(b, TaskDomain.Reading)).ToList();
        var rechnen = alle.Where(b => b.Versuch == 1 && Ist(b, TaskDomain.Math)).ToList();
        yield return Kontrast("Erste Antwort falsch: Lesen gegen Rechnen (grob, Aufgaben verschieden schwer)",
            lesen.Count(b => !b.Richtig), lesen.Count, rechnen.Count(b => !b.Richtig), rechnen.Count, 0.15, "falsch");
    }

    private static IEnumerable<QuellenErgebnis> KleinerVomGroesseren(IReadOnlyList<Beobachtung> alle)
    {
        var minus = alle.Where(b => b.View == "arith-numpad" && b.Versuch == 1 && b.Wert("op") == "-" &&
                                    int.TryParse(b.Wert("a"), out var a) && int.TryParse(b.Wert("b"), out var c) &&
                                    a % 10 < c % 10).ToList();
        var treffer = minus.Count(b =>
        {
            int a = int.Parse(b.Wert("a")!), c = int.Parse(b.Wert("b")!);
            return b.Wert("antwort") == ((a / 10 - c / 10) * 10 + (c % 10 - a % 10)).ToString();
        });
        const string name = "Minus mit Zehnerübergang: Antwort wie „Einer verkehrt herum“ (12-3=11)";
        yield return minus.Count < MindestN
            ? new QuellenErgebnis(name, null, minus.Count, $"{minus.Count} solche Aufgaben, mindestens {MindestN} nötig")
            : new QuellenErgebnis(name, Statistik.RateEvidenz(treffer, minus.Count, 0.10, 0.03), minus.Count,
                $"{treffer} von {minus.Count} ({Prozent(treffer, minus.Count)}); ab 10 % ein Hinweis, unter 3 % keiner");
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private static QuellenErgebnis Kontrast(string name, int k1, int n1, int k2, int n2, double delta, string was)
    {
        if (n1 < MindestGruppe || n2 < MindestGruppe)
            return new QuellenErgebnis(name, null, n1 + n2, $"{n1} gegen {n2}, je mindestens {MindestGruppe} nötig");
        return new QuellenErgebnis(name, Statistik.KontrastEvidenz(k1, n1, k2, n2, delta), n1 + n2,
            $"{was}: {k1} von {n1} ({Prozent(k1, n1)}) gegen {k2} von {n2} ({Prozent(k2, n2)}); " +
            $"ab {delta:P0} Unterschied ein Hinweis");
    }

    private static string Prozent(int k, int n) => n == 0 ? "-" : $"{(double)k / n:P0}";
}
