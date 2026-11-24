namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Forces the chooser onto a definition that trains <c>skill</c> - at <c>level</c> if given,
///     otherwise a random one of its levels ("Mix") - each task freshly made by its own generator,
///     for any task domain, e.g. <c>/debug/Lesedetektiv?task=skill&amp;skill=read_mirror_letters</c>.
///     <c>mastery</c> (percent) replaces the device's mastery for generating it. Picks that don't
///     offer such a definition (the page's other skills, its fillers) stay normal.
/// </summary>
public sealed class SkillDebugOverride(string skillId, int? level, float? mastery = null) : ITaskDebugOverride
{
    private readonly Random _rng = new();

    public float? Mastery => mastery;

    public T? TryForce<T>(IReadOnlyCollection<T> candidates) where T : BaseTaskDefinition
    {
        var passend = candidates.Where(c => c.Skills.Contains(skillId) && (level is null || c.DifficultyLevel == level))
            .ToList();
        return passend.Count == 0 ? null : passend[_rng.Next(passend.Count)];
    }
}

/// <summary>Builds a <see cref="SkillDebugOverride" /> from <c>skill</c>/<c>level</c>/<c>mastery</c> query params.</summary>
public sealed class SkillDebugOverrideFactory : ITaskDebugOverrideFactory
{
    public string Kind => "skill";

    public ITaskDebugOverride? Build(IReadOnlyDictionary<string, string> query)
    {
        if (!query.TryGetValue("skill", out var skill) || string.IsNullOrWhiteSpace(skill))
            return null;
        return new SkillDebugOverride(skill,
            query.TryGetValue("level", out var level) && int.TryParse(level, out var n) ? n : null,
            query.TryGetValue("mastery", out var m) && int.TryParse(m, out var prozent)
                ? Math.Clamp(prozent, 0, 100) / 100f
                : null);
    }
}
