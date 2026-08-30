using Kidz2Learn.Model;

namespace Kidz2Learn.Tests;

/// <summary>
///     In-memory <see cref="ISilbenHammerRatingStore" /> so <see cref="SilbenHammerSelector" /> can
///     be unit-tested without a real IndexedDB-backed store.
/// </summary>
public class FakeSilbenHammerRatingStore(IReadOnlyDictionary<string, int>? streaks = null) : ISilbenHammerRatingStore
{
    private readonly Dictionary<string, int> _streaks = streaks is null
        ? new Dictionary<string, int>()
        : new Dictionary<string, int>(streaks);

    public List<(string Syllable, bool Clean)> RecordCalls { get; } = [];

    public Task<IReadOnlyDictionary<string, int>> GetAllCleanStreaksAsync()
    {
        return Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(_streaks));
    }

    public Task RecordCleanAsync(string normalizedSyllable)
    {
        Adjust(normalizedSyllable, +1);
        RecordCalls.Add((normalizedSyllable, true));
        return Task.CompletedTask;
    }

    public Task RecordStruggledAsync(string normalizedSyllable)
    {
        Adjust(normalizedSyllable, -1);
        RecordCalls.Add((normalizedSyllable, false));
        return Task.CompletedTask;
    }

    private void Adjust(string syllable, int delta)
    {
        _streaks[syllable] = _streaks.GetValueOrDefault(syllable) + delta;
    }
}
