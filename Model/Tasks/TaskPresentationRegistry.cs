namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Maps a <see cref="BaseTaskDefinition.View" /> key (e.g. <c>"silben-multiple-choice"</c>) to
///     the Blazor component type that renders it. New task presentations register themselves here -
///     <c>TaskHost</c> itself never needs to change. Same style as <see cref="TaskDebugOverrideRegistry" />
///     resp. its Model-namespace sibling in <c>Model/TaskDebugOverrideRegistry.cs</c>.
/// </summary>
public static class TaskPresentationRegistry
{
    private static readonly Dictionary<string, Type> Views = new(StringComparer.Ordinal);

    public static void Register(string view, Type componentType)
    {
        if (!typeof(ITaskView).IsAssignableFrom(componentType))
            throw new ArgumentException(
                $"{componentType.Name} must implement {nameof(ITaskView)} to be registered as a task view.",
                nameof(componentType));

        Views[view] = componentType;
    }

    public static Type Resolve(string view)
    {
        return Views.TryGetValue(view, out var type)
            ? type
            : throw new InvalidOperationException(
                $"No view registered for '{view}' (known: {string.Join(", ", Views.Keys)}).");
    }
}
