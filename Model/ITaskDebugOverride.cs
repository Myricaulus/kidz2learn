using Kidz2Learn.Model.Tasks;

namespace Kidz2Learn.Model;

/// <summary>
///     Lets a debug page force <see cref="AdaptiveTaskGenerator.ChooseTaskAsync{T}" /> to hand back
///     a specific task instead of picking one adaptively, e.g. to try out a fixed word/skill
///     combination. Implementations are domain-specific (e.g. one for Silben tasks); the generic
///     chooser stays unaware of which domain is actually being overridden.
/// </summary>
public interface ITaskDebugOverride
{
    /// <summary>
    ///     Returns a forced task if this override applies to <typeparamref name="T" /> and the
    ///     given <paramref name="candidates" />, or <c>null</c> to fall back to the normal pick.
    /// </summary>
    T? TryForce<T>(IReadOnlyCollection<T> candidates) where T : BaseTaskDefinition;
}
