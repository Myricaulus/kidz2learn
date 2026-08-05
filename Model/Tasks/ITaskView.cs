using Microsoft.AspNetCore.Components;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Parameter contract every view component registered in <see cref="TaskPresentationRegistry" />
///     must implement, so <c>TaskHost</c> can bind them via <c>DynamicComponent</c> without knowing
///     the concrete component type. Deliberately thin: the view owns everything about how to render
///     <see cref="IChosenTask.Payload" /> and how/when to record the answer (it injects
///     <c>TaskSessionController</c> itself, exactly like the pages did before) - <c>TaskHost</c>'s
///     only job is picking a task, resolving its view, and reacting to <see cref="OnNext" /> by
///     picking the next one. See TASK_PRESENTATION_REDESIGN.md, Baustein 5.
/// </summary>
public interface ITaskView
{
    IChosenTask Task { get; set; }

    /// <summary>Raised by the view once it's done with this task and ready for the next one.</summary>
    EventCallback OnNext { get; set; }
}
