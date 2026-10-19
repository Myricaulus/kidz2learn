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
///     "w-frage" view: a sentence, a question with a color-coded question word, and the sentence's
///     own parts as options. A wrong pick only greys that option out and the child
///     picks again - the sentence stays unmarked, so the answer still has to be read. Picking an option plays its word when
///     it's in the known word list.
/// </summary>
public partial class WFrageView : ComponentBase, ITaskView
{
    private const int CorrectDelayMs = 1400;

    /// <summary>One fixed color per question word, so the color itself becomes a cue over time.</summary>
    public static readonly IReadOnlyDictionary<FrageRolle, string> Farben = new Dictionary<FrageRolle, string>
    {
        [FrageRolle.Wer] = "#e53935",
        [FrageRolle.Wen] = "#ad1457",
        [FrageRolle.Was] = "#2e7d32",
        [FrageRolle.Wo] = "#1565c0",
        [FrageRolle.Womit] = "#ef6c00",
        [FrageRolle.Wann] = "#6a1b9a"
    };

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private IChosenTask? _loadedForTask;
    private WFrageAufgabe _aufgabe = new(string.Empty, FrageRolle.Wer, "Wer?", [], string.Empty);
    private readonly HashSet<string> _wrongPicks = [];
    private bool _isProcessing;
    private bool _solved;
    private string? _feedbackText;
    private int _correctCount;
    private int _wrongCount;

    private string Farbe => Farben[_aufgabe.Rolle];

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _aufgabe = (WFrageAufgabe)ChosenTask.Payload;
        _wrongPicks.Clear();
        _solved = false;
        _feedbackText = null;
        _isProcessing = false;
    }

    private string OptionClass(WFrageOption option)
    {
        if (_wrongPicks.Contains(option.Label))
            return "lese-option-falsch";
        return _solved && option.Label == _aufgabe.Richtig ? "lese-option-richtig" : string.Empty;
    }

    private async Task Pick(WFrageOption option)
    {
        if (_isProcessing)
            return;
        _isProcessing = true;

        if (WordAudio.PathFor(option.AudioWort) is { } audio)
            await Js.InvokeVoidAsync("k4l_playAudioFile", "audioPlayer", audio);

        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var id = SilbenLog.GenId($"{_aufgabe.Text}|{_aufgabe.Rolle}", Skill.ReadWhQuestion);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = $"{_aufgabe.Fragewort}: {_aufgabe.Richtig}";
        var detail = $"{_aufgabe.Text} | {_aufgabe.Frage} → {option.Label}";

        if (option.Label == _aufgabe.Richtig)
        {
            _solved = true;
            _correctCount++;
            _feedbackText = "Richtig!";
            log.Falsch = _wrongPicks.Count;
            log.Kompetenz.AddRichtig();
            // Half points after a wrong pick.
            var points = LesePunkte.ForText(_aufgabe.Text, _aufgabe.Frage) / (_wrongPicks.Count > 0 ? 2 : 1);
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, points, points, detail);
            StateHasChanged();

            _ = Task.Delay(CorrectDelayMs)
                .ContinueWith(async _ => await InvokeAsync(() => OnNext.InvokeAsync()));
        }
        else
        {
            _wrongPicks.Add(option.Label);
            _wrongCount++;
            _feedbackText = "Nochmal! Lies den Satz noch einmal.";
            log.Falsch = _wrongPicks.Count;
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, 0, 0, detail);
            _isProcessing = false;
        }

        await db.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());
    }
}
