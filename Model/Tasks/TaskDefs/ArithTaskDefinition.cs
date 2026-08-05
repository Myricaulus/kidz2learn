using Kidz2Learn.Model;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class ArithTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition
{
    public required Func<Random, (int? x, int? y, int? z, ArithOperator)> Generator { get; init; }
    public static string Domain => TaskDomain.Math;

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<ArithTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}