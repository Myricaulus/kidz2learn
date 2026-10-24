namespace Kidz2Learn.Model;

/// <summary>
///     How the current session is played, chosen on the splash screen. In <see cref="Koop" /> mode a
///     parent plays along, so the answers aren't the child's alone: skill mastery, Silbenhammer
///     ratings and the per-task histories stay untouched, and every <c>Beobachtung</c> and telemetry
///     session is flagged <c>Koop</c> so the reading profile and offline analysis can leave it out.
/// </summary>
public static class PlayMode
{
    public static bool Koop { get; set; }
}
