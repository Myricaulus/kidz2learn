using Kidz2Learn.Model.Lesen;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class RaetselTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, RaetselAufgabe> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<RaetselTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}
