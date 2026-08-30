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
    ///     Built once (cached by <see cref="SilbenHammerWordCatalog" />) from the ~6000-entry word
    ///     catalog - must stay linear in the number of (word, syllable) pairs. An earlier version
    ///     deduped each pool entry via <c>List.Contains</c> (O(n) per insertion) and was rebuilt on
    ///     every Silbenhammer burst instead of once per page visit - together that made a common
    ///     syllable's pool (hundreds of words) quadratic and turned every few words into a
    ///     multi-second "Lade Wort" stall. Fixed by (a) O(1) HashSet-based dedup here and (b)
    ///     caching the result in the catalog instead of rebuilding it per burst.
    /// </summary>
    public static SilbenHammerSyllableIndex Build(IReadOnlyList<SilbenHammerWordEntry> words)
    {
        var first = new Dictionary<string, List<SilbenHammerWordEntry>>();
        var innerOrLast = new Dictionary<string, List<SilbenHammerWordEntry>>();
        var any = new Dictionary<string, List<SilbenHammerWordEntry>>();
        var seenKeys = new Dictionary<string, HashSet<string>>();

        foreach (var word in words)
        for (var i = 0; i < word.Syllables.Length; i++)
        {
            var key = SilbenHammerSyllableKey.Normalize(word.Syllables[i]);
            var target = i == 0 ? first : innerOrLast;

            AddOnce(target, key, word, seenKeys);
            AddOnce(any, key, word, seenKeys);
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
        // normalizing to "en") - only list it once per pool. Tracked via a separate per-key
        // HashSet (O(1) lookup) instead of List.Contains (O(n) - see remarks on Build above).
        if (seen.Add(word.Key))
            list.Add(word);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SilbenHammerWordEntry>> Freeze(
        Dictionary<string, List<SilbenHammerWordEntry>> pool)
    {
        return pool.ToDictionary(kv => kv.Key, IReadOnlyList<SilbenHammerWordEntry> (kv) => kv.Value);
    }
}
