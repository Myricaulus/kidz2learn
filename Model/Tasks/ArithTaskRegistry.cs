using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class ArithTaskRegistry
{
    private static readonly List<ArithTaskDefinition> Defs =
    [
        new() // 0
        {
            Skills = [Skill.Math.Add15],
            DifficultyLevel = 1,
            Generator = r => (r.Next(1, 5), r.Next(1, 5), null, ArithOperator.Addition)
        },


        new() // 1
        {
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

                return (a, b, null, ArithOperator.Addition);
            }
        },


        new() // 2
        {
            Skills = [Skill.Math.Add10WithCarry],
            DifficultyLevel = 3,
            Generator = r => (r.Next(5, 10), r.Next(5, 10), null, ArithOperator.Addition)
        },


        new() //3
        {
            Skills = [Skill.Math.Add20],
            DifficultyLevel = 4,
            Generator = r => (r.Next(1, 20), r.Next(1, 20), null, ArithOperator.Addition)
        },

        // SUBTRAKTION

        new() //4
        {
            Skills = [Skill.Math.Sub10],
            DifficultyLevel = 3,
            Generator = r =>
            {
                var a = r.Next(1, 10);
                var b = r.Next(1, a + 1);
                return (a, b, null, ArithOperator.Subtraction);
            }
        },


        new() //6
        {
            Skills = [Skill.Math.Sub20],
            DifficultyLevel = 4,
            Generator = r =>
            {
                var a = r.Next(1, 20);
                var b = r.Next(1, 20);
                if (b > a) (a, b) = (b, a);
                return (a, b, null, ArithOperator.Subtraction);
            }
        }
    ];

    private static readonly List<ArithTaskDefinition> TurboDefs =
    [
        new()
        {
            Skills = [Skill.Math.Turbo10],
            DifficultyLevel = 3,
            Generator = r =>
            {
                if (r.Next(2) == 0)
                {
                    int? x, y;
                    do
                    {
                        x = r.Next(1, 10);
                        y = r.Next(1, 10);
                    } while (x + y >= 10);

                    var z = x + y;
                    var i = r.Next(3);
                    if (i == 0) x = null;
                    else if (i == 1) y = null;
                    else z = null;
                    return (x, y, z, ArithOperator.Addition);
                }
                else
                {
                    int? x = r.Next(1, 10);
                    int? y = r.Next(1, x.Value + 1);
                    var z = x - y;
                    var i = r.Next(3);
                    if (i == 0) x = null;
                    else if (i == 1) y = null;
                    else z = null;
                    return (x, y, z, ArithOperator.Subtraction);
                }
            }
        }
    ];

    public static IReadOnlyList<ArithTaskDefinition> All => Defs.Union(TurboDefs).ToList();
    public static IReadOnlyList<ArithTaskDefinition> Simple => Defs;
    public static IReadOnlyList<ArithTaskDefinition> Turbo => TurboDefs;

    /// <summary>
    ///     Skill ids covered by <see cref="Simple" />, i.e. everything except the Turbo skills.
    ///     Used to scope <see cref="AdaptiveTaskGenerator.ChooseTaskAsync{T}" /> to "normal" tasks
    ///     without hardcoding a single skill.
    /// </summary>
    public static readonly IReadOnlyCollection<string> SimpleSkills =
        Defs.SelectMany(d => d.Skills).ToHashSet();
}