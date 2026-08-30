namespace Kidz2Learn.Model.Tasks.TaskDefs;

/// <summary>
///     Mixer entry point for the Silbenhammer game mode. The payload is a trivial marker -
///     picking the actual word (and the whole multi-word "burst") needs an async round trip to
///     <see cref="ISilbenHammerRatingStore" />/the word catalog, which <see cref="Generator" />
///     (synchronous, no DB access) can't do. <see cref="Components.TaskViews.SilbenHammerView" />
///     does that selection itself via DI; only the reference identity of the resulting
///     <see cref="IChosenTask" /> matters to it, to detect "new burst" the same way every other
///     view detects "new round".
/// </summary>
public sealed class SilbenHammerTaskDefinition : BaseTaskDefinition, IBaseTaskDefinition
{
    public static string Domain => TaskDomain.Reading;
    public required Func<Random, object> Generator { get; init; }

    internal override IChosenTask Choose(Random rng, Difficulty difficulty, ISkillMasteryStore store)
    {
        return new LearningTask<SilbenHammerTaskDefinition>(this, Generator(rng), difficulty, store);
    }
}
