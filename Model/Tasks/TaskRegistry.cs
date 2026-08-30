using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class TaskRegistry
{
    private static readonly Dictionary<Type, IReadOnlyList<BaseTaskDefinition>> Tasks =
        new()
        {
            { typeof(ArithTaskDefinition), AllArith },
            { typeof(SilbenTaskDefinition), AllSilben },
            { typeof(SilbenHammerTaskDefinition), AllSilbenHammer }
        };


    private static IReadOnlyList<BaseTaskDefinition>? _all;

    static TaskRegistry()
    {
        var baseType = typeof(BaseTaskDefinition);

        var allTaskTypes = baseType.Assembly
            .GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsClass: true } &&
                baseType.IsAssignableFrom(t))
            .ToList();

        var registeredTypes = Tasks.Keys.ToHashSet();

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

    public static IReadOnlyList<BaseTaskDefinition> All => _all ??= [.. Tasks.Values.SelectMany(x => x)];
    public static IReadOnlyList<ArithTaskDefinition> AllArith => ArithTaskRegistry.All;
    public static IReadOnlyList<ArithTaskDefinition> TurboArith => ArithTaskRegistry.Turbo;
    public static IReadOnlyList<SilbenTaskDefinition> AllSilben => SilbenTaskRegistry.All;
    public static IReadOnlyList<SilbenHammerTaskDefinition> AllSilbenHammer => SilbenHammerTaskRegistry.All;

    public static IReadOnlyList<T> GetTasks<T>() where T : BaseTaskDefinition
    {
        return (IReadOnlyList<T>)Tasks[typeof(T)];
    }
}