using Kidz2Learn.Model.Lesen;

namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class QuatschTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, QuatschSatz> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store,
        int levelsAboveEasiest)
    {
        return new LearningTask<QuatschTaskDefinition>(this, Generator(rng), difficulty, store,
            levelsAboveEasiest);
    }
}
