using System.Globalization;
using Kidz2Learn.Entities;
using Kidz2Learn.Model.Merkmale;

namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     One "ABC-Sortieren" task: <see cref="Woerter" /> in the order they are shown, to be put into
///     <see cref="Richtig" /> order.
/// </summary>
/// <param name="Stufe">1 = different first letters, 2 = at least two share one, 3 = similar words.</param>
/// <param name="Zusatz">Extra words from the skill's mastery (<see cref="AbcSortieren.ZusatzFuer" />).</param>
/// <param name="Naehe">
///     How close the words are to each other: 0 = drawn at random, higher = drawn from a smaller
///     Levenshtein neighbourhood of one seed word (<see cref="AbcSortieren.NachbarnFuer" />).
/// </param>
public sealed record AbcAufgabe(int Stufe, int Zusatz, int Naehe, IReadOnlyList<string> Woerter)
{
    public IReadOnlyList<string> Richtig { get; } = AbcSortieren.Sortiert(Woerter);

    /// <summary>Mean Levenshtein distance between all pairs of words.</summary>
    public double MittlererAbstand { get; } = AbcSortieren.MittlererAbstand(Woerter);

    public bool IstRichtig(IReadOnlyList<string> antwort) => antwort.SequenceEqual(Richtig);

    /// <summary>
    ///     For each neighbouring pair in <see cref="Richtig" />, the 0-based letter position that
    ///     decides their order.
    /// </summary>
    public IReadOnlyList<int> Entscheidungen =>
        [.. Richtig.Zip(Richtig.Skip(1)).Select(p => AbcSortieren.Entscheidung(p.First, p.Second))];

    /// <summary>
    ///     Letter positions in <paramref name="wort" /> that decide its place against its neighbours
    ///     in <see cref="Richtig" /> - what the child should have compared.
    /// </summary>
    public IReadOnlySet<int> Markierung(string wort)
    {
        var platz = Richtig.ToList().IndexOf(wort);
        var stellen = new HashSet<int>();
        foreach (var nachbar in new[] { platz - 1, platz + 1 })
        {
            if (nachbar < 0 || nachbar >= Richtig.Count)
                continue;
            var stelle = AbcSortieren.Entscheidung(wort, Richtig[nachbar]);
            // A prefix of its neighbour ("Ei" < "Eis") has no deciding letter of its own.
            if (stelle < wort.Length)
                stellen.Add(stelle);
        }
        return stellen;
    }
}

/// <summary>
///     Generator and ABC order for <see cref="AbcAufgabe" />. Words come from the Lese-Turbo list
///     of real words and the primary-school tier of the Silbenhammer catalog. Order follows DIN 5007-1: case doesn't count and ä/ö/ü sort like a/o/u; words
///     with ß or starting with an umlaut are left out so no rule beyond "letter by letter" is
///     needed.
/// </summary>
public static class AbcSortieren
{
    /// <summary>Normal-level points per word for a first-try solution; a second try earns half.</summary>
    public const int PunkteProWort = 2;

    /// <summary>Longer words don't fit a card on a phone-sized screen.</summary>
    public const int MaxBuchstaben = 10;

    /// <summary>Words that can be sorted - one per sort key, so no two of them tie.</summary>
    public static readonly IReadOnlyList<string> Pool =
    [
        .. LeseTurboWords.Echt
            .Concat(SilbenHammerWords.Data.Where(e => e.Tier == WordTier.Grundschule).Select(e => e.Word))
            .Where(w => w.Length is >= 2 and <= MaxBuchstaben && w.All(char.IsLetter) && !w.Contains('ß')
                        && !"ÄÖÜäöü".Contains(w[0]))
            .GroupBy(Schluessel)
            .Select(g => g.First())
    ];

    private static readonly string[] PoolSchluessel = [.. Pool.Select(Schluessel)];

    public static int Punkte(int woerter, int versuch) =>
        versuch <= 1 ? woerter * PunkteProWort : woerter * PunkteProWort / 2;

