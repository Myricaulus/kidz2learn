namespace Kidz2Learn.Model;

/// <summary>
///     How the current session is played, chosen on the splash screen. In <see cref="Koop" /> mode
///     two kids of about the same age solve the tasks together, so the answers aren't the child's
///     alone: they train the Koop mastery profile only (<c>SkillMasteryStore</c>), Silbenhammer
///     ratings and the per-task histories stay untouched, no <c>Beobachtung</c> is stored for the
///     reading profile, tasks that mainly serve it (<c>BaseTaskDefinition.FuerLeseprofil</c>) come up
///     rarely, and the telemetry session is flagged <c>Koop</c>.
/// </summary>
public static class PlayMode
{
    public static bool Koop { get; set; }
}
