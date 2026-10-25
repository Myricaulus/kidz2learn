namespace Kidz2Learn.Model.Lesen;

/// <param name="Echt">True for a real word, false for a nonsense word.</param>
public sealed record LeseTurboWort(string Text, bool Echt);

/// <summary>
///     Rules of the "Lese-Turbo" (skill <c>read_turbo</c>, <c>LeseTurboView</c>): one word at a time,
///     "echtes Wort oder Quatschwort?" as fast as possible for 20-30 seconds, scored as one attempt
///     at the end. Words come from <see cref="LeseTurboWords" />.
/// </summary>
public static class LeseTurbo
{
    /// <summary>A word left unanswered this long counts as wrong (too slow), and the next one comes.</summary>
    public static readonly TimeSpan ZeitProWort = TimeSpan.FromSeconds(5);

    /// <summary>A round with fewer answered words doesn't count as solved, however few were wrong.</summary>
    public const int MindestWoerter = 8;

    /// <summary>
    ///     Half real, half nonsense, never a word twice in a round. A third of the nonsense words are
    ///     real words with two letters swapped, half of them keeping the word's outline (reading
    ///     profile, M4); they keep the real word's case. The other nonsense words are capitalized at
    ///     random, so a capital letter doesn't give a real noun away.
    /// </summary>
    public static LeseTurboWort Naechstes(Random rng, ISet<string> gezeigt)
    {
        var echt = rng.Next(2) == 0;
        var vertauscht = !echt && rng.Next(3) == 0;
        var liste = echt ? LeseTurboWords.Echt
            : !vertauscht ? LeseTurboWords.Quatsch
            : rng.Next(2) == 0 ? LeseTurboWords.VertauschtGleich
            : LeseTurboWords.VertauschtAnders;
        var pool = liste.Where(w => !gezeigt.Contains(w)).ToList();
        if (pool.Count == 0)
            pool = [.. liste];

        var wort = pool[rng.Next(pool.Count)];
        gezeigt.Add(wort);
        if (!echt && !vertauscht && rng.Next(2) == 0)
            wort = char.ToUpperInvariant(wort[0]) + wort[1..];
        return new LeseTurboWort(wort, echt);
    }

    /// <summary>At least <see cref="MindestWoerter" /> answered and at most a quarter of them wrong.</summary>
    public static bool Geschafft(int richtig, int falsch) =>
        richtig + falsch >= MindestWoerter && 3 * falsch <= richtig;
}
