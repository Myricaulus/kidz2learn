namespace Kidz2Learn.Model.Lesen;

/// <summary>An abstract noun plus non-noun forms of the same word family ("Sprung" - "springt").</summary>
public sealed record AbstraktNomen(string Wort, params string[] Verwandte);

/// <summary>
///     "Nomen finden": a <see cref="SuchRaster" /> of words, all in lower case so
///     capitalization gives nothing away. The hand-picked word lists keep every word unambiguous
///     on its own: no noun doubles as a verb form or adjective ("Decke"/"decke", "Arm"/"arm") and
///     no non-noun can be read as a noun ("essen"/"Essen", "laut"/"Laut", "morgen"/"Morgen") -
///     otherwise the lower-case display would make the answer a guess.
/// </summary>
public static class NomenSammlung
{

    /// <summary>Things you can touch - the only nouns of <see cref="NomenRaster.WortArt.Konkret" />.</summary>
    public static readonly IReadOnlyList<string> Konkret =
    [
        "Ball", "Tisch", "Stuhl", "Bett", "Lampe", "Fenster", "Tür", "Uhr", "Kissen", "Koffer",
        "Hund", "Katze", "Maus", "Igel", "Pferd", "Kuh", "Ente", "Fisch",
        "Apfel", "Birne", "Banane", "Tomate", "Gurke", "Brot", "Käse", "Kuchen", "Ei", "Nuss",
        "Löffel", "Gabel", "Messer", "Teller", "Tasse", "Becher", "Glas", "Flasche", "Topf",
        "Schuh", "Socke", "Hut", "Mütze", "Jacke", "Hose", "Mantel",
        "Buch", "Stift", "Schere", "Kerze", "Puppe", "Teddy", "Eimer", "Kiste", "Schlüssel", "Kamm", "Seife",
        "Baum", "Blume", "Blatt", "Stein", "Auto", "Rad", "Roller", "Haus",
        "Nase", "Hand", "Fuß", "Zahn"
    ];

    /// <summary>
    ///     Nouns you can't touch. <see cref="AbstraktNomen.Verwandte" /> are the traps of
    ///     <see cref="NomenRaster.WortArt.Abstrakt" />: they
    ///     sound like the noun but aren't one.
    /// </summary>
    public static readonly IReadOnlyList<AbstraktNomen> Abstrakt =
    [
        new("Sprung", "springt"),
        new("Geist", "spukt"),
        new("Angst", "ängstlich"),
        new("Freude", "freut"),
        new("Traum", "träumt"),
        new("Glück", "glücklich"),
        new("Wut", "wütend"),
        new("Mut", "mutig"),
        new("Witz", "witzig"),
        new("Spaß", "lustig"),
        new("Lärm", "lärmt"),
        new("Durst", "durstig"),
        new("Hunger", "hungrig"),
        new("Hilfe", "hilft"),
        new("Frage", "fragt"),
        new("Antwort", "antwortet"),
        new("Fehler", "falsch"),
        new("Spiel", "spielt"),
        new("Lied", "singt"),
        new("Wunsch", "wünscht"),
        new("Sieg", "siegt"),
        new("Streit", "streitet"),
        new("Kuss", "küsst"),
        new("Ende", "endet"),
        new("Anfang", "beginnt"),
        new("Gedanke", "denkt"),
        new("Name", "heißt"),
        new("Märchen", "erzählt"),
        new("Ruhe", "ruhig"),
        new("Idee"),
        new("Zeit"),
        new("Woche"),
        new("Ferien"),
        new("Geburtstag"),
        new("Pause")
    ];

    /// <summary>Verbs, adjectives and small words that are never nouns.</summary>
    public static readonly IReadOnlyList<string> KeineNomen =
    [
        "rennt", "lacht", "schläft", "malt", "liest", "hüpft", "baut", "kocht", "trinkt", "schreibt", "winkt",
        "rot", "blau", "grün", "gelb", "groß", "klein", "schnell", "langsam", "leise", "warm", "kalt",
        "nass", "weich", "dick", "dünn", "müde",
        "und", "mit", "unter", "auf", "neben", "hier", "dort", "sehr", "oft", "nie", "schon", "bald",
        "wir", "ihr", "sie", "er", "ich", "doch", "weil", "aber"
    ];

    private static IEnumerable<string> Mischen(IEnumerable<string> woerter, Random rng)
    {
        return woerter.OrderBy(_ => rng.Next());
    }