    /// <summary>Extra words for the skill's mastery: +1 from 40 %, +2 from 60 %, +3 from 80 %.</summary>
    public static int ZusatzFuer(float mastery) => mastery switch
    {
        >= 0.8f => 3,
        >= 0.6f => 2,
        >= 0.4f => 1,
        _ => 0
    };

    /// <summary>
    ///     Size of the seed word's neighbourhood the other words come from, by <see cref="AbcAufgabe.Naehe" />;
    ///     null = the whole pool. Smaller = lower Levenshtein distance between the words.
    /// </summary>
    public static int? NachbarnFuer(int naehe) => naehe switch
    {
        <= 0 => null,
        1 => 80,
        2 => 40,
        3 => 20,
        _ => 12
    };

    public static AbcAufgabe Generate(int stufe, float mastery, Random rng)
    {
        var zusatz = ZusatzFuer(mastery);
        var anzahl = (stufe >= 3 ? 5 : 4) + zusatz;
        var naehe = (stufe >= 3 ? 1 : 0) + zusatz;

        var woerter = Waehle(stufe, anzahl, naehe, rng);
        List<string> gemischt;
        do
        {
            gemischt = [.. woerter.OrderBy(_ => rng.Next())];
        } while (gemischt.SequenceEqual(Sortiert(gemischt)));

        return new AbcAufgabe(stufe, zusatz, naehe, gemischt);
    }

    private static List<string> Waehle(int stufe, int anzahl, int naehe, Random rng)
    {
        // Best effort: a neighbourhood too small for the level's rule (e.g. not enough different
        // first letters) is widened step by step, a seed that can't work at all is replaced.
        for (var versuch = 0; versuch < 50; versuch++)
        {
            var saat = rng.Next(Pool.Count);
            var nachbarn = naehe > 0 ? NachDistanz(saat, rng) : Zufaellig(saat, ZufallsAuswahl, rng);

            for (var groesse = NachbarnFuer(naehe) ?? nachbarn.Length; ; groesse *= 2)
            {
                var kandidaten = nachbarn.Take(groesse).Select(i => Pool[i]).OrderBy(_ => rng.Next()).ToList();
                if (Fuelle(stufe, anzahl, Pool[saat], kandidaten) is { } woerter)
                    return woerter;
                if (groesse >= nachbarn.Length)
                    break;
            }
        }

        throw new InvalidOperationException($"No ABC task with {anzahl} words at level {stufe}.");
    }

    /// <summary>Random sample the words of a task without a closeness goal are drawn from.</summary>
    private const int ZufallsAuswahl = 300;

    private static int[] Zufaellig(int saat, int anzahl, Random rng)
    {
        var auswahl = new HashSet<int>();
        while (auswahl.Count < Math.Min(anzahl, Pool.Count - 1))
            if (rng.Next(Pool.Count) is var i && i != saat)
                auswahl.Add(i);
        return [.. auswahl];
    }

    /// <summary>Every other word's index, nearest to the seed first, ties in random order.</summary>
    private static int[] NachDistanz(int saat, Random rng)
    {
        var indizes = new int[Pool.Count - 1];
        var rang = new long[Pool.Count - 1];
        for (int i = 0, n = 0; i < Pool.Count; i++)
        {
            if (i == saat)
                continue;
            indizes[n] = i;
            rang[n++] = (long)Levenshtein(PoolSchluessel[saat], PoolSchluessel[i]) << 32 | (uint)rng.Next();
        }
        Array.Sort(rang, indizes);
        return indizes;
    }

    private static List<string>? Fuelle(int stufe, int anzahl, string saat, List<string> kandidaten)
    {
        List<string> woerter = [saat];
        switch (stufe)
        {
            case 1:
                foreach (var k in kandidaten)
                    if (woerter.Count < anzahl && woerter.All(w => Schluessel(w)[0] != Schluessel(k)[0]))
                        woerter.Add(k);
                break;
            case 2:
                if (kandidaten.FirstOrDefault(k => Schluessel(k)[0] == Schluessel(saat)[0]) is not { } partner)
                    return null;
                woerter.Add(partner);
                woerter.AddRange(kandidaten.Where(k => k != partner).Take(anzahl - 2));
                break;
            default:
                woerter.AddRange(kandidaten.Take(anzahl - 1));
                break;
        }

        return woerter.Count == anzahl ? woerter : null;
    }

