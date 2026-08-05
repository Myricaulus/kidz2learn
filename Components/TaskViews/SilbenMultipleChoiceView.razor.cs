using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Pages.SilbenChallenge;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "silben-multiple-choice" view: audio playback + multiple-choice options + the mark-your-mistake
///     popup for read_precise. Extracted out of the old monolithic SilbenChallenge.razor(.cs); see
///     TASK_PRESENTATION_REDESIGN.md Baustein 4/5. Registered under "silben-multiple-choice" in
///     TaskPresentationRegistry - rendered generically by TaskHost via DynamicComponent, not routed
///     to directly.
/// </summary>
public partial class SilbenMultipleChoiceView : ComponentBase, ITaskView, IAsyncDisposable
{
    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    private readonly Random _rng = new();

    private int _correctCount;
    private string _correctSyllable = string.Empty;
    private string _currentAudio = string.Empty;
    private List<string> _currentOptions = [];
    private string _feedbackClass = string.Empty;
    private string _feedbackText = string.Empty;
    private bool _showFeedback;
    private int _wrongCount;
    private bool _isProcessing;
    private List<string> _wrongSelectedOption = [];

    // --- Fehler-Markier-Popup ---
    private bool _showMarkierPopup;
    private string _popupCorrectWord = string.Empty;
    private string _popupWrongWord = string.Empty;

    // Guards the "new round" reset below against firing again on incidental re-renders that don't
    // actually carry a new ChosenTask - see TASK_PRESENTATION_REDESIGN.md Baustein 5.
    private IChosenTask? _loadedForTask;

    // GraphemPhonem shares this view's payload shape (string correct, string[] options) but is a
    // silent visual-discrimination exercise, not a listening one - unlike ReadSyllables/ReadPrecise,
    // it never had an audio button, and its Generator's "correct" is the WordMeta dictionary key,
    // not the .filename used for audio lookup, so playing it could 404 anyway. Kept skill-gated
    // here rather than as a separate view, same pattern as the ReadPrecise-only popup below.
    private bool _hasAudio;

