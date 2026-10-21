using Kidz2Learn.Model.Merkmale;

namespace Kidz2Learn.Model.Tasks;

/// <summary>The kinds of reading mistake a read_precise distractor stands for, as the reading profile compares them.</summary>
public enum Distraktorart
{
    /// <summary>b↔d, p↔q (M3).</summary>
    Spiegel,

    /// <summary>ei/ie/eu/au mixed up or cut to one vowel (M7).</summary>
    Vokalpaar,

    /// <summary>Two letters or two syllables swapped (M8).</summary>
    Reihenfolge,

    /// <summary>A letter or a syllable left out (M8).</summary>
    Auslassung,

    /// <summary>d↔t, g↔k inside the word (M9).</summary>
    Stimmhaft,

    /// <summary>Another vowel, a letter of similar shape, a letter group shortened.</summary>
    Sonstige
}

/// <summary>
///     Wrong spellings for "hear a word, pick its spelling" (read_precise): every spelling one typical
///     reading mistake away from the word, sorted into <see cref="Distraktorart" />s by what
///     <see cref="FehlerartRechner" /> makes of them - the same classification the reading profile
///     uses. Spellings that sound like the word are left out (d/t at the end, f/v, ei/ai, äu/eu):
///     hearing can't tell them apart.
/// </summary>
public static class ErstleserDistraktorGenerator
{
    private const string Vokale = "aeiouäöü";

    private static readonly Dictionary<char, char[]> Buchstaben = new()
    {
        ['b'] = ['d', 'p'],
        ['d'] = ['b', 't'],
        ['p'] = ['b', 'q'],
        ['q'] = ['p'],
        ['g'] = ['k'],
        ['k'] = ['g'],
        ['t'] = ['d'],
        ['s'] = ['z'],
        ['z'] = ['s'],
        ['n'] = ['m', 'r'],
        ['m'] = ['n'],
        ['f'] = ['t'],
        ['u'] = ['n']
    };

    /// <summary>Single vowels, only where they aren't part of a vowel pair (those have <see cref="Vokalpaare" />).</summary>
    private static readonly Dictionary<char, char[]> EinzelVokale = new()
    {
        ['a'] = ['e'],
        ['e'] = ['i', 'a'],
        ['i'] = ['e'],
        ['o'] = ['u'],
        ['u'] = ['o']
    };

    private static readonly Dictionary<string, string[]> Vokalpaare = new()
    {
        ["ei"] = ["ie", "eu", "e", "i"],
        ["ie"] = ["ei"],
        ["eu"] = ["ei", "au"],
        ["au"] = ["eu", "a"]
    };

    private static readonly string[] AllePaare = ["ei", "ie", "eu", "au", "äu", "ai"];

    private static readonly Dictionary<string, string[]> Gruppen = new()
    {
        ["pf"] = ["f"],
        ["sch"] = ["ch", "s"],
        ["ch"] = ["h"],
        ["ck"] = ["k"],
        ["ss"] = ["s"],
        ["mm"] = ["m"],
        ["ll"] = ["l"],
        ["nn"] = ["n"],
        ["tz"] = ["z"]
    };

    /// <summary>
    ///     Every distractor for <paramref name="word" /> with its kind. <paramref name="silben" /> is the
    ///     syllable spelling ("Ba-na-ne", <see cref="WordInfo.filename" />) for syllable swaps and
    ///     omissions; without it, or if it doesn't spell the word, there are none.
    /// </summary>
    public static IReadOnlyList<(string Text, Distraktorart Art)> Kandidaten(string word, string? silben = null)
    {
        var isCapital = char.IsUpper(word[0]);
        var w = word.ToLowerInvariant();
        var result = new List<(string, Distraktorart)>();
        var gesehen = new HashSet<string> { w };
        foreach (var variante in Varianten(w, silben?.ToLowerInvariant()))
            if (gesehen.Add(variante) && Art(FehlerartRechner.Bestimme(w, variante)) is { } art)
                result.Add((isCapital ? char.ToUpper(variante[0]) + variante[1..] : variante, art));
        return result;
    }

