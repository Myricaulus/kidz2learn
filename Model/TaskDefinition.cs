namespace Kidz2Learn.Model;

public interface IBaseTaskDefinition
{
    string[] Skills { get; }
    int DifficultyLevel { get; }
}

public abstract class BaseTaskDefinition<T> : IBaseTaskDefinition
{
    public required string[] Skills { get; init; }
    public int DifficultyLevel { get; init; } // 1 = leicht, 2 = mittel, 3 = schwer

    public required Func<Random, T> Generator { get; init; }
}


public sealed class ArithTaskDefinition : BaseTaskDefinition<(int a, int b)>
{
    public ArithOperator Operator { get; init; }

}

public static class TaskRegistry
{
    private static readonly List<ArithTaskDefinition> _defs = new()
    {
        // ADDITION
        new() {
            Operator = ArithOperator.Addition,
            Skills = new[]{ Skill.Math.add_1_5 },
            DifficultyLevel = 1,
            Generator = r => (r.Next(1,5), r.Next(1,5))
        },

        new() {
            Operator = ArithOperator.Addition,
            Skills = new[]{ Skill.Math.add_10_no_carry },
            DifficultyLevel = 2,
            Generator = r => {
                int a,b;
                do { a = r.Next(1,10); b = r.Next(1,10); }
                while(a + b >= 10);
                return (a,b);
            }
        },

        new() {
            Operator = ArithOperator.Addition,
            Skills = new[]{ Skill.Math.add_10_with_carry },
            DifficultyLevel = 3,
            Generator = r => (r.Next(5,10), r.Next(5,10))
        },

        new() {
            Operator = ArithOperator.Addition,
            Skills = new[]{ Skill.Math.add_20 },
            DifficultyLevel = 4,
            Generator = r => (r.Next(1,20), r.Next(1,20))
        },

        // SUBTRAKTION
        new() {
            Operator = ArithOperator.Subtraction,
            Skills = new[]{ Skill.Math.sub_10 },
            DifficultyLevel = 3,
            Generator = r => {
                int a = r.Next(1,10);
                int b = r.Next(1,a+1);
                return (a,b);
            }
        },

        new() {
            Operator = ArithOperator.Subtraction,
            Skills = new[]{ Skill.Math.sub_20 },
            DifficultyLevel = 4,
            Generator = r => {
                int a = r.Next(1,20);
                int b = r.Next(1,20);
                if(b > a) (a,b) = (b,a);
                return (a,b);
            }
        }
    };

    public static IReadOnlyList<ArithTaskDefinition> All => _defs;
}

public enum ArithOperator
{
    Addition,
    Subtraction,
    Multiplication,
    Division
}