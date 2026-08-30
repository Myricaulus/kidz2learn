using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Round-based mini-game "events" the chooser can launch as a mixer pick - one entry per
///     distinct event, not "several variants of one topic" the way <see cref="ArithTaskRegistry" />/
///     <see cref="SilbenTaskRegistry" /> hold many payload-generating definitions for the same skill
///     family. See <see cref="EventTaskDefinition" /> for why events need their own definition type.
/// </summary>
public static class EventTaskRegistry
{
    private static readonly List<EventTaskDefinition> Defs =
    [
        new()
        {
            Skills = [Skill.SilbenHammer],
            DifficultyLevel = 2,
            View = "silben-hammer",
            Generator = _ => new EventLaunchOptions { RoundBudget = 4 }
        }
    ];

    public static IReadOnlyList<EventTaskDefinition> All => Defs;
}
