using System.ComponentModel.DataAnnotations;
using Kidz2Learn.Model;
using MudBlazor;

namespace Kidz2Learn.Model.Tasks;

public abstract class BaseTaskDefinition
{
    public required string[] Skills { get; init; }

    /// <summary>
    ///     Share of a full mastery adjustment a skill gets from this task (1 = all of it, the default
    ///     for every skill not listed). A task that trains one skill directly and others along the
    ///     way (a word problem: reading the question, then calculating) weights the side skills lower.
    /// </summary>
    public IReadOnlyDictionary<string, float> SkillWeights { get; init; } = new Dictionary<string, float>();

    public float WeightOf(string skill) => SkillWeights.GetValueOrDefault(skill, 1f);
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
    /// <param name="levelsAboveEasiest">How far <see cref="DifficultyLevel" /> lies above the pool's easiest level.</param>
    internal abstract IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store,
        int levelsAboveEasiest);

    /// <summary>
    ///     Runs for a fixed time (the Turbo rounds), so it always counts as long in
    ///     <see cref="LangeAufgaben" />; every other task's length is measured.
    /// </summary>
    public bool FesteDauer { get; init; }

    /// <summary>
    ///     Can't be answered without hearing its word clip (the listening exercises). While
    ///     <c>AudioStatusService</c> reports no audio, <c>TaskHost</c> doesn't pick it and ends it
    ///     without scoring if it's open; other tasks play their clips only as an extra.
    /// </summary>
    public bool BrauchtTon { get; init; }

    /// <summary>
    ///     Mainly there to observe the child's reading for the reading profile (p/q search grid). In
    ///     Koop mode, where nothing is observed, <c>TaskHost</c> rarely picks it.
    /// </summary>
    public bool FuerLeseprofil { get; init; }
}
