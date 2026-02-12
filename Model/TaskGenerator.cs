using System.Threading.Tasks;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Shared.Extensions;

namespace Kidz2Learn.Model;

public enum Difficulty
{
    Normal,
    Hard,
    Extreme
}

public sealed class LearningTask<T> where T: BaseTaskDefinition
{
    public T Task { get; }
    public Difficulty Difficulty { get; }

    private readonly SkillMasteryStore _store;

    /// <summary>
    /// Jaja, die Aufgabe muss noch angezeigt werden, und Menschen mit langsamenen Rechner werden hier systematisch benachteilgt, mimimi. Heul leise...
    /// Menschen die schlechte Rechner haben sind Arm. Und arme Menschen müssen mehr üben, damit sie aus ihrer Armut entfliehen können!
    /// Ausserdem sollten sich die Unterschiede im Millisekunden bereich aufhalten...
    /// </summary>
    private readonly DateTime _timeStarted = DateTime.Now;

    internal LearningTask(
        T task,
        Difficulty difficulty,
        SkillMasteryStore store)
    {
        Task = task;
        Difficulty = difficulty;
        _store = store;
    }

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

public sealed class AdaptiveTaskGenerator(SkillMasteryStore store, Random rng)
{
    public async Task<LearningTask<T>> ChooseTaskAsync<T>(string? category=null) where T: BaseTaskDefinition, IBaseTaskDefinition
    {
        var domain = T.Domain; 
        var skillstates = await store.GetSkillViewEnumerableAsync();
        // 1. Schwächste Skills priorisieren
        var weakestSkills = skillstates
            .Where(sv=>sv.Definition.Domain==domain && (category == null || sv.Definition.Category==category)  )
            .OrderBy(sv => sv.State.Mastery)
            .ThenBy(kv => kv.Definition.Difficulty)
            .Take(3)
            .Select(kv => kv.State.Id)
            .ToHashSet();

        // 2. Aufgaben suchen, die diese Skills trainieren
        var candidates = TaskRegistry.GetTasks<T>();

        /*candidates = candidates
            .Where(d => d.Skills.Any(s => weakestSkills.Contains(s)))
            .ToList();*/
        
        // Fallback, falls alles voll mastered
       // if (candidates.Count == 0)
        //    candidates = [.. TaskRegistry.All];

        // 3. Bevorzugung normaler Tasks
        var weighted = candidates
            .Select(d => (def: d, weight: d.DifficultyLevel))
            .ToList();

        var easiestDifficulty = candidates.Min(c=>c.DifficultyLevel);

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

    private T InvertedWeightedPick<T>(List<(T def, int weight)> items)
    {
        var max = items.Max(i=>i.weight);
        var total = items.Sum(i => max+1 - i.weight);
        var roll = rng.Next(0, total);
        var sum = 0;

        foreach (var item in items)
        {
            sum += max+1 - item.weight;
            if (roll < sum)
                return item.def;
        }

        return items[0].def;
    }
}

