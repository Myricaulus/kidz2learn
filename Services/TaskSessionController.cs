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
public sealed class TaskSessionController(
    ScoreService score,
    AffirmationService affirmation,
    HudStateService hud,
    TelemetryService telemetry)
{
    private DateTime _taskStarted = DateTime.Now;
    private int _taskAttempts;

    public int TotalAttempts { get; private set; }
    public int SuccessfulAttempts { get; private set; }
    public float SuccessRatio => TotalAttempts == 0 ? 0f : (float)SuccessfulAttempts / TotalAttempts;

    // Per-skill breakdown for the current session only (LiveLogger's "kurze Stat-Liste", enriched
    // with SkillState.RecentAccuracy for display) - separate from the persisted SkillState.Attempts/
    // Success (lifetime) and RecentAccuracy (last-20-window, cross-session). A task can train
    // several skills at once, same as LearningTask.Success/Fail's per-skill loop.
    private readonly Dictionary<string, (int Attempts, int Successes)> _skillAttempts = new();
    public IReadOnlyDictionary<string, (int Attempts, int Successes)> SkillAttempts => _skillAttempts;

    public void ResetStats()
    {
        TotalAttempts = 0;
        SuccessfulAttempts = 0;
        _skillAttempts.Clear();
    }

    /// <summary>Marks the moment the current task is shown; attempt durations are measured from here.</summary>
    public void BeginTask()
    {
        _taskStarted = DateTime.Now;
        _taskAttempts = 0;
    }

    /// <param name="basePoints">Points at <see cref="Difficulty.Normal" />; scaled here by <see cref="DifficultyInfo.PointFactor" />.</param>
    /// <param name="detail">Human-readable task and answer for the telemetry log ("Hund → Hand").</param>
    public async Task RecordSuccess(IChosenTask task, Kompetenzniveau kompetenz, int basePoints, int bonusPoints,
        string? detail = null)
    {
        var answered = DateTime.Now;
        var attempt = ++_taskAttempts;
        basePoints = DifficultyInfo.Scale(basePoints, task.Difficulty);
        bonusPoints = DifficultyInfo.Scale(bonusPoints, task.Difficulty);
        score.AddPoints(basePoints, bonusPoints);
        hud.IncrementCombo();
        TotalAttempts++;
        SuccessfulAttempts++;
        RecordPerSkill(task, success: true);
        await affirmation.PlayErfolgAsync();
        var mastery = await task.Success(kompetenz);
        RecordTelemetry(task, answered, attempt, detail, correct: true, basePoints + bonusPoints, mastery);
    }

    /// <param name="detail">Human-readable task and answer for the telemetry log ("Hund → Hand").</param>
    public async Task RecordFailure(IChosenTask task, Kompetenzniveau kompetenz, int basePoints, int bonusPoints,
        string? detail = null)
    {
        var answered = DateTime.Now;
        var attempt = ++_taskAttempts;
        score.AddPoints(basePoints, bonusPoints);
        hud.SetCombo(0);
        TotalAttempts++;
        RecordPerSkill(task, success: false);
        await affirmation.PlayMisserfolgAsync();
        var mastery = await task.Fail(kompetenz);
        RecordTelemetry(task, answered, attempt, detail, correct: false, basePoints + bonusPoints, mastery);
    }

    private void RecordTelemetry(IChosenTask task, DateTime answered, int attempt, string? detail, bool correct,
        int points, IReadOnlyDictionary<string, MasteryChange> mastery)
    {
        telemetry.Record(new TelemetryEntry(answered, task.View, task.Skills, task.Level, task.Difficulty.ToString(),
            attempt, detail, correct, points, (long)(answered - _taskStarted).TotalMilliseconds,
            mastery.ToDictionary(m => m.Key,
                m => new TelemetryMastery(Math.Round(m.Value.Before, 4), Math.Round(m.Value.After, 4)))));
    }

    private void RecordPerSkill(IChosenTask task, bool success)
    {
        foreach (var skill in task.Skills)
        {
            var (attempts, successes) = _skillAttempts.GetValueOrDefault(skill);
            _skillAttempts[skill] = (attempts + 1, successes + (success ? 1 : 0));
        }
    }
}
