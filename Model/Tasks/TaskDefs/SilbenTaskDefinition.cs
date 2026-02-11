using System.Runtime.CompilerServices;

namespace Kidz2Learn.Model.Tasks.TaskDefs;


public sealed class SilbenTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition
{
    public static string Domain => TaskDomain.Reading;
    public required Func<Random, (string correct,string[] options)> Generator { get; init; }
}
