using Kidz2Learn.Model;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class ArithTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, (int? x, int? y, int? z, ArithOperator)> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<ArithTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}