using Kidz2Learn.Model.Tasks;

namespace Kidz2Learn.Model;

public enum Difficulty
{
    Normal,
    Hard,
    Extreme
}

public sealed class LearningTask<T> : IChosenTask where T : BaseTaskDefinition
{
    private readonly ISkillMasteryStore _store;

    /// <summary>
    ///     Jaja, die Aufgabe muss noch angezeigt werden, und Menschen mit langsamenen Rechner werden hier systematisch
    ///     benachteilgt, mimimi. Heul leise...
    ///     Menschen die schlechte Rechner haben sind Arm. Und arme Menschen müssen mehr üben, damit sie aus ihrer Armut
    ///     entfliehen können!
    ///     Ausserdem sollten sich die Unterschiede im Millisekunden bereich aufhalten...
    /// </summary>
    /// <remarks>Only times <see cref="Success(Kompetenzniveau)" />/<see cref="Fail(Kompetenzniveau)" />.</remarks>
    private readonly DateTime _timeStarted = DateTime.Now;

    internal LearningTask(
        T task,
        Difficulty difficulty,
        ISkillMasteryStore store)
        : this(task, null, difficulty, store)
    {
    }

    /// <summary>
    ///     Used by <see cref="BaseTaskDefinition.Choose" /> (the type-erased picker path), which
    ///     generates the payload eagerly since the generic <c>TaskHost</c> can't call a
    ///     domain-specific <c>Generator</c> itself. The legacy <see cref="AdaptiveTaskGenerator.ChooseTaskAsync{T}" />
    ///     path above leaves <paramref name="payload" /> unset - its callers still generate the
    ///     payload themselves via <c>Task.Generator(rng)</c>.
    /// </summary>
    internal LearningTask(
        T task,
        object? payload,
        Difficulty difficulty,
        ISkillMasteryStore store,
        int levelsAboveEasiest = 0)
    {
        Task = task;
        Payload = payload;
        Difficulty = difficulty;
        LevelsAboveEasiest = levelsAboveEasiest;
        _store = store;
    }

    public T Task { get; }
    public object? Payload { get; }
    public Difficulty Difficulty { get; }
    public int LevelsAboveEasiest { get; }

    object IChosenTask.Payload => Payload ?? throw new InvalidOperationException(
        $"{nameof(Payload)} was never generated for this task - it was chosen via the legacy " +
        $"{nameof(AdaptiveTaskGenerator.ChooseTaskAsync)} path, which doesn't populate it.");

    string IChosenTask.View => Task.View;
    IReadOnlyList<string> IChosenTask.Skills => Task.Skills;
    int IChosenTask.Level => Task.DifficultyLevel;
    BaseTaskDefinition IChosenTask.Definition => Task;

    /// <summary>Scores an attempt timed from this task's creation - for callers outside <c>TaskSessionController</c> (Turbo).</summary>
    public Task<IReadOnlyDictionary<string, MasteryChange>> Success(Kompetenzniveau kompetenz)
    {
        return Adjust(kompetenz, true, ElapsedMs);
    }

    /// <inheritdoc cref="Success(Kompetenzniveau)" />
    public Task<IReadOnlyDictionary<string, MasteryChange>> Fail(Kompetenzniveau kompetenz)
    {
        return Adjust(kompetenz, false, ElapsedMs);
    }

    public Task<IReadOnlyDictionary<string, MasteryChange>> Success(Kompetenzniveau kompetenz, int timeMs)
    {
        return Adjust(kompetenz, true, timeMs);
    }

    public Task<IReadOnlyDictionary<string, MasteryChange>> Fail(Kompetenzniveau kompetenz, int timeMs)
    {
        return Adjust(kompetenz, false, timeMs);
    }

    private int ElapsedMs => (int)(DateTime.Now - _timeStarted).TotalMilliseconds;