    private string Title => _hasAudio
        ? "Hörübung: Welches Wort hörst du?"
        : "Übung: Welches Wort gehört nicht dazu? Manche Buchstaben werden manchmal anders ausgesprochen.";

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private SidWidgetService Player { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private IndexedDbStore ReadingDb { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        ReadingDb = AufgabenDb["LeseAufgaben"] ?? throw new Exception("IndexedDb not instanced");
    }

    public async ValueTask DisposeAsync()
    {
        await Player.Restore();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Fires on every parameter-set pass, but TaskHost only rebuilds its Parameters dict when it
        // actually picked a new task (see Components/TaskHost.razor) - so a reference change here
        // reliably means "new round", covering both the very first task and every subsequent one in
        // a single path (the old SilbenChallenge needed two separate call sites for that).
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _hasAudio = ChosenTask.Skills.Contains(Skill.ReadSyllables) || ChosenTask.Skills.Contains(Skill.ReadPrecise);
        // Ducked/restored per task, not just once on init: "Deutsch-Mix" (TASK_PRESENTATION_REDESIGN.md
        // Phase 5) can alternate audio (read_syllables/read_precise) and silent (GraphemPhonem) tasks
        // within the same session. Duck()/Restore() (not SetVolume(1.0)) so a silent task restores
        // the user's own manually-chosen volume instead of blasting it back to full.
        if (_hasAudio)
            await Player.Duck(0.1);
        else
            await Player.Restore();

        var (correct, options) = ((string correct, string[] options))ChosenTask.Payload;
        _correctSyllable = correct;
        _currentAudio = $"audio/{_correctSyllable}.opus";
        _currentOptions = options.OrderBy(_ => _rng.Next()).ToList();

        _wrongSelectedOption = [];
        _showFeedback = false;
        _isProcessing = false;

        if (_hasAudio)
            await PlayAudio();
    }

    private string GetOptionClass(string option)
    {
        return _wrongSelectedOption.Contains(option) ? "k4l-option-wrong" : "";
    }

    private async Task PlayAudio()
    {
        await Js.InvokeVoidAsync("k4l_playAudioFile", "audioPlayer", _currentAudio);
    }

    private async Task CheckAnswer(string answer)
    {
        if (_isProcessing)
            return;
        _isProcessing = true;

        var correctAnswer = _correctSyllable.Replace("-", "");
        var correct = answer.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase);

        if (correct)
        {
            _correctCount++;
            _feedbackText = "Richtig!";
            _feedbackClass = "k4l-feedback-correct";
            var id = SilbenLog.GenId(correctAnswer, ChosenTask.Skills.First());
            var log = await ReadingDb.GetItemAsync<SilbenLog>(id) ?? new SilbenLog
            {
                Id = id
            };
            log.Wort = correctAnswer;
            log.Falsch = _wrongCount;
            // Diese Antwort war richtig - vorangegangene Fehlversuche (falls welche) haben bereits
            // im else-Zweig unten je einen eigenen AddFalsch()/RecordFailure()-Eintrag bekommen.
            log.Kompetenz.AddRichtig();

            await Session.RecordSuccess(ChosenTask, log.Kompetenz, 5, 5);
            _showFeedback = true;
            StateHasChanged();
            // Feedback bleibt sichtbar, bis TaskHost die nächste Aufgabe geliefert hat -
            // OnParametersSetAsync oben räumt dann in einem Rutsch auf (Optionen, Feedback, Audio).
            _ = Task.Delay(900).ContinueWith(async _ => await OnNext.InvokeAsync());

            await ReadingDb.StoreItemAsync(log);
            Logger.Log(log.ToRenderFragment());

            _wrongCount = 0;
        }
        else
        {
            _wrongCount++;
            // TODO Es so machen, die zu vergebenen +-Punkte ebenfalls aus dem ChosenTask kommen, so kann jede Aufgabe die Punkte aufteilen.
            _wrongSelectedOption.Add(answer);

            _feedbackText = "Nochmal versuchen!";
            _feedbackClass = "k4l-feedback-wrong";
            _showFeedback = true;

            var id = SilbenLog.GenId(correctAnswer, ChosenTask.Skills.First());
            var log = await ReadingDb.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
            log.Wort = correctAnswer;
            log.Falsch = _wrongCount;
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, -10 * _wrongCount, -10 * _wrongCount);
            await ReadingDb.StoreItemAsync(log);

            if (ChosenTask.Skills.Contains(Skill.ReadPrecise))
            {
                OpenMarkierPopup(correctAnswer, answer);
                // _isProcessing bleibt true, solange das Popup offen ist -
                // erst nach korrekter Fehleranalyse (OnPopupResolved) geht's weiter.
            }
            else
            {
                // Bei anderen Aufgabentypen ist die falsche Auswahl oft ein völlig
                // anderes Wort (kein Tippfehler) - Buchstaben-Korrektur ergibt da keinen Sinn.
                _isProcessing = false;
            }
        }
    }

    private void OpenMarkierPopup(string correctWord, string wrongWord)
    {
        _popupCorrectWord = correctWord;
        _popupWrongWord = wrongWord;
        _showMarkierPopup = true;
    }

    private void OnPopupResolved()
    {
        _showMarkierPopup = false;
        _isProcessing = false;
    }

    private static string GetColoredHtml(string silbe)
    {
        var result = "";

        foreach (var c in silbe)
            if ("aeiouäöüAEIOUÄÖÜ".Contains(c))
                // Vokal
                result += $"<span style='color:#0077ff;font-weight:bold'>{c}</span>";
            else
                // Konsonant
                result += $"<span style='color:#ff0066;font-weight:bold'>{c}</span>";

        return result;
    }
}
