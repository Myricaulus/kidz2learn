using Kidz2Learn.Model.Tasks;

namespace Kidz2Learn.Model;

public enum Difficulty
{
    Normal,
    Hard,
    Extreme
}

public sealed class LearningTask<T> where T : BaseTaskDefinition
{
    private readonly ISkillMasteryStore _store;

    /// <summary>
    ///     Jaja, die Aufgabe muss noch angezeigt werden, und Menschen mit langsamenen Rechner werden hier systematisch
    ///     benachteilgt, mimimi. Heul leise...
    ///     Menschen die schlechte Rechner haben sind Arm. Und arme Menschen müssen mehr üben, damit sie aus ihrer Armut
    ///     entfliehen können!
    ///     Ausserdem sollten sich die Unterschiede im Millisekunden bereich aufhalten...
    /// </summary>
    private readonly DateTime _timeStarted = DateTime.Now;

    internal LearningTask(
        T task,
        Difficulty difficulty,
        ISkillMasteryStore store)
    {
        Task = task;
        Difficulty = difficulty;
        _store = store;
    }

    public T Task { get; }
    public Difficulty Difficulty { get; }

    public async Task Success(Kompetenzniveau kompetenz)
    {
        var time = DateTime.Now - _timeStarted;
        foreach (var skill in Task.Skills)
            await _store.Adjust(skill, Difficulty, (int)time.TotalMilliseconds, kompetenz, true);
    }

    public async Task Fail(Kompetenzniveau kompetenz)
    {
        var time = DateTime.Now - _timeStarted;
        foreach (var skill in Task.Skills)
            await _store.Adjust(skill, Difficulty, (int)time.TotalMilliseconds, kompetenz, false);
    }
}

public sealed class AdaptiveTaskGenerator(ISkillMasteryStore store, Random rng)
{
    /// <summary>
    ///     How many DifficultyLevel-equivalent "steps" a fully-mastered skill (Mastery == 1) is
    ///     penalized by, relative to a completely untrained one (Mastery == 0). Kept in the same
    ///     rough magnitude as <see cref="BaseTaskDefinition.DifficultyLevel" /> (1-4 in practice) so
    ///     neither difficulty nor mastery dominates the pick.
    /// </summary>
    private const int MasteryWeightRange = 4;

    /// <summary>
    ///     Picks a task for <typeparamref name="T" />, weighted towards easier difficulties and
    ///     towards whichever of the task's own skills the learner has mastered least so far.
    /// </summary>
    /// <param name="skills">
    ///     Restricts candidates to tasks that train at least one of these skill ids. When
    ///     <c>null</c>, every registered task for <typeparamref name="T" /> (i.e. the whole
    ///     <see cref="IBaseTaskDefinition.Domain" />) is eligible.
    /// </param>
    public async Task<LearningTask<T>> ChooseTaskAsync<T>(IReadOnlyCollection<string>? skills = null)
        where T : BaseTaskDefinition, IBaseTaskDefinition
    {
        var candidates = TaskRegistry.GetTasks<T>();
        if (skills is not null)
            candidates = candidates.Where(c => c.Skills.Any(skills.Contains)).ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"No {typeof(T).Name} tasks match skills [{string.Join(", ", skills ?? [])}].");

        var masteryBySkill = (await store.GetSkillViewEnumerableAsync())
            .ToDictionary(sv => sv.State.Id, sv => sv.State.Mastery);

        var easiestDifficulty = candidates.Min(c => c.DifficultyLevel);

        var weighted = candidates
            .Select(d => (def: d, weight: d.DifficultyLevel + MasteryWeight(WeakestSkillMastery(d, masteryBySkill))))
            .ToList();

        var chosen = InvertedWeightedPick(weighted);
        var difficulty = chosen.DifficultyLevel switch
        {
            var x when x == easiestDifficulty => Difficulty.Normal,
            var x when x == easiestDifficulty + 1 => Difficulty.Hard,
            _ => Difficulty.Extreme
        };

        return new LearningTask<T>(
            chosen,
            difficulty,
            store);
    }

    /// <summary>
    ///     A task is exactly as "needed" as the least-mastered skill it trains, so we key the
    ///     weighting off the minimum, not the average.
    /// </summary>
    private static float WeakestSkillMastery(BaseTaskDefinition def, IReadOnlyDictionary<string, float> masteryBySkill)
    {
        return def.Skills
            .Select(s => masteryBySkill.GetValueOrDefault(s, 0f))
            .DefaultIfEmpty(0f)
            .Min();
    }

    private static int MasteryWeight(float mastery)
    {
        return (int)Math.Round(Math.Clamp(mastery, 0f, 1f) * MasteryWeightRange);
    }

    private T InvertedWeightedPick<T>(List<(T def, int weight)> items)
    {
        var max = items.Max(i => i.weight);
        var total = items.Sum(i => max + 1 - i.weight);
        var roll = rng.Next(0, total);
        var sum = 0;

        foreach (var item in items)
        {
            sum += max + 1 - item.weight;
            if (roll < sum)
                return item.def;
        }

        return items[0].def;
    }
}