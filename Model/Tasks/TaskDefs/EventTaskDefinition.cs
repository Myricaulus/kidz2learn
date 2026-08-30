namespace Kidz2Learn.Model.Tasks.TaskDefs;

/// <summary>
///     Mixer entry point for a round-based mini-game "event" (Silbenhammer today - Turbo Rechnen
///     is a natural future addition once its UI is extracted out of TurboArithChallenge into an
///     ITaskView, the way SilbenHammerView already is). Distinct from a normal
///     <see cref="BaseTaskDefinition" /> subtype (one definition = one Q&amp;A payload, e.g.
///     <see cref="ArithTaskDefinition" />/<see cref="SilbenTaskDefinition" />): an event's own view
///     runs a whole internal session (a "burst") before handing control back to
///     <c>TaskHost</c> via <c>ITaskView.OnNext</c>, so <see cref="Generator" /> doesn't produce the
///     session's actual content (that's the view's job, via DI) - it produces
///     <see cref="EventLaunchOptions" />, sizing parameters the chooser hands the event when
///     launching it.
/// </summary>
public sealed class EventTaskDefinition : BaseTaskDefinition
{
    public required Func<Random, EventLaunchOptions> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<EventTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}

/// <summary>
///     Parameters the chooser hands an event when launching it, read off
///     <see cref="IChosenTask.Payload" /> by the event's own view (see
///     <c>Components/TaskViews/SilbenHammerView.razor.cs</c> for the pattern). The other half of
///     "chooser tells the event what to prepare" is <see cref="IChosenTask.Difficulty" /> itself -
///     that's the chooser's mastery/difficulty-weighting verdict (Normal/Hard/Extreme); the fields
///     here are the event definition's own static configuration (set per <see cref="EventTaskRegistry" />
///     entry, currently constant but modeled as a <see cref="Random" />-taking generator like every
///     other task definition, so a future entry can vary them - e.g. roll a random duration within
///     a range - the same way <c>ArithTaskDefinition.Generator</c> rolls random operands).
/// </summary>
public sealed record EventLaunchOptions
{
    /// <summary>How many rounds/words/attempts make up one session. Meaning is event-specific.</summary>
    public int? RoundBudget { get; init; }

    /// <summary>How long the session should run, for time-boxed events (e.g. a future Turbo entry).</summary>
    public TimeSpan? TargetDuration { get; init; }
}