    private async Task<IReadOnlyDictionary<string, MasteryChange>> Adjust(Kompetenzniveau kompetenz, bool success,
        int timeMs)
    {
        var changes = new Dictionary<string, MasteryChange>();
        foreach (var skill in Task.Skills)
            changes[skill] = await _store.Adjust(skill, Difficulty, timeMs, kompetenz, success, Task.WeightOf(skill));
        return changes;
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
    ///     Mastery of a group's skill a definition needs, by how many levels it lies above the
    ///     group's easiest one: the easiest is always open, the next only from 0.25 on, and so on.
    /// </summary>
    private static readonly float[] LevelThresholds = [0f, 0.25f, 0.5f, 0.7f, 0.85f];

    public static bool IsUnlocked(int levelsAboveEasiest, float mastery)
    {
        return levelsAboveEasiest < LevelThresholds.Length
            ? mastery >= LevelThresholds[Math.Max(levelsAboveEasiest, 0)]
            : mastery >= 1f;
    }

    /// <summary>
    ///     Debug hook so a wrapper page can force a specific task instead of the adaptive pick,
    ///     without the real challenge pages having to know about it. Set by a debug page before
    ///     rendering the real page as a child component; must be reset to <c>null</c> when that
    ///     page goes away, or every later real session would keep getting the forced task.
    /// </summary>
    public static ITaskDebugOverride? DebugOverride { get; set; }

    /// <summary>
    ///     Picks a task for <typeparamref name="T" />, weighted towards easier difficulties and
    ///     towards whichever of the task's own skills the learner has mastered least so far.
    /// </summary>
    /// <param name="skills">
    ///     Restricts candidates to tasks that train at least one of these skill ids. When
    ///     <c>null</c>, every registered task for <typeparamref name="T" /> is eligible.
    /// </param>
    public async Task<LearningTask<T>> ChooseTaskAsync<T>(IReadOnlyCollection<string>? skills = null)
        where T : BaseTaskDefinition
    {
        var candidates = TaskRegistry.GetTasks<T>();
        if (skills is not null)
            candidates = candidates.Where(c => c.Skills.Any(skills.Contains)).ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"No {typeof(T).Name} tasks match skills [{string.Join(", ", skills ?? [])}].");

        if (DebugOverride?.TryForce(candidates) is { } forced)
            return new LearningTask<T>(forced, Difficulty.Normal, store);

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
    ///     Type-erased counterpart to <see cref="ChooseTaskAsync{T}" />: picks across every
    ///     registered <see cref="BaseTaskDefinition" /> subtype at once (<see cref="TaskRegistry.All" />)
    ///     instead of one fixed <c>T</c>, so candidates from different domains/payload shapes can be
    ///     mixed in the same pool. Same weighting logic, same signature shape as
    ///     <see cref="ChooseTaskAsync{T}" /> - <c>skills = null</c> means "everything", across every
    ///     domain, not just one. Wired into <c>TaskHost</c> - see TASK_PRESENTATION_REDESIGN.md
    ///     (Baustein 4).
    ///     Picks in two steps: first a group - every definition belongs to its first (primary)
    ///     skill, so the three kinds of word problems are one group and W-Fragen another - then a
    ///     definition within it. A group counts once however many definitions it has, so adding
    ///     variants of one exercise doesn't crowd out the others.
    /// </summary>
    /// <param name="exclude">
    ///     Candidates to leave out for now (long tasks right after a long one, see
    ///     <see cref="LangeAufgaben" />) - ignored where it would leave nothing.
    /// </param>
    public async Task<IChosenTask> ChooseAnyAsync(IReadOnlyCollection<string>? skills = null,
        Func<BaseTaskDefinition, bool>? exclude = null)
    {
        var candidates = TaskRegistry.All;
        if (skills is not null)
            candidates = candidates.Where(c => c.Skills.Any(skills.Contains)).ToList();

        // Skip candidates whose View has no registered presentation (e.g. "arith-turbo" - only the
        // standalone TurboArithChallenge page renders that; the mixer has "turbo-runde" instead)
        // instead of picking one and having TaskPresentationRegistry.Resolve throw later.
        candidates = candidates.Where(c => TaskPresentationRegistry.IsRegistered(c.View)).ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"No tasks match skills [{string.Join(", ", skills ?? [])}].");

        if (DebugOverride?.TryForce(candidates) is { } forced)
            return forced.Choose(rng, Difficulty.Normal, store, 0);

        var masteryBySkill = (await store.GetSkillViewEnumerableAsync())
            .ToDictionary(sv => sv.State.Id, sv => sv.State.Mastery);

        // 🎈/💪/💣 stay relative to the whole pool, before anything is left out for now.
        var easiestDifficulty = candidates.Min(c => c.DifficultyLevel);

        // A harder variant only once its group's skill is far enough along.
        var unlocked = candidates
            .GroupBy(d => d.Skills[0])
            .SelectMany(g => g.Where(d => IsUnlocked(d.DifficultyLevel - g.Min(x => x.DifficultyLevel),
                masteryBySkill.GetValueOrDefault(g.Key, 0f))))
            .ToList();
        if (exclude is not null && unlocked.Any(d => !exclude(d)))
            unlocked = unlocked.Where(d => !exclude(d)).ToList();

        var groups = unlocked
            .GroupBy(d => d.Skills[0])
            .Select(g => (def: g, weight: g.Min(d => d.DifficultyLevel)
                                          + MasteryWeight(masteryBySkill.GetValueOrDefault(g.Key, 0f))))
            .ToList();
        var group = InvertedWeightedPick(groups);

        var weighted = group
            .Select(d => (def: d, weight: d.DifficultyLevel + MasteryWeight(WeakestSkillMastery(d, masteryBySkill))))
            .ToList();

        var chosen = InvertedWeightedPick(weighted);
        // Relative to the whole pool, not the group: a level-3 word problem stays a 💣 on the Lesedetektiv.
        var difficulty = chosen.DifficultyLevel switch
        {
            var x when x == easiestDifficulty => Difficulty.Normal,
            var x when x == easiestDifficulty + 1 => Difficulty.Hard,
            _ => Difficulty.Extreme
        };

        return chosen.Choose(rng, difficulty, store, chosen.DifficultyLevel - easiestDifficulty);
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