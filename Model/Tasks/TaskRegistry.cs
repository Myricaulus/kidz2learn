using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class TaskRegistry
{
    private static readonly Dictionary<Type, IReadOnlyList<BaseTaskDefinition>> _tasks =
    new()
    {
        { typeof(ArithTaskDefinition), AllArith },
        { typeof(SilbenTaskDefinition), AllSilben }
    };

    public static IReadOnlyList<T> GetTasks<T>() where T : BaseTaskDefinition
        => (IReadOnlyList<T>)_tasks[typeof(T)];
    

    private static IReadOnlyList<BaseTaskDefinition>? _all;
    public static IReadOnlyList<BaseTaskDefinition> All => _all ??= [.. _tasks.Values.SelectMany(x => x)];
    public static IReadOnlyList<ArithTaskDefinition> AllArith => ArithTaskRegistry.All;
    public static IReadOnlyList<SilbenTaskDefinition> AllSilben => SilbenTaskRegistry.All;

    static TaskRegistry()
    {
        var baseType = typeof(BaseTaskDefinition);

        var allTaskTypes = baseType.Assembly
            .GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsClass: true } &&
                baseType.IsAssignableFrom(t))
            .ToList();

        var registeredTypes = _tasks.Keys.ToHashSet();

        var missing = allTaskTypes
            .Where(t => !registeredTypes.Contains(t))
            .ToList();

        if (missing.Count > 0)
        {
            var message =
                "TaskRegistry is missing registrations for:\n" +
                string.Join("\n", missing.Select(t => $" - {t.FullName}"));

            throw new InvalidOperationException(message);
        }
    }
}
