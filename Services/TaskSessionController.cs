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
public sealed class TaskSessionController(ScoreService score, AffirmationService affirmation, HudStateService hud)
{
    public async Task RecordSuccess(IChosenTask task, Kompetenzniveau kompetenz, int basePoints, int bonusPoints)
    {
        score.AddPoints(basePoints, bonusPoints);
        hud.IncrementCombo();
        await affirmation.PlayErfolgAsync();
        await task.Success(kompetenz);
    }

    public async Task RecordFailure(IChosenTask task, Kompetenzniveau kompetenz, int basePoints, int bonusPoints)
    {
        score.AddPoints(basePoints, bonusPoints);
        hud.SetCombo(0);
        await affirmation.PlayMisserfolgAsync();
        await task.Fail(kompetenz);
    }
}
