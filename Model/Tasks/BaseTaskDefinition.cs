using System.ComponentModel.DataAnnotations;
using MudBlazor;

namespace Kidz2Learn.Model.Tasks;
public interface IBaseTaskDefinition
{
    string[] Skills { get; }
    int DifficultyLevel { get; }

    static abstract string Domain { get; }
}
public abstract class BaseTaskDefinition
{
    public required string[] Skills { get; init; }
    public int DifficultyLevel { get; init; } // 1 = leicht, 2 = mittel, 3 = schwer

    /// <summary>
    ///     Key naming which UI this task needs to be rendered (e.g. "silben-multiple-choice",
    ///     "arith-numpad"). Not yet consumed anywhere - part of the incremental
    ///     task-presentation redesign, see TASK_PRESENTATION_REDESIGN.md.
    /// </summary>
    public required string View { get; init; }
}
