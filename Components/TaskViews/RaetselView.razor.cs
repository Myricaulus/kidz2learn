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
///     "raetsel" view ("Wer bin ich?"): three clues, one per line, and three things to pick from. A
///     wrong pick highlights the deciding (last) clue and the child picks again. Picking an option
///     plays its word when it's in the known word list.
/// </summary>
public partial class RaetselView : ComponentBase, ITaskView
{
    private const int CorrectDelayMs = 1400;
    private const string HintColor = "#ef6c00";

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private IChosenTask? _loadedForTask;
    private RaetselAufgabe _aufgabe = new([string.Empty], [], string.Empty);
    private readonly HashSet<string> _wrongPicks = [];
    private bool _isProcessing;
    private bool _showHint;
    private bool _solved;
    private string? _feedbackText;
    private int _correctCount;
    private int _wrongCount;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _aufgabe = (RaetselAufgabe)ChosenTask.Payload;
        _wrongPicks.Clear();
        _showHint = false;
        _solved = false;
        _feedbackText = null;
        _isProcessing = false;
    }

    private string OptionClass(string option)
    {
        if (_wrongPicks.Contains(option))
            return "lese-option-falsch";
        return _solved && option == _aufgabe.Richtig ? "lese-option-richtig" : string.Empty;
    }

    private async Task Pick(string option)
    {
        if (_isProcessing)
            return;
        _isProcessing = true;

        if (WordAudio.PathFor(option) is { } audio)
            await Js.InvokeVoidAsync("k4l_playAudioFile", "audioPlayer", audio);

        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var id = SilbenLog.GenId(_aufgabe.Richtig, Skill.ReadRiddle);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = $"Rätsel: {_aufgabe.Richtig}";
        var detail = $"{string.Join(" ", _aufgabe.Hinweise)} | [{string.Join(", ", _aufgabe.Optionen)}] → {option}";

        if (option == _aufgabe.Richtig)
        {
            _solved = true;
            _correctCount++;
            _feedbackText = "Richtig!";
            log.Falsch = _wrongPicks.Count;
            log.Kompetenz.AddRichtig();
            // Half points once the deciding clue has been highlighted after a wrong pick.
            var points = LesePunkte.ForText([.. _aufgabe.Hinweise]) / (_wrongPicks.Count > 0 ? 2 : 1);
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, points, points, detail);
            StateHasChanged();

            _ = Task.Delay(CorrectDelayMs)
                .ContinueWith(async _ => await InvokeAsync(() => OnNext.InvokeAsync()));
        }
        else
        {
            _wrongPicks.Add(option);
            _wrongCount++;
            _showHint = true;
            _feedbackText = "Nochmal! Lies den markierten Satz.";
            log.Falsch = _wrongPicks.Count;
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, 0, 0, detail);
            _isProcessing = false;
        }

        await db.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());
    }
}