    /// <summary>
    ///     <paramref name="count" /> distractors, each of another kind while the word has kinds left:
    ///     <paramref name="zuerst" /> first if the word allows it, then the other kinds in random order.
    /// </summary>
    public static List<string> Generate(string word, string? silben, int count, Random r, Distraktorart? zuerst = null)
    {
        var nachArt = Kandidaten(word, silben).GroupBy(k => k.Art)
            .ToDictionary(g => g.Key, g => g.Select(k => k.Text).ToList());
        var arten = nachArt.Keys.OrderBy(_ => r.Next()).ToList();
        if (zuerst is { } z && arten.Remove(z))
            arten.Insert(0, z);

        var result = arten.Take(count).Select(a => nachArt[a][r.Next(nachArt[a].Count)]).ToList();
        result.AddRange(nachArt.Values.SelectMany(l => l).Where(t => !result.Contains(t)).OrderBy(_ => r.Next())
            .Take(count - result.Count));
        return result;
    }

    private static Distraktorart? Art(Fehlerart art) => art switch
    {
        Fehlerart.Spiegel => Distraktorart.Spiegel,
        Fehlerart.Vokalfolge or Fehlerart.Diphthong => Distraktorart.Vokalpaar,
        Fehlerart.Vertauschung or Fehlerart.Silbenfolge => Distraktorart.Reihenfolge,
        Fehlerart.Auslassung or Fehlerart.Einfuegung => Distraktorart.Auslassung,
        Fehlerart.Stimmhaft => Distraktorart.Stimmhaft,
        Fehlerart.Keine or Fehlerart.Auslaut or Fehlerart.AnderesWort => null,
        _ => Distraktorart.Sonstige
    };

    /// <summary>All one-mistake spellings of the lower-case <paramref name="w" />, unclassified and possibly repeated.</summary>
    private static IEnumerable<string> Varianten(string w, string? silben)
    {
        for (var i = 0; i < w.Length; i++)
        {
            var ersatz = Buchstaben.GetValueOrDefault(w[i], []).AsEnumerable();
            if (!InPaar(w, i))
                ersatz = ersatz.Concat(EinzelVokale.GetValueOrDefault(w[i], []));
            foreach (var c in ersatz)
                yield return w[..i] + c + w[(i + 1)..];
        }

        foreach (var (paar, ersaetze) in Vokalpaare)
            for (var i = w.IndexOf(paar, StringComparison.Ordinal); i >= 0; i = w.IndexOf(paar, i + 1, StringComparison.Ordinal))
                foreach (var ersatz in ersaetze)
                    yield return w[..i] + ersatz + w[(i + 2)..];

        // Neighbors swapped and consonants dropped from a cluster, inside the word only.
        for (var i = 1; i + 2 < w.Length; i++)
            yield return w[..i] + w[i + 1] + w[i] + w[(i + 2)..];
        for (var i = 1; i + 1 < w.Length; i++)
            if (!Vokale.Contains(w[i]) && (!Vokale.Contains(w[i - 1]) || !Vokale.Contains(w[i + 1])))
                yield return w.Remove(i, 1);

        foreach (var (gruppe, ersaetze) in Gruppen)
            for (var i = w.IndexOf(gruppe, StringComparison.Ordinal); i >= 0; i = w.IndexOf(gruppe, i + 1, StringComparison.Ordinal))
                if (i > 0 && i + gruppe.Length < w.Length)
                    foreach (var ersatz in ersaetze)
                        yield return w[..i] + ersatz + w[(i + gruppe.Length)..];
        for (var i = 1; i + 1 < w.Length; i++)
            if ("mnsl".Contains(w[i]) && w[i - 1] != w[i] && w[i + 1] != w[i])
                yield return w[..i] + w[i] + w[i..];

        var teile = silben?.Split('-');
        if (teile is not { Length: >= 2 } || string.Concat(teile) != w)
            yield break;
        for (var j = 0; j + 1 < teile.Length; j++)
            yield return string.Concat([.. teile[..j], teile[j + 1], teile[j], .. teile[(j + 2)..]]);
        if (teile.Length >= 3)
            for (var j = 0; j < teile.Length; j++)
                yield return string.Concat(teile.Where((_, k) => k != j));
    }

    private static bool InPaar(string w, int i) =>
        AllePaare.Any(p => (i + 1 < w.Length && w.Substring(i, 2) == p) || (i > 0 && w.Substring(i - 1, 2) == p));
}
