using Kidz2Learn.Model.Lesen;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

/// <summary>"ABC-Sortieren": the generator gets the skill's mastery, which adds words and makes them more alike.</summary>
public sealed class AbcSortierTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, float, AbcAufgabe> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store,
        int levelsAboveEasiest, float mastery)
    {
        return new LearningTask<AbcSortierTaskDefinition>(this, Generator(rng, mastery), difficulty, store,
            levelsAboveEasiest);
    }
}
