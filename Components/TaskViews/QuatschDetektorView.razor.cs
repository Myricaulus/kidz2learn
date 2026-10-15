using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Pages.SilbenChallenge;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "quatsch-detektor" view: one sentence, the child decides "Stimmt" or "Quatsch". No retry -
///     with two buttons a second try would always be right. Per-sentence history is kept as a
///     <see cref="SilbenLog" /> in "LeseAufgaben", keyed by the sentence text.
/// </summary>
public partial class QuatschDetektorView : ComponentBase, ITaskView
{
    private const int CorrectDelayMs = 1000;

    // Long enough to read the correction once.
    private const int WrongDelayMs = 2800;

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private IChosenTask? _loadedForTask;
    private QuatschSatz _satz = new(string.Empty, true);
    private bool _isProcessing;
    private bool? _picked;
    private bool _lastCorrect;
    private string? _feedbackText;
    private int _correctCount;
    private int _wrongCount;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _satz = (QuatschSatz)ChosenTask.Payload;
        _picked = null;
        _feedbackText = null;
        _isProcessing = false;
    }

    private string AnswerClass(bool answer)
    {
        if (_picked is null)
            return string.Empty;
        if (answer == _satz.Stimmt)
            return "lese-antwort-richtig";
        return answer == _picked ? "lese-antwort-falsch" : "lese-antwort-blass";
    }

    private string Detail(bool stimmt) => $"{_satz.Text} → {(stimmt ? "Stimmt" : "Quatsch")}";

    private async Task Answer(bool stimmt)
    {
        if (_isProcessing)
            return;
        _isProcessing = true;
        _picked = stimmt;
        _lastCorrect = stimmt == _satz.Stimmt;

        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var id = SilbenLog.GenId(_satz.Text, Skill.ReadSentenceTruth);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = _satz.Text;
        log.Falsch = _lastCorrect ? 0 : 1;

        if (_lastCorrect)
        {
            _correctCount++;
            _feedbackText = _satz.Stimmt ? "Richtig, das stimmt!" : "Richtig, das ist Quatsch!";
            log.Kompetenz.AddRichtig();
            var points = LesePunkte.ForText(_satz.Text);
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, points, points, Detail(stimmt));
        }
        else
        {
            _wrongCount++;
            _feedbackText = _satz.Stimmt ? "Hmm, das stimmt wirklich." : "Hmm, das ist Quatsch.";
            log.Kompetenz.AddFalsch();
            // No deduction: a wrong judgement on a sentence he struggled through shouldn't cost
            // what he already earned.
            await Session.RecordFailure(ChosenTask, log.Kompetenz, 0, 0, Detail(stimmt));
        }

        StateHasChanged();
        await db.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());

        _ = Task.Delay(_lastCorrect ? CorrectDelayMs : WrongDelayMs)
            .ContinueWith(async _ => await InvokeAsync(() => OnNext.InvokeAsync()));
    }
}
