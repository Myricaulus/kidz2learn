using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Kidz2Learn.Model.Tasks.TaskDefs;


public sealed class ArithTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition
{
    public static string Domain => TaskDomain.Math;
    
    public ArithOperator Operator { get; init; }

    public required Func<Random, (int a,int b)> Generator { get; init; }

}
