using Kidz2Learn.Model.Lesen;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class SuchRasterTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, SuchRaster> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<SuchRasterTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}
