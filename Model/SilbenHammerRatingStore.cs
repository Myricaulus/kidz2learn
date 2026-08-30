using Kidz2Learn.Entities;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Model;

/// <summary>
///     Storage boundary for per-syllable Silbenhammer ratings - separate from
///     <see cref="ISkillMasteryStore" /> on purpose: different scale/semantics (a "needs more
///     practice" score keyed by syllable text, not a 0-1 skill mastery keyed by a small static
///     skill catalog) and a data-driven key set (every distinct syllable in the word catalog, not
///     <see cref="SkillRegistry" />). Extracted so <see cref="SilbenHammerSelector" /> can be
///     unit-tested with a fake store instead of a real IndexedDB-backed one.
/// </summary>
public interface ISilbenHammerRatingStore
{
    Task<IReadOnlyDictionary<string, int>> GetAllCleanStreaksAsync();
    Task RecordCleanAsync(string normalizedSyllable);
    Task RecordStruggledAsync(string normalizedSyllable);
}

public sealed class SilbenHammerRatingStore(IndexedDb aufgabenDb) : ISilbenHammerRatingStore
{
    private readonly IndexedDbStore _store = aufgabenDb["SilbenHammerRatings"]
        ?? throw new InvalidOperationException("SilbenHammerRatings DB failed to init");

    public async Task<IReadOnlyDictionary<string, int>> GetAllCleanStreaksAsync()
    {
        var dict = new Dictionary<string, int>();
        await foreach (var r in _store.GetAllAsync<SilbenHammerSyllableRating>())
            dict[r.Id] = r.CleanStreak;
        return dict;
    }

    public Task RecordCleanAsync(string normalizedSyllable)
    {
        return Adjust(normalizedSyllable, +1);
    }

    public Task RecordStruggledAsync(string normalizedSyllable)
    {
        return Adjust(normalizedSyllable, -1);
    }

    private async Task Adjust(string normalizedSyllable, int delta)
    {
        var r = await _store.GetItemAsync<SilbenHammerSyllableRating>(normalizedSyllable)
            ?? new SilbenHammerSyllableRating { Id = normalizedSyllable };
        r.CleanStreak += delta;
        await _store.StoreItemAsync(r);
    }
}
