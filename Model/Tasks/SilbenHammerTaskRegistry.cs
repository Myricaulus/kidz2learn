using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class SilbenHammerTaskRegistry
{
    private static readonly List<SilbenHammerTaskDefinition> Defs =
    [
        new()
        {
            Skills = [Skill.SilbenHammer],
            DifficultyLevel = 2,
            View = "silben-hammer",
            Generator = _ => new object()
        }
    ];

    public static IReadOnlyList<SilbenHammerTaskDefinition> All => Defs;
}
