using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class ArithTaskRegistry
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
