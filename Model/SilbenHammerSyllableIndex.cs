namespace Kidz2Learn.Model;

/// <summary>
///     Groups the word catalog by normalized syllable text, split into three pools consumed by
///     <see cref="SilbenHammerSelector" />: words where the syllable is the first one, words where
///     it occurs in the middle/at the end, and every word containing it regardless of position
///     (used for the "same syllable" follow-up rounds).
/// </summary>
public sealed class SilbenHammerSyllableIndex
{
    public required IReadOnlyDictionary<string, IReadOnlyList<SilbenHammerWordEntry>> FirstSyllablePool { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<SilbenHammerWordEntry>> InnerOrLastSyllablePool { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<SilbenHammerWordEntry>> AnyPositionPool { get; init; }

    /// <summary>
    ///     Built once (cached by <see cref="SilbenHammerWords.Index" />) from the compile-time word
    ///     catalog. Must stay linear in the number of (word, syllable) pairs - the per-pool dedupe
    ///     in <see cref="AddOnce" /> is HashSet-based (O(1)) precisely so this scales to the whole
    ///     catalog without going quadratic on a common syllable's word count.
    /// </summary>
    public static SilbenHammerSyllableIndex Build(IReadOnlyList<SilbenHammerWordEntry> words)
    {
        var first = new Dictionary<string, List<SilbenHammerWordEntry>>();
        var innerOrLast = new Dictionary<string, List<SilbenHammerWordEntry>>();
        var any = new Dictionary<string, List<SilbenHammerWordEntry>>();

        // first/innerOrLast are mutually exclusive per (word, syllable-index) and can share one
        // "seen" tracker; any is populated independently for the very same (key, word) pairs and
        // needs its own - reusing the positional tracker for it would mark every pair "seen"
        // before any ever got a chance to record it, leaving AnyPositionPool empty.
        // SilbenHammerSelector's follow-up lookup reads exclusively from AnyPositionPool.
        var seenPositional = new Dictionary<string, HashSet<string>>();
        var seenAny = new Dictionary<string, HashSet<string>>();

        foreach (var word in words)
        for (var i = 0; i < word.Syllables.Length; i++)
        {
            var key = SilbenHammerSyllableKey.Normalize(word.Syllables[i]);
            var target = i == 0 ? first : innerOrLast;

            AddOnce(target, key, word, seenPositional);
            AddOnce(any, key, word, seenAny);
        }

        return new SilbenHammerSyllableIndex
        {
            FirstSyllablePool = Freeze(first),
            InnerOrLastSyllablePool = Freeze(innerOrLast),
            AnyPositionPool = Freeze(any)
        };
    }

    private static void AddOnce(Dictionary<string, List<SilbenHammerWordEntry>> pool, string key,
        SilbenHammerWordEntry word, Dictionary<string, HashSet<string>> seenKeys)
    {
        if (!pool.TryGetValue(key, out var list))
        {
            list = [];
            pool[key] = list;
        }

        if (!seenKeys.TryGetValue(key, out var seen))
        {
            seen = [];
            seenKeys[key] = seen;
        }

        // A word can contain the same syllable more than once (e.g. two syllables both
        // normalizing to "en") - only list it once per pool, via O(1) HashSet lookup (see
        // remarks on Build).
        if (seen.Add(word.Key))
            list.Add(word);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SilbenHammerWordEntry>> Freeze(
        Dictionary<string, List<SilbenHammerWordEntry>> pool)
    {
        return pool.ToDictionary(kv => kv.Key, IReadOnlyList<SilbenHammerWordEntry> (kv) => kv.Value);
    }
}
