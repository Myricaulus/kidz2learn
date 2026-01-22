namespace Kidz2Learn.Model;

public enum Difficulty
{
    Normal,
    Hard,
    Extreme
}

public sealed class LearningTask
{
    public ArithTaskDefinition Task { get; }
    public Difficulty Difficulty { get; }

    private readonly SkillMasteryStore _store;

    internal LearningTask(
        ArithTaskDefinition task,
        Difficulty difficulty,
        SkillMasteryStore store)
    {
        Task = task;
        Difficulty = difficulty;
        _store = store;
    }

    public void Success()
    {
        foreach (var skill in Task.Skills)
            _store.Adjust(skill, Difficulty, true);
    }

    public void Fail()
    {
        foreach (var skill in Task.Skills)
            _store.Adjust(skill, Difficulty, false);
    }
}

public sealed class AdaptiveTaskGenerator
{
    private readonly SkillMasteryStore _store;
    private readonly Random _rng;

    public AdaptiveTaskGenerator(SkillMasteryStore store, Random rng)
    {
        _store = store;
        _rng = rng;
    }

    public LearningTask ChooseTask(string domain, string? category=null)
    {
        
        // 1. Schwächste Skills priorisieren
        var weakestSkills = _store.Snapshot().Join(SkillRegistry.All,m=>m.Key,r=>r.Key,
            (skillMastery,skillRegistry)=>
            {
                return(skillMastery,skillRegistry);
            }
            )
            .Where(kw=>kw.skillRegistry.Value.Domain==domain && (category == null || kw.skillRegistry.Value.Category==category)  )
            .OrderBy(kv => kv.skillMastery.Value.Mastery)
            .ThenBy(kv => kv.skillRegistry.Value.Difficulty)
            .Take(3)
            .Select(kv => kv.skillMastery.Key)
            .ToHashSet();

        // 2. Aufgaben suchen, die diese Skills trainieren
        var candidates = TaskRegistry.All
            .Where(d => d.Skills.Any(s => weakestSkills.Contains(s)))
            .ToList();

        // Fallback, falls alles voll mastered
        if (candidates.Count == 0)
            candidates = [.. TaskRegistry.All];

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

        return new LearningTask(
            chosen,
            difficulty,
            _store);
    }

    private ArithTaskDefinition InvertedWeightedPick(List<(ArithTaskDefinition def, int weight)> items)
    {
        int max = items.Max(i=>i.weight);
        int total = items.Sum(i => max+1 - i.weight);
        int roll = _rng.Next(0, total);
        int sum = 0;

        foreach (var item in items)
        {
            sum += max+1 - item.weight;
            if (roll < sum)
                return item.def;
        }

        return items[0].def;
    }
}