    /// <summary>
    ///     A grid of <paramref name="groesse" /> with nouns of <paramref name="art" />; its
    ///     <see cref="NomenRaster.Stufe" /> adds up both.
    /// </summary>
    public static SuchRaster Generate(NomenRaster.Groesse groesse, NomenRaster.WortArt art, Random rng)
    {
        var (_, _, minNomen, maxNomen) = NomenRaster.Masse(groesse);
        var anzahl = rng.Next(minNomen, maxNomen + 1);
        var (nomen, keineNomen) = art switch
        {
            NomenRaster.WortArt.Konkret => Leicht(anzahl, rng),
            NomenRaster.WortArt.Gemischt => Mittel(anzahl, rng),
            _ => Schwer(anzahl, rng)
        };
        return Build(groesse, art, nomen, keineNomen, rng);
    }

    /// <summary>Only touchable things among plain verbs, adjectives and small words.</summary>
    private static (IEnumerable<string>, IEnumerable<string>) Leicht(int anzahl, Random rng)
    {
        return (Mischen(Konkret, rng).Take(anzahl), Mischen(KeineNomen, rng));
    }

    /// <summary>About half of the nouns are abstract ("Sprung", "Geist").</summary>
    private static (IEnumerable<string>, IEnumerable<string>) Mittel(int anzahl, Random rng)
    {
        var abstrakt = anzahl / 2;
        var nomen = Mischen(Abstrakt.Select(a => a.Wort), rng).Take(abstrakt)
            .Concat(Mischen(Konkret, rng).Take(anzahl - abstrakt));
        return (nomen, Mischen(KeineNomen, rng));
    }

    /// <summary>
    ///     Only abstract nouns, each next to a non-noun of its own word family where it has one
    ///     ("Sprung" and "springt"), so the meaning alone doesn't give the noun away.
    /// </summary>
    private static (IEnumerable<string>, IEnumerable<string>) Schwer(int anzahl, Random rng)
    {
        var nomen = Abstrakt.OrderBy(_ => rng.Next()).Take(anzahl).ToList();
        var fallen = nomen
            .Where(n => n.Verwandte.Length > 0)
            .Select(n => n.Verwandte[rng.Next(n.Verwandte.Length)]);
        var andereVerwandte = Abstrakt.Except(nomen).SelectMany(a => a.Verwandte);
        return (nomen.Select(n => n.Wort), fallen.Concat(Mischen(andereVerwandte.Concat(KeineNomen), rng)));
    }

    /// <summary>
    ///     Fills the grid up to its size with <paramref name="keineNomen" /> in the given
    ///     order (skipping repeats), shuffles it and writes it all in lower case. Points
    ///     scale with the number of words to read, like every reading task.
    /// </summary>
    public static SuchRaster Build(NomenRaster.Groesse groesse, NomenRaster.WortArt art, IEnumerable<string> nomen,
        IEnumerable<string> keineNomen, Random rng)
    {
        var (spalten, anzahl, _, _) = NomenRaster.Masse(groesse);
        var woerter = nomen.Select(n => (Wort: n, IstNomen: true))
            .Concat(keineNomen.Select(k => (Wort: k, IstNomen: false)))
            .DistinctBy(w => w.Wort, StringComparer.OrdinalIgnoreCase)
            .Take(anzahl);

        var felder = woerter
            .OrderBy(_ => rng.Next())
            .Select(w => new RasterFeld(w.Wort.ToLowerInvariant(), w.IstNomen))
            .ToList();
        var punkte = LesePunkte.ForText([.. felder.Select(f => f.Text)]);
        return new SuchRaster("Nomen", spalten, felder, punkte, $"Nomen-{groesse}-{art}");
    }
}

/// <summary>
///     The two difficulty knobs of "Nomen finden". Each step on either one makes a grid one level
///     harder: a small grid with abstract nouns is as hard as a large one with touchable things.
/// </summary>
public static class NomenRaster
{
    /// <summary>3×3, 4×3 and 4×4 tiles.</summary>
    public enum Groesse
    {
        Klein,
        Mittel,
        Gross
    }

    /// <summary>Touchable nouns, half abstract ones, only abstract ones next to their word family.</summary>
    public enum WortArt
    {
        Konkret,
        Gemischt,
        Abstrakt
    }

    /// <summary>Columns, tiles, and the range of nouns among them.</summary>
    public static (int Spalten, int Anzahl, int MinNomen, int MaxNomen) Masse(Groesse groesse)
    {
        return groesse switch
        {
            Groesse.Klein => (3, 9, 3, 4),
            Groesse.Mittel => (4, 12, 4, 6),
            _ => (4, 16, 6, 8)
        };
    }

    /// <summary>1 for a small grid of touchable things, up to 5 for a large one of abstract nouns.</summary>
    public static int Stufe(Groesse groesse, WortArt art)
    {
        return 1 + (int)groesse + (int)art;
    }
}
