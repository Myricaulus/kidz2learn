using Kidz2Learn.Model.Lesen;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class WFrageTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, WFrageAufgabe> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<WFrageTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}
