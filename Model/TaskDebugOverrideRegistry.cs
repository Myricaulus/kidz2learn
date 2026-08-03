using Kidz2Learn.Model.Tasks;

namespace Kidz2Learn.Model;

/// <summary>
///     Maps the debug wrapper's <c>task</c> query value (e.g. <c>"silben"</c>) to the factory that
///     knows how to build an <see cref="ITaskDebugOverride" /> for that domain from the rest of
///     the query string. Adding a new task domain to the debug tooling means registering its
///     factory here - the generic debug page itself never changes.
/// </summary>
public static class TaskDebugOverrideRegistry
{
    private static readonly Dictionary<string, ITaskDebugOverrideFactory> Factories =
        new(StringComparer.OrdinalIgnoreCase);

    static TaskDebugOverrideRegistry()
    {
        Register(new SilbenDebugOverrideFactory());
    }

    public static void Register(ITaskDebugOverrideFactory factory)
    {
        Factories[factory.Kind] = factory;
    }

    public static ITaskDebugOverride? Build(string kind, IReadOnlyDictionary<string, string> query)
    {
        return Factories.TryGetValue(kind, out var factory) ? factory.Build(query) : null;
    }

    public static IReadOnlyCollection<string> Kinds => Factories.Keys;
}
