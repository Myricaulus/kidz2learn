namespace Kidz2Learn.Model;

/// <summary>
///     Builds an <see cref="ITaskDebugOverride" /> from raw debug query-string parameters for one
///     task domain (e.g. Silben). Registered in <see cref="TaskDebugOverrideRegistry" /> under a
///     short <c>kind</c> key so the generic debug wrapper page never needs to know about
///     individual task domains.
/// </summary>
public interface ITaskDebugOverrideFactory
{
    /// <summary>The <c>task</c> query value that selects this factory, e.g. <c>"silben"</c>.</summary>
    string Kind { get; }

    /// <summary>
    ///     Builds an override from the debug query parameters, or <c>null</c> if they don't carry
    ///     enough information for this domain (e.g. no target word given).
    /// </summary>
    ITaskDebugOverride? Build(IReadOnlyDictionary<string, string> query);
}
