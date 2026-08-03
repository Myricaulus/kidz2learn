using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Forces Silben task selection onto one specific word (and optionally a specific skill / a
///     fixed set of distractor options), so a debug page can try out edge cases like unusually
///     long words without touching the real challenge page's logic.
/// </summary>
public sealed class SilbenDebugOverride(string word, string? skillId, IReadOnlyList<string>? options)
    : ITaskDebugOverride
{
    public T? TryForce<T>(IReadOnlyCollection<T> candidates) where T : BaseTaskDefinition
    {
        if (candidates is not IReadOnlyCollection<SilbenTaskDefinition> silbenCandidates)
            return null;

        var entry = WordMeta.Data.FirstOrDefault(w =>
            string.Equals(w.Key, word, StringComparison.OrdinalIgnoreCase));
        var target = entry.Key ?? word;
        var filename = entry.Value?.filename ?? target;

        var baseDef = (skillId is null ? null : silbenCandidates.FirstOrDefault(c => c.Skills.Contains(skillId)))
                      ?? silbenCandidates.First();

        var resolvedOptions = options is { Count: > 0 }
            ? options.Append(target).Distinct().ToArray()
            : ErstleserDistraktorGenerator.Generate(target, 4, Random.Shared).Append(target).ToArray();

        var forced = new SilbenTaskDefinition
        {
            Skills = baseDef.Skills,
            DifficultyLevel = baseDef.DifficultyLevel,
            Generator = _ => (filename, resolvedOptions)
        };

        return forced as T;
    }
}

/// <summary>Builds a <see cref="SilbenDebugOverride" /> from <c>word</c>/<c>skill</c>/<c>options</c> query params.</summary>
public sealed class SilbenDebugOverrideFactory : ITaskDebugOverrideFactory
{
    public string Kind => "silben";

    public ITaskDebugOverride? Build(IReadOnlyDictionary<string, string> query)
    {
        if (!query.TryGetValue("word", out var word) || string.IsNullOrWhiteSpace(word))
            return null;

        query.TryGetValue("skill", out var skill);

        var options = query.TryGetValue("options", out var rawOptions)
            ? rawOptions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        return new SilbenDebugOverride(word, skill, options);
    }
}
