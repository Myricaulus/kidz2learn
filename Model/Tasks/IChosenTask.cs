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
    Task Success(Kompetenzniveau kompetenz);
    Task Fail(Kompetenzniveau kompetenz);
}
