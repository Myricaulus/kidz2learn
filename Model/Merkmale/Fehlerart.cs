namespace Kidz2Learn.Model.Merkmale;

/// <summary>How a wrong written option differs from the target word (<see cref="FehlerartRechner" />).</summary>
public enum Fehlerart
{
    Keine,

    /// <summary>b↔d or p↔q: the letter mirrored left/right.</summary>
    Spiegel,

    /// <summary>d↔q, b↔q, d↔p: the letter flipped upside down or turned.</summary>
    Kippung,

    /// <summary>b↔p: flipped upside down and voiced/voiceless at once, so it counts for neither.</summary>
    Mehrdeutig,

    /// <summary>d↔t, g↔k: voiced and voiceless sound mixed up.</summary>
    Stimmhaft,

    /// <summary>d↔t, b↔p, g↔k at the end of the word - spoken alike there (and in dialect), not a weakness.</summary>
    Auslaut,

    /// <summary>One vowel for another (a↔e, o↔u).</summary>
    Vokal,

    /// <summary>One vowel for another inside ei/ie/eu/au/äu (ei→eu).</summary>
    Diphthong,

    /// <summary>Two neighboring vowels swapped (ei↔ie).</summary>
    Vokalfolge,

    /// <summary>Two neighboring letters swapped, not both vowels.</summary>
    Vertauschung,

    /// <summary>A letter group shortened or a consonant doubled (sch→ch, ck→k, m→mm).</summary>
    Gruppe,

    /// <summary>A letter or a short stretch (a syllable) left out.</summary>
    Auslassung,

    /// <summary>A letter or a short stretch added.</summary>
    Einfuegung,

    /// <summary>Similar shape without being a mirror image (n↔m, n↔u, f↔t).</summary>
    Formaehnlich,

    /// <summary>One other letter, none of the above.</summary>
    AndererBuchstabe,

    /// <summary>More than one change - another word altogether.</summary>
    AnderesWort
}

/// <summary>
///     Classifies a wrong written option against the target (<see cref="Bestimme" />), case
///     insensitive. Feeds the distractor-attraction sources of the reading profile
///     (<see cref="MerkmalRechner" />).
/// </summary>
public static class FehlerartRechner
{
    private const string Vokale = "aeiouäöü";

    private static readonly string[] Diphthonge = ["ei", "ie", "eu", "au", "äu", "ai"];

    private static readonly (string Gruppe, string Kurz)[] Gruppen =
        [("sch", "ch"), ("sch", "s"), ("pf", "f"), ("ch", "h"), ("ck", "k"), ("tz", "z")];

    private static readonly HashSet<(char, char)> SpiegelPaare = Paare(('b', 'd'), ('p', 'q'));
    private static readonly HashSet<(char, char)> KippPaare = Paare(('d', 'q'), ('b', 'q'), ('d', 'p'));
    private static readonly HashSet<(char, char)> StimmPaare = Paare(('d', 't'), ('g', 'k'), ('b', 'p'));

    private static readonly HashSet<(char, char)> FormPaare =
        Paare(('n', 'm'), ('n', 'u'), ('n', 'r'), ('f', 't'), ('h', 'n'), ('m', 'w'), ('i', 'l'));

    public static Fehlerart Bestimme(string ziel, string option)
    {
        var a = ziel.ToLowerInvariant();
        var b = option.ToLowerInvariant();
        if (a == b)
            return Fehlerart.Keine;

        if (a.Length == b.Length)
            return GleicheLaenge(a, b);

        if (IstGruppe(a, b) || IstGruppe(b, a))
            return Fehlerart.Gruppe;
        if (IstAusgelassen(a, b))
            return Fehlerart.Auslassung;
        if (IstAusgelassen(b, a))
            return Fehlerart.Einfuegung;
        return Fehlerart.AnderesWort;
    }

