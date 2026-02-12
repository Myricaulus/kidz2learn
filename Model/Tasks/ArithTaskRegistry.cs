using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class ArithTaskRegistry
{
    private static readonly List<ArithTaskDefinition> Defs =
    [
        new()
        {
            Operator = ArithOperator.Addition,
            Skills = [Skill.Math.Add15],
            DifficultyLevel = 1,
            Generator = r => (r.Next(1, 5), r.Next(1, 5))
        },


        new()
        {
            Operator = ArithOperator.Addition,
            Skills = [Skill.Math.Add10NoCarry],
            DifficultyLevel = 2,
            Generator = r =>
            {
                int a, b;
                do
                {
                    a = r.Next(1, 10);
                    b = r.Next(1, 10);
                } while (a + b >= 10);

                return (a, b);
            }
        },


        new()
        {
            Operator = ArithOperator.Addition,
            Skills = [Skill.Math.Add10WithCarry],
            DifficultyLevel = 3,
            Generator = r => (r.Next(5, 10), r.Next(5, 10))
        },


        new()
        {
            Operator = ArithOperator.Addition,
            Skills = [Skill.Math.Add20],
            DifficultyLevel = 4,
            Generator = r => (r.Next(1, 20), r.Next(1, 20))
        },

        // SUBTRAKTION

        new()
        {
            Operator = ArithOperator.Subtraction,
            Skills = [Skill.Math.Sub10],
            DifficultyLevel = 3,
            Generator = r =>
            {
                var a = r.Next(1, 10);
                var b = r.Next(1, a + 1);
                return (a, b);
            }
        },


        new()
        {
            Operator = ArithOperator.Subtraction,
            Skills = [Skill.Math.Sub20],
            DifficultyLevel = 4,
            Generator = r =>
            {
                var a = r.Next(1, 20);
                var b = r.Next(1, 20);
                if (b > a) (a, b) = (b, a);
                return (a, b);
            }
        }
    ];
    public static IReadOnlyList<ArithTaskDefinition> All => Defs;
}
