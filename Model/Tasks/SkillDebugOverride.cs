namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Forces the chooser onto the definition that trains <c>skill</c> (at <c>level</c>, if given),
///     each task freshly made by its own generator - for any task domain, e.g.
///     <c>/debug/Lesedetektiv?task=skill&amp;skill=read_mirror_letters&amp;level=2</c>. Picks that don't
///     offer such a definition (the page's other skills, its fillers) stay normal.
/// </summary>
public sealed class SkillDebugOverride(string skillId, int? level) : ITaskDebugOverride
{
    public T? TryForce<T>(IReadOnlyCollection<T> candidates) where T : BaseTaskDefinition =>
        candidates.FirstOrDefault(c => c.Skills.Contains(skillId) && (level is null || c.DifficultyLevel == level));
}

/// <summary>Builds a <see cref="SkillDebugOverride" /> from <c>skill</c>/<c>level</c> query params.</summary>
public sealed class SkillDebugOverrideFactory : ITaskDebugOverrideFactory
{
    public string Kind => "skill";

    public ITaskDebugOverride? Build(IReadOnlyDictionary<string, string> query)
    {
        if (!query.TryGetValue("skill", out var skill) || string.IsNullOrWhiteSpace(skill))
            return null;
        return new SkillDebugOverride(skill,
            query.TryGetValue("level", out var level) && int.TryParse(level, out var n) ? n : null);
    }
}
