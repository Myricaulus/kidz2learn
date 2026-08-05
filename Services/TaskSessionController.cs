using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;

namespace Kidz2Learn.Services;

/// <summary>
///     Common success/failure bookkeeping that SilbenChallenge.CheckAnswer, GraphemChallenge.CheckAnswer
///     and ArithmeticChallenge.Evaluate used to duplicate: score, affirmation sound, combo, and
///     mastery adjustment via <see cref="IChosenTask" />. Loading/storing the domain-specific log
///     entity (SilbenLog vs. ArithemticLog) and the Logger.Erfolgreich/GesamtAnzahl bookkeeping
///     deliberately stay with each view - their shapes/semantics differ too much to unify without a
///     bigger schema change, see TASK_PRESENTATION_REDESIGN.md (Baustein 6) and TECH_DEBT.md #7/#9.
/// </summary>
/// <remarks>
///     Combo (<see cref="HudStateService" />) used to be wired up ad-hoc, only by
///     ArithmeticChallenge/TurboArithChallenge - Silben/Graphem never touched it, so combo silently
///     never counted there (found during Deutsch-Mix/Bestenmix manual testing, TASK_PRESENTATION_REDESIGN.md).
///     Moved here since it's exactly as domain-agnostic as Score - every view goes through this.
/// </remarks>
/// <remarks>
///     <see cref="TotalAttempts" />/<see cref="SuccessfulAttempts" /> replace the old
///     <c>LoggerService.GesamtAnzahl</c>/<c>Erfolgreich</c> fields (TECH_DEBT.md #9: Silben treated
///     them as an ever-growing counter, Arithmetik as a 0..1 ratio reloaded from
///     <c>ArithemticLogStats</c> - same singleton fields, two incompatible semantics, hence the
///     ">100%" HUD drift). One attempt == one <see cref="RecordSuccess" />/<see cref="RecordFailure" />
///     call, for every domain alike - reset once per page visit via <see cref="ResetStats" /> from
///     <c>TaskHost.OnInitializedAsync</c>, same lifecycle as <see cref="HudStateService.ResetAll" />.
///     <c>ArithemticLogStats</c> itself keeps being persisted separately in `ArithNumpadView` - that's
///     a different, longer-lived stat, not the source of this bug.
/// </remarks>
public sealed class TaskSessionController(ScoreService score, AffirmationService affirmation, HudStateService hud)
{
    public int TotalAttempts { get; private set; }
    public int SuccessfulAttempts { get; private set; }
    public float SuccessRatio => TotalAttempts == 0 ? 0f : (float)SuccessfulAttempts / TotalAttempts;

    public void ResetStats()
    {
        TotalAttempts = 0;
        SuccessfulAttempts = 0;
    }

    public async Task RecordSuccess(IChosenTask task, Kompetenzniveau kompetenz, int basePoints, int bonusPoints)
    {
        score.AddPoints(basePoints, bonusPoints);
        hud.IncrementCombo();
        TotalAttempts++;
        SuccessfulAttempts++;
        await affirmation.PlayErfolgAsync();
        await task.Success(kompetenz);
    }

    public async Task RecordFailure(IChosenTask task, Kompetenzniveau kompetenz, int basePoints, int bonusPoints)
    {
        score.AddPoints(basePoints, bonusPoints);
        hud.SetCombo(0);
        TotalAttempts++;
        await affirmation.PlayMisserfolgAsync();
        await task.Fail(kompetenz);
    }
}