    /// <summary>
    ///     True when the two words differ by one letter replaced, added, dropped, or two neighbors
    ///     swapped - a nonsense word this close to a real one can pass for it by its shape.
    /// </summary>
    public static bool IstNachbar(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        if (a == b)
            return false;
        if (a.Length == b.Length)
        {
            var diffs = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
            return diffs.Count == 1 ||
                   (diffs.Count == 2 && diffs[1] == diffs[0] + 1 && a[diffs[0]] == b[diffs[1]] &&
                    a[diffs[1]] == b[diffs[0]]);
        }

        var (lang, kurz) = a.Length > b.Length ? (a, b) : (b, a);
        return lang.Length == kurz.Length + 1 &&
               Enumerable.Range(0, lang.Length).Any(i => lang.Remove(i, 1) == kurz);
    }

    private static Fehlerart GleicheLaenge(string a, string b)
    {
        var diffs = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
        if (diffs.Count == 2 && diffs[1] == diffs[0] + 1 && a[diffs[0]] == b[diffs[1]] && a[diffs[1]] == b[diffs[0]])
            return Vokale.Contains(a[diffs[0]]) && Vokale.Contains(a[diffs[1]])
                ? Fehlerart.Vokalfolge
                : Fehlerart.Vertauschung;
        if (diffs.Count != 1)
            return Fehlerart.AnderesWort;

        var i = diffs[0];
        var paar = (a[i], b[i]);
        if (i == a.Length - 1 && StimmPaare.Contains(paar))
            return Fehlerart.Auslaut;
        if (SpiegelPaare.Contains(paar))
            return Fehlerart.Spiegel;
        if (paar is ('b', 'p') or ('p', 'b'))
            return Fehlerart.Mehrdeutig;
        if (KippPaare.Contains(paar))
            return Fehlerart.Kippung;
        if (StimmPaare.Contains(paar))
            return Fehlerart.Stimmhaft;
        if (Vokale.Contains(a[i]) && Vokale.Contains(b[i]))
            return InDiphthong(a, i) || InDiphthong(b, i) ? Fehlerart.Diphthong : Fehlerart.Vokal;
        if (FormPaare.Contains(paar))
            return Fehlerart.Formaehnlich;
        return Fehlerart.AndererBuchstabe;
    }

    private static bool InDiphthong(string wort, int i)
    {
        return Diphthonge.Any(d =>
            (i + 1 < wort.Length && wort.Substring(i, 2) == d) || (i > 0 && wort.Substring(i - 1, 2) == d));
    }

    /// <summary>True when <paramref name="kurz" /> is <paramref name="lang" /> with a group shortened or a doubled consonant made single.</summary>
    private static bool IstGruppe(string lang, string kurz)
    {
        if (lang.Length <= kurz.Length)
            return false;
        for (var i = 0; i + 1 < lang.Length; i++)
            if (lang[i] == lang[i + 1] && !Vokale.Contains(lang[i]) && lang.Remove(i, 1) == kurz)
                return true;

        foreach (var (gruppe, ersatz) in Gruppen)
            for (var i = lang.IndexOf(gruppe, StringComparison.Ordinal);
                 i >= 0;
                 i = lang.IndexOf(gruppe, i + 1, StringComparison.Ordinal))
                if (string.Concat(lang.AsSpan(0, i), ersatz, lang.AsSpan(i + gruppe.Length)) == kurz)
                    return true;
        return false;
    }

    /// <summary>True when <paramref name="kurz" /> is <paramref name="lang" /> with 1-3 letters in a row removed.</summary>
    private static bool IstAusgelassen(string lang, string kurz)
    {
        var fehlt = lang.Length - kurz.Length;
        if (fehlt is < 1 or > 3)
            return false;
        return Enumerable.Range(0, kurz.Length + 1).Any(i => lang.Remove(i, fehlt) == kurz);
    }

    private static HashSet<(char, char)> Paare(params (char, char)[] paare)
    {
        return paare.SelectMany(p => new[] { p, (p.Item2, p.Item1) }).ToHashSet();
    }
}
