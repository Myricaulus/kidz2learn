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
        // Filter element-by-element instead of casting the whole collection: candidates can be a
        // mixed BaseTaskDefinition pool (AdaptiveTaskGenerator.ChooseAnyAsync), which never
        // satisfies "is IReadOnlyCollection<SilbenTaskDefinition>" regardless of its actual
        // contents - see TASK_PRESENTATION_REDESIGN.md, Baustein 4.
        var silbenCandidates = candidates.OfType<SilbenTaskDefinition>().ToList();
        if (silbenCandidates.Count == 0)
            return null;

        var entry = WordMeta.Data.FirstOrDefault(w =>
            string.Equals(w.Key, word, StringComparison.OrdinalIgnoreCase));
        var target = entry.Key ?? word;
        var filename = entry.Value?.filename ?? target;

        var baseDef = (skillId is null ? null : silbenCandidates.FirstOrDefault(c => c.Skills.Contains(skillId)))
                      ?? silbenCandidates.First();

        var resolvedOptions = options is { Count: > 0 }
            ? options.Append(target).Distinct().ToArray()
            : RealisticDistractorsFor(baseDef, target);

        var forced = new SilbenTaskDefinition
        {
            Skills = baseDef.Skills,
            DifficultyLevel = baseDef.DifficultyLevel,
            View = baseDef.View,
            Generator = _ => (filename, resolvedOptions)
        };

        return forced as T;
    }

    /// <summary>
    ///     Without an explicit <c>options=</c> override, invokes the real generator once to get a
    ///     genuinely random word's options, then swaps its own target for <paramref name="target" />.
    ///     Keeps the exact distractor *count* each skill's real generator uses (3 for read_precise/
    ///     GraphemPhonem, 6 for read_syllables, ...) without hardcoding a number here or duplicating
    ///     each skill's distractor algorithm - see TECH_DEBT.md #13, where a hardcoded "4" produced 5
    ///     options for read_precise instead of the real 3.
    /// </summary>
    private static string[] RealisticDistractorsFor(SilbenTaskDefinition baseDef, string target)
    {
        var (theirTarget, generatedOptions) = baseDef.Generator(Random.Shared);
        // Options are built against the hyphen-stripped correct answer, same normalization
        // SilbenMultipleChoiceView.CheckAnswer applies (_correctSyllable.Replace("-", "")) -
        // "their" own target (whatever value the shape's first tuple element is: a filename with
        // syllable dashes for read_syllables/read_precise, a plain WordMeta key for GraphemPhonem)
        // won't string-equal any option otherwise, and nothing gets filtered out.
        var theirTargetAnswer = theirTarget.Replace("-", "");
        return generatedOptions
            .Where(o => !o.Equals(theirTargetAnswer, StringComparison.OrdinalIgnoreCase))
            .Append(target)
            .Distinct()
            .ToArray();
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
