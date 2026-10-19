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
    public const int Anzahl = 16;
    public const int Spalten = 4;
    public const int MinNomen = 6;
    public const int MaxNomen = 8;

    /// <summary>Things you can touch - level 1's only nouns.</summary>
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
    ///     Nouns you can't touch. <see cref="AbstraktNomen.Verwandte" /> are level 3's traps: they
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

    /// <summary>Level 1: only touchable things among plain verbs, adjectives and small words.</summary>
    public static SuchRaster GenerateLeicht(Random rng)
    {
        var anzahl = rng.Next(MinNomen, MaxNomen + 1);
        var nomen = Mischen(Konkret, rng).Take(anzahl);
        return Build(1, nomen, Mischen(KeineNomen, rng), rng);
    }

    /// <summary>Level 2: about half of the nouns are abstract ("Sprung", "Geist").</summary>
    public static SuchRaster GenerateMittel(Random rng)
    {
        var anzahl = rng.Next(MinNomen, MaxNomen + 1);
        var abstrakt = anzahl / 2;
        var nomen = Mischen(Abstrakt.Select(a => a.Wort), rng).Take(abstrakt)
            .Concat(Mischen(Konkret, rng).Take(anzahl - abstrakt));
        return Build(2, nomen, Mischen(KeineNomen, rng), rng);
    }

    /// <summary>
    ///     Level 3: only abstract nouns, each next to a non-noun of its own word family where it has
    ///     one ("Sprung" and "springt"), so the meaning alone doesn't give the noun away.
    /// </summary>
    public static SuchRaster GenerateSchwer(Random rng)
    {
        var anzahl = rng.Next(MinNomen, MaxNomen + 1);
        var nomen = Abstrakt.OrderBy(_ => rng.Next()).Take(anzahl).ToList();
        var fallen = nomen
            .Where(n => n.Verwandte.Length > 0)
            .Select(n => n.Verwandte[rng.Next(n.Verwandte.Length)]);
        var andereVerwandte = Abstrakt.Except(nomen).SelectMany(a => a.Verwandte);
        var keineNomen = fallen.Concat(Mischen(andereVerwandte.Concat(KeineNomen), rng));
        return Build(3, nomen.Select(n => n.Wort), keineNomen, rng);
    }

    /// <summary>
    ///     Fills the grid up to <see cref="Anzahl" /> with <paramref name="keineNomen" /> in the given
    ///     order (skipping repeats), shuffles it and writes it all in lower case. Points
    ///     scale with the number of words to read, like every reading task.
    /// </summary>
    public static SuchRaster Build(int stufe, IEnumerable<string> nomen, IEnumerable<string> keineNomen, Random rng)
    {
        var woerter = nomen.Select(n => (Wort: n, IstNomen: true))
            .Concat(keineNomen.Select(k => (Wort: k, IstNomen: false)))
            .DistinctBy(w => w.Wort, StringComparer.OrdinalIgnoreCase)
            .Take(Anzahl);

        var felder = woerter
            .OrderBy(_ => rng.Next())
            .Select(w => new RasterFeld(w.Wort.ToLowerInvariant(), w.IstNomen))
            .ToList();
        var punkte = LesePunkte.ForText([.. felder.Select(f => f.Text)]);
        return new SuchRaster("Nomen", Spalten, felder, punkte, $"Nomen{stufe}");
    }
}
