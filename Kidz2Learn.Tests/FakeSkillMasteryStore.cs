using Kidz2Learn.Entities;
using Kidz2Learn.Model;

namespace Kidz2Learn.Tests;

/// <summary>
///     In-memory <see cref="ISkillMasteryStore" /> so AdaptiveTaskGenerator can be unit-tested
///     without a real IndexedDB/JSInterop-backed store.
/// </summary>
public class FakeSkillMasteryStore(IReadOnlyDictionary<string, float>? mastery = null) : ISkillMasteryStore
{
    private readonly Dictionary<string, float> _mastery = mastery is null
        ? new Dictionary<string, float>()
        : new Dictionary<string, float>(mastery);

    public List<(string SkillId, bool Success)> AdjustCalls { get; } = [];

    public Task Adjust(string skillId, Difficulty difficulty, int timeMs, Kompetenzniveau specificTaskHistory,
        bool success)
    {
        AdjustCalls.Add((skillId, success));
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SkillView>> GetSkillViewEnumerableAsync()
    {
        var views = SkillRegistry.All.Values
            .Select(def => new SkillView(def, new SkillState { Id = def.Id, Mastery = _mastery.GetValueOrDefault(def.Id) }))
            .ToList();

        return Task.FromResult<IEnumerable<SkillView>>(views);
    }
}
