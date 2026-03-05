namespace Kidz2Learn.Model.Tasks.TaskDefs;

public sealed class ArithTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition
{
    public required Func<Random, (int? x, int? y, int? z, ArithOperator)> Generator { get; init; }
    public static string Domain => TaskDomain.Math;
}