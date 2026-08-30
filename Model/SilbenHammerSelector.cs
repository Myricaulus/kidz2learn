namespace Kidz2Learn.Model;

public sealed record SilbenHammerSelectorOptions
{
    /// <summary>How many extra words (after the first) drill the same target syllable.</summary>
    public int FollowUpRounds { get; init; } = 3;

    /// <summary>
    ///     Below this score, a syllable is considered "well mastered as a word-opener" - the fresh
    ///     pick then falls back to the middle/end-of-word pool instead.
    /// </summary>
    public int MasteredFirstSyllableThreshold { get; init; } = 15;
}

/// <summary>
///     Picks the next word for one Silbenhammer "burst" (a fresh target-syllable word plus a
///     handful of same-syllable follow-ups). One instance per burst - constructed fresh whenever
///     <see cref="Components.TaskViews.SilbenHammerView" /> receives a new <see cref="IChosenTask" />,
///     mirroring how <see cref="Tasks.TaskHost" /> rebuilds <see cref="AdaptiveTaskGenerator" /> on
///     every pick. Cheap to do, unlike constructing the <see cref="SilbenHammerSyllableIndex" /> it
///     wraps: that's built once by <see cref="SilbenHammerWordCatalog" /> and passed in ready-made,
///     not rebuilt here per burst (see that class's remarks - it used to be, and that was the
///     actual multi-second "Lade Wort" bug). "No word repeats" is scoped to one burst, not the
///     whole session - with thousands of catalog words that's more than enough to avoid noticeable
///     repeats.
/// </summary>
public sealed class SilbenHammerSelector
{
    private readonly SilbenHammerSyllableIndex _index;
    private readonly ISilbenHammerRatingStore _ratingStore;
    private readonly Random _rng;
    private readonly SilbenHammerSelectorOptions _options;
    private readonly HashSet<string> _usedWordKeys = [];

    private int _followUpBudgetRemaining;

    public string? TargetSyllable { get; private set; }
    public IReadOnlyCollection<string> UsedWordKeys => _usedWordKeys;

    public SilbenHammerSelector(
        SilbenHammerSyllableIndex index,
        ISilbenHammerRatingStore ratingStore,
        Random rng,
        SilbenHammerSelectorOptions? options = null)
    {
        _index = index;
        _ratingStore = ratingStore;
        _rng = rng;
        _options = options ?? new SilbenHammerSelectorOptions();
    }

    public async Task<SilbenHammerWordEntry?> PickNextWordAsync()
    {
        if (TargetSyllable is not null && _followUpBudgetRemaining > 0)
        {
            var followUp = PickUnused(_index.AnyPositionPool.GetValueOrDefault(TargetSyllable));
            if (followUp is not null)
            {
                _followUpBudgetRemaining--;
                _usedWordKeys.Add(followUp.Key);
                return followUp;
            }

            // No unused word left for this syllable, even though the follow-up budget isn't
            // spent yet - fall through to a fresh target pick instead of returning null early.
        }

        return await PickFreshTargetAsync();
    }

    private async Task<SilbenHammerWordEntry?> PickFreshTargetAsync()
    {
        if (_index.FirstSyllablePool.Count == 0 && _index.InnerOrLastSyllablePool.Count == 0)
            return null;

        var streaks = await _ratingStore.GetAllCleanStreaksAsync();

        int Score(string syllable)
        {
            return SilbenHammerScoring.ComputeScore(streaks.GetValueOrDefault(syllable, 0));
        }

        string chosen;
        IReadOnlyDictionary<string, IReadOnlyList<SilbenHammerWordEntry>> pool;

        if (_index.FirstSyllablePool.Count > 0)
        {
            chosen = WeightedPick(_index.FirstSyllablePool.Keys, Score);
            pool = _index.FirstSyllablePool;

            if (Score(chosen) < _options.MasteredFirstSyllableThreshold
                && _index.InnerOrLastSyllablePool.Count > 0)
            {
                chosen = WeightedPick(_index.InnerOrLastSyllablePool.Keys, Score);
                pool = _index.InnerOrLastSyllablePool;
            }
        }
        else
        {
            chosen = WeightedPick(_index.InnerOrLastSyllablePool.Keys, Score);
            pool = _index.InnerOrLastSyllablePool;
        }

        var word = PickUnused(pool.GetValueOrDefault(chosen))
            ?? PickUnused(_index.AnyPositionPool.GetValueOrDefault(chosen));

        if (word is null)
        {
            // Every word containing this syllable has already been used this burst - reset the
            // burst-local "used" set (a full "lap") and try once more, unconstrained.
            _usedWordKeys.Clear();
            word = PickUnused(_index.AnyPositionPool.GetValueOrDefault(chosen));
        }

        if (word is null)
            return null;

        TargetSyllable = chosen;
        _followUpBudgetRemaining = _options.FollowUpRounds;
        _usedWordKeys.Add(word.Key);
        return word;
    }

    private SilbenHammerWordEntry? PickUnused(IReadOnlyList<SilbenHammerWordEntry>? candidates)
    {
        if (candidates is null || candidates.Count == 0)
            return null;

        var unused = candidates.Where(w => !_usedWordKeys.Contains(w.Key)).ToList();
        return unused.Count == 0 ? null : unused[_rng.Next(unused.Count)];
    }

    private string WeightedPick(IEnumerable<string> keys, Func<string, int> weight)
    {
        var items = keys.Select(k => (key: k, weight: weight(k))).ToList();
        var total = items.Sum(i => i.weight);
        var roll = _rng.Next(0, total);
        var sum = 0;

        foreach (var item in items)
        {
            sum += item.weight;
            if (roll < sum)
                return item.key;
        }

        return items[^1].key;
    }
}
