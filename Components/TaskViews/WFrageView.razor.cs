using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Pages.SilbenChallenge;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "w-frage" view: a sentence and a series of questions about it (<see cref="WFrageSerie" />),
///     each with a color-coded question word and the sentence's own parts as options. Every
///     question is its own scored attempt (<see cref="TaskSessionController.BeginTask" /> per question).
///     A wrong pick greys that option out, costs points (<see cref="DifficultyInfo.Penalty" />), and
///     makes the question word blink and play while the options are locked - the sentence stays
///     unmarked, so the answer still has to be read. A correct pick plays its word when it's in
///     the known word list.
/// </summary>
public partial class WFrageView : ComponentBase, ITaskView
{
    private const int CorrectDelayMs = 1400;

    /// <summary>How long the question word blinks after a wrong pick; the options stay locked meanwhile.</summary>
    private const int HinweisMs = 1600;

    /// <summary>One fixed color per question word, so the color itself becomes a cue over time.</summary>
    public static readonly IReadOnlyDictionary<FrageRolle, string> Farben = new Dictionary<FrageRolle, string>
    {
        [FrageRolle.Wer] = "#e53935",
        [FrageRolle.Wen] = "#ad1457",
        [FrageRolle.Was] = "#2e7d32",
        [FrageRolle.Wo] = "#1565c0",
        [FrageRolle.Womit] = "#ef6c00",
        [FrageRolle.Wann] = "#6a1b9a",
        [FrageRolle.Wohin] = "#00897b",
        [FrageRolle.Woher] = "#6d4c41",
        [FrageRolle.Wem] = "#b8860b",
        [FrageRolle.MitWem] = "#546e7a",
        [FrageRolle.Wie] = "#827717",
        [FrageRolle.WieViele] = "#283593"
    };

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private IChosenTask? _loadedForTask;
    private WFrageSerie _serie = new(string.Empty, [new WFrageAufgabe(string.Empty, FrageRolle.Wer, "Wer?", [], string.Empty)]);
    private int _index;
    private readonly HashSet<string> _wrongPicks = [];
    private bool _isProcessing;
    private bool _solved;
    private string? _feedbackText;
    private int _correctCount;
    private int _wrongCount;

    // Bumped on every wrong pick; keys the question-word span so the blink animation restarts.
    private int _hinweisCount;

    private WFrageAufgabe Aufgabe => _serie.Fragen[_index];
    private string Farbe => Farben[Aufgabe.Rolle];

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _serie = (WFrageSerie)ChosenTask.Payload;
        ShowFrage(0);
    }

    private void ShowFrage(int index)
    {
        _index = index;
        _wrongPicks.Clear();
        _solved = false;
        _feedbackText = null;
        _isProcessing = false;
        _hinweisCount = 0;
    }

    private string OptionClass(WFrageOption option)
    {
        if (_wrongPicks.Contains(option.Label))
            return "lese-option-falsch";
        return _solved && option.Label == Aufgabe.Richtig ? "lese-option-richtig" : string.Empty;
    }

    private async Task Pick(WFrageOption option)
    {
        if (_isProcessing)
            return;
        _isProcessing = true;

        var aufgabe = Aufgabe;
        var richtig = option.Label == aufgabe.Richtig;
        var audio = richtig ? WordAudio.PathFor(option.AudioWort) : WordAudio.PathFor(WFragen.FragewortAudio(aufgabe.Rolle));
        if (audio is not null)
            await Js.InvokeVoidAsync("k4l_playAudioFile", "audioPlayer", audio);

        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var id = SilbenLog.GenId($"{aufgabe.Text}|{aufgabe.Rolle}", Skill.ReadWhQuestion);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = $"{aufgabe.Fragewort}: {aufgabe.Richtig}";
        var detail = $"{aufgabe.Text} | {aufgabe.Frage} → {option.Label}";

        if (richtig)
        {
            _solved = true;
            _correctCount++;
            _feedbackText = "Richtig!";
            log.Falsch = _wrongPicks.Count;
            log.Kompetenz.AddRichtig();
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, Punkte(aufgabe), Punkte(aufgabe), detail);
            StateHasChanged();

            _ = Task.Delay(CorrectDelayMs).ContinueWith(async _ => await InvokeAsync(Weiter));
        }
        else
        {
            _wrongPicks.Add(option.Label);
            _wrongCount++;
            _hinweisCount++;
            _feedbackText = $"Nochmal! Gefragt ist: {aufgabe.Fragewort.ToUpperInvariant()}?";
            log.Falsch = _wrongPicks.Count;
            log.Kompetenz.AddFalsch();
            var penalty = DifficultyInfo.Penalty(_wrongPicks.Count, ChosenTask.Difficulty);
            await Session.RecordFailure(ChosenTask, log.Kompetenz, penalty, penalty, detail);
            StateHasChanged();

            _ = Task.Delay(HinweisMs).ContinueWith(async _ => await InvokeAsync(() =>
            {
                _isProcessing = false;
                StateHasChanged();
            }));
        }

        await db.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());
    }

    /// <summary>
    ///     The first question pays for reading the sentence too, follow-up questions only for
    ///     reading the question - the sentence is already read. Half points after a wrong pick.
    /// </summary>
    private int Punkte(WFrageAufgabe aufgabe)
    {
        var punkte = _index == 0 ? LesePunkte.ForText(aufgabe.Text, aufgabe.Frage) : LesePunkte.ForText(aufgabe.Frage);
        return punkte / (_wrongPicks.Count > 0 ? 2 : 1);
    }

    private async Task Weiter()
    {
        if (_index + 1 < _serie.Fragen.Count)
        {
            ShowFrage(_index + 1);
            Session.BeginTask();
            StateHasChanged();
            return;
        }

        await OnNext.InvokeAsync();
    }
}
