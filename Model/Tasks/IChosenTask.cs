using Kidz2Learn.Model;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Type-erased view of a <see cref="LearningTask{T}" />, for callers (like the future generic
///     TaskHost, see TASK_PRESENTATION_REDESIGN.md) that pick across multiple
///     <see cref="BaseTaskDefinition" /> subtypes and don't know the concrete <c>T</c> at compile
///     time.
/// </summary>
public interface IChosenTask
{
    object Payload { get; }
    string View { get; }
    IReadOnlyList<string> Skills { get; }
    Difficulty Difficulty { get; }

    /// <summary>The definition's absolute <see cref="BaseTaskDefinition.DifficultyLevel" />.</summary>
    int Level { get; }

    /// <summary>
    ///     Scores the attempt into every skill's mastery; returns each skill's change, keyed by skill id.
    ///     <paramref name="timeMs" /> is the time since the question was shown.
    /// </summary>
    Task<IReadOnlyDictionary<string, MasteryChange>> Success(Kompetenzniveau kompetenz, int timeMs);

    /// <inheritdoc cref="Success" />
    Task<IReadOnlyDictionary<string, MasteryChange>> Fail(Kompetenzniveau kompetenz, int timeMs);
}
