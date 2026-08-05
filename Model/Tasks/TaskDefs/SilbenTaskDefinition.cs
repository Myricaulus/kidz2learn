using System.Runtime.CompilerServices;
using Kidz2Learn.Model;

namespace Kidz2Learn.Model.Tasks.TaskDefs;


public sealed class SilbenTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition
{
    public static string Domain => TaskDomain.Reading;
    public required Func<Random, (string correct,string[] options)> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<SilbenTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}