    /// <summary>Sort key: lower case, ä/ö/ü as a/o/u - one char per letter, so positions match the word.</summary>
    public static string Schluessel(string wort) =>
        new([.. wort.ToLower(CultureInfo.InvariantCulture).Select(c => c switch
        {
            'ä' => 'a',
            'ö' => 'o',
            'ü' => 'u',
            _ => c
        })]);

    public static IReadOnlyList<string> Sortiert(IEnumerable<string> woerter) =>
        [.. woerter.OrderBy(Schluessel, StringComparer.Ordinal)];

    /// <summary>
    ///     0-based position of the first letter that differs; for a word that is the other's prefix,
    ///     the shorter word's length.
    /// </summary>
    public static int Entscheidung(string a, string b)
    {
        var ka = Schluessel(a);
        var kb = Schluessel(b);
        var i = 0;
        while (i < ka.Length && i < kb.Length && ka[i] == kb[i])
            i++;
        return i;
    }

    public static int Levenshtein(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        Span<int> vorher = stackalloc int[b.Length + 1];
        Span<int> jetzt = stackalloc int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            vorher[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            jetzt[0] = i;
            for (var j = 1; j <= b.Length; j++)
                jetzt[j] = Math.Min(Math.Min(jetzt[j - 1] + 1, vorher[j] + 1),
                    vorher[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            var tausch = vorher;
            vorher = jetzt;
            jetzt = tausch;
        }
        return vorher[b.Length];
    }

    public static double MittlererAbstand(IReadOnlyList<string> woerter)
    {
        var paare = woerter.SelectMany((a, i) => woerter.Skip(i + 1).Select(b => Levenshtein(Schluessel(a), Schluessel(b))))
            .ToList();
        return paare.Count == 0 ? 0 : paare.Average();
    }

    /// <summary>
    ///     What one check of the order goes into the reading profile with. <c>entscheidung</c> lists
    ///     the 1-based deciding letter of each neighbouring pair in the right order, so errors can
    ///     later be split by "first letter decides" against "a later letter decides".
    /// </summary>
    /// <param name="antwort">The order the child handed in.</param>
    /// <param name="zuege">Moves (drags and swaps) since the task or the last check.</param>
    /// <param name="ersterZugMs">Time from showing the task to the first move, null if none.</param>
    public static BeobachtungsDaten Beobachtung(AbcAufgabe aufgabe, IReadOnlyList<string> antwort, int zuege,
        int? ersterZugMs)
    {
        var werte = new Dictionary<string, string>
        {
            ["stufe"] = aufgabe.Stufe.ToString(),
            ["woerter"] = aufgabe.Woerter.Count.ToString(),
            ["zusatz"] = aufgabe.Zusatz.ToString(),
            ["naehe"] = aufgabe.Naehe.ToString(),
            ["abstand"] = aufgabe.MittlererAbstand.ToString("0.00", CultureInfo.InvariantCulture),
            ["entscheidung"] = string.Join(",", aufgabe.Entscheidungen.Select(e => e + 1)),
            ["zuege"] = zuege.ToString(),
            ["falsch_platziert"] = antwort.Where((w, i) => aufgabe.Richtig[i] != w).Count().ToString()
        };
        if (ersterZugMs is { } ms)
            werte["erster_zug_ms"] = ms.ToString();

        var items = antwort
            .Select((w, i) => new BeobachtungsItem(w, null, aufgabe.Richtig[i] == w ? "richtig" : "falsch", 0, i))
            .ToList();

        return new BeobachtungsDaten(
            string.Join(" < ", aufgabe.Richtig),
            string.Join(" < ", antwort),
            aufgabe.Woerter,
            Lesezeit.Mindest([.. aufgabe.Woerter]),
            werte,
            items);
    }
}
