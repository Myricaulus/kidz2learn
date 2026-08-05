using System.ComponentModel.DataAnnotations;
using Kidz2Learn.Model;
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

    /// <summary>
    ///     Generates this task's payload and wraps it in an <see cref="IChosenTask" />. The only
    ///     place that still knows the concrete payload shape at compile time, so the type-erased
    ///     <see cref="AdaptiveTaskGenerator.ChooseAnyAsync" /> can pick across subtypes without a
    ///     type switch. Uses the same <see cref="Random" /> instance the picker itself draws from,
    ///     matching how pages call <c>Generator(rng)</c> with their own rng today.
    /// </summary>
    internal abstract IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store);
}
