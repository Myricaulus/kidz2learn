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
    ///     Half real, half nonsense, never a word twice in a round. Nonsense words are capitalized at
    ///     random, so a capital letter doesn't give a real noun away.
    /// </summary>
    public static LeseTurboWort Naechstes(Random rng, ISet<string> gezeigt)
    {
        var echt = rng.Next(2) == 0;
        var pool = (echt ? LeseTurboWords.Echt : LeseTurboWords.Quatsch).Where(w => !gezeigt.Contains(w)).ToList();
        if (pool.Count == 0)
            pool = [.. echt ? LeseTurboWords.Echt : LeseTurboWords.Quatsch];

        var wort = pool[rng.Next(pool.Count)];
        gezeigt.Add(wort);
        if (!echt && rng.Next(2) == 0)
            wort = char.ToUpperInvariant(wort[0]) + wort[1..];
        return new LeseTurboWort(wort, echt);
    }

    /// <summary>At least <see cref="MindestWoerter" /> answered and at least 80 % of them right.</summary>
    public static bool Geschafft(int richtig, int falsch) =>
        richtig + falsch >= MindestWoerter && 5 * richtig >= 4 * (richtig + falsch);
}
