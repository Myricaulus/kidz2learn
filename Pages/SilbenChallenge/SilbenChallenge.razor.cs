using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;

namespace Kidz2Learn.Pages.SilbenChallenge;

public class SilbenLog : IIdItem
{
    [JsonIgnore] public string Wort { get; set; } = string.Empty;

    public Kompetenzniveau Kompetenz { get; set; } = new();

    [JsonIgnore] public int Falsch { get; set; }

    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }


    public static string GenId(string word, string skill)
    {
        var abb = StringAbbreviator.Abbreviate(skill);
        return word + "-" + abb;
    }

    public RenderFragment ToRenderFragment()
    {
        return builder =>
        {
            var i = 0;
            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "class", "log-entry arithmetik-log");
            builder.AddContent(i++, $"{Id} = ");
            builder.OpenElement(i++, "span");
            builder.AddAttribute(i++, "style", $"color: {(Falsch == 0 ? "green" : "red")}");
            builder.AddContent(i++, $"V:{Falsch + 1}");
            builder.CloseElement(); // </span>
            builder.AddContent(i, $" ({Wort}) R:{Kompetenz.GetProzent()}");
            builder.CloseElement(); // </div>
        };
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public partial class SilbenChallenge : ComponentBase, IAsyncDisposable
{
    private readonly Random _rng = new();

    private int _correctCount;
    private string _correctSyllable = string.Empty;

    private string _currentAudio = string.Empty;

    private List<string> _currentOptions = [];
    private LearningTask<SilbenTaskDefinition>? _currentTaskDef;
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
    private HashSet<int> _requiredMarks = [];
    private Dictionary<int, char> _requiredSubstitutions = [];
    private Dictionary<int, char> _requiredGaps = [];
    private HashSet<int> _markedIndices = [];
    private Dictionary<int, char> _letterCorrections = [];
    private Dictionary<int, char> _insertedLetters = [];
    private int? _openGapIndex;
    private int? _openLetterIndex;
    private bool _needsGapFocus;
    private bool _needsLetterFocus;
    private ElementReference _gapInputRef;
    private ElementReference _letterInputRef;
    private int _failedPopupAttempts;
    private bool _shakeWrongBox;
    private bool _hoverArmed;

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] public ScoreService Score { get; set; } = null!;
    [Inject] public SidWidgetService Player { get; set; } = null!;
    private IndexedDbStore ReadingDb { get; set; } = null!;

    public async ValueTask DisposeAsync()
    {
        await Player.SetVolume(1.0);
    }

    protected override async Task OnInitializedAsync()
    {
        ReadingDb = AufgabenDb["LeseAufgaben"] ?? throw new Exception("IndexedDb not instanced");
        await NextTask();
    }


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Player.SetVolume(0.1);
            await PlayAudio();
        }

        if (_needsGapFocus)
        {
            _needsGapFocus = false;
            await _gapInputRef.FocusAsync();
        }

        if (_needsLetterFocus)
        {
            _needsLetterFocus = false;
            await Js.InvokeVoidAsync("k4l_focusAndSelect", _letterInputRef);
        }
    }

    private string GetOptionClass(string option)
    {
        if (_wrongSelectedOption.Contains(option))
            return "k4l-option-wrong";

        return "";
    }

    private async Task NextTask()
    {
        var store = new SkillMasteryStore(AufgabenDb);
        var adaptiveTask = new AdaptiveTaskGenerator(store, _rng);
        _currentTaskDef =
            await adaptiveTask.ChooseTaskAsync<SilbenTaskDefinition>([Skill.ReadSyllables, Skill.ReadPrecise]);
        var task = _currentTaskDef.Task.Generator(_rng);

        // 1. Silbe auswählen
        _correctSyllable = task.correct;
        _currentAudio = $"audio/{_correctSyllable}.opus";

        // 2. Optionspool vorbereiten
        //    1 richtige + 3 zufällige andere
        var shuffled = task.options
            .OrderBy(_ => _rng.Next())
            .ToList();

        _currentOptions = shuffled;
    }

    private async Task PlayAudio()
    {
        await Js.InvokeVoidAsync("k4l_playAudio", "audioPlayer");
    }

    private async Task CheckAnswer(string answer)
    {
        if(_isProcessing)
            return;
        _isProcessing = true;
            
        var correctAnswer = _correctSyllable.Replace("-", "");
        var correct = answer.Equals( correctAnswer, StringComparison.OrdinalIgnoreCase);

        if (correct)
        {
            _correctCount++;
            _feedbackText = "Richtig!";
            _feedbackClass = "k4l-feedback-correct";
            Score.AddPoints(5, 5);
            if (_currentTaskDef is null)
                throw new InvalidOperationException(
                    "Cannot Check answer if no task has been given."); // should never land here
            var id = SilbenLog.GenId(correctAnswer, _currentTaskDef.Task.Skills.First());
            var log = await ReadingDb.GetItemAsync<SilbenLog>(id) ?? new SilbenLog
            {
                Id = id
            };
            log.Wort = correctAnswer;
            log.Falsch = _wrongCount;
            if (_wrongCount > 0)
                log.Kompetenz.AddFalsch();
            else
                log.Kompetenz.AddRichtig();

            await (_currentTaskDef?.Success(log.Kompetenz) ?? Task.CompletedTask);
            _showFeedback = true;
            StateHasChanged();
            // Reset für nächste Runde
            _ = Task.Delay(900).ContinueWith(async _ =>
            {
                await NextTask();               
                _wrongSelectedOption = [];
                _showFeedback = false;
                _isProcessing = false;
                StateHasChanged();
                await PlayAudio();
            });
            await ReadingDb.StoreItemAsync(log);
            Logger.Log(log.ToRenderFragment());

            Logger.Erfolgreich++;
            Logger.GesamtAnzahl += 1 + _wrongCount;
            _wrongCount = 0;
        }
        else
        {
            _wrongCount++;
            // TODO Es so machen, die zu vergebenen +-Punkte ebenfalls aus der _currentTaskDef kommen, so kann jede Aufgabe die Punkte aufteilen.
            Score.AddPoints(-10 * _wrongCount, -10 * _wrongCount);
            _wrongSelectedOption.Add(answer);

            _feedbackText = "Nochmal versuchen!";
            _feedbackClass = "k4l-feedback-wrong";

            _showFeedback = true;

            OpenMarkierPopup(correctAnswer, answer);
            // _isProcessing bleibt true, solange das Popup offen ist -
            // erst nach korrekter Fehleranalyse (CloseMarkierPopup) geht's weiter.
        }
    }

    // --- Fehler-Markier-Popup ---

    private void OpenMarkierPopup(string correctWord, string wrongWord)
    {
        _popupCorrectWord = correctWord;
        _popupWrongWord = wrongWord;

        var requirements = WordDiff.BuildRequirements(correctWord, wrongWord);
        _requiredMarks = requirements.RequiredMarks;
        _requiredSubstitutions = requirements.RequiredSubstitutions;
        _requiredGaps = requirements.RequiredGaps;

        _markedIndices = [];
        _letterCorrections = [];
        _insertedLetters = [];
        _openGapIndex = null;
        _openLetterIndex = null;
        _failedPopupAttempts = 0;
        _shakeWrongBox = false;
        _hoverArmed = false;

        _showMarkierPopup = true;
    }

    private void OnCorrectBoxEnter()
    {
        _hoverArmed = true;
    }

    private void OpenLetterEdit(int index)
    {
        _openLetterIndex = index;
        _needsLetterFocus = true;
    }

    private void CloseLetterEdit()
    {
        _openLetterIndex = null;
    }

    private void OnLetterInput(int index, ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? "";
        if (text.Length > 0)
        {
            _letterCorrections[index] = text[^1];
            _markedIndices.Remove(index);
            _openLetterIndex = null;
        }
        else
        {
            _letterCorrections.Remove(index);
        }
    }

    private void OnLetterKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Key is "Enter" or "Escape" or "Tab")
            _openLetterIndex = null;
    }

    private void MarkLetterAsExtra(int index)
    {
        _markedIndices.Add(index);
        _letterCorrections.Remove(index);
        _openLetterIndex = null;
    }

    private void OpenGap(int gapIndex)
    {
        _openGapIndex = gapIndex;
        _needsGapFocus = true;
    }

    private void CloseGap()
    {
        _openGapIndex = null;
    }

    private void OnGapInput(int gapIndex, ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? "";
        if (text.Length > 0)
            _insertedLetters[gapIndex] = text[^1];
        else
            _insertedLetters.Remove(gapIndex);
    }

    private void OnGapKeyDown(KeyboardEventArgs e, int gapIndex)
    {
        if (e.Key is "Enter" or "Escape" or "Tab")
            _openGapIndex = null;
    }

    private void RemoveGapLetter(int gapIndex)
    {
        _insertedLetters.Remove(gapIndex);
    }

    private async Task OnWeiterClicked()
    {
        var marksOk = _markedIndices.SetEquals(_requiredMarks);
        var substitutionsOk = _requiredSubstitutions.Count == _letterCorrections.Count &&
                               _requiredSubstitutions.All(kv =>
                                   _letterCorrections.TryGetValue(kv.Key, out var typed) &&
                                   char.ToLowerInvariant(typed) == char.ToLowerInvariant(kv.Value));
        var gapsOk = _requiredGaps.Count == _insertedLetters.Count &&
                     _requiredGaps.All(kv =>
                         _insertedLetters.TryGetValue(kv.Key, out var typed) &&
                         char.ToLowerInvariant(typed) == char.ToLowerInvariant(kv.Value));

        if (marksOk && substitutionsOk && gapsOk)
        {
            _showMarkierPopup = false;
            _isProcessing = false;
            return;
        }

        _failedPopupAttempts++;
        _shakeWrongBox = true;
        StateHasChanged();
        await Task.Delay(500);
        _shakeWrongBox = false;
        StateHasChanged();
    }

    private void ResetMarkierPopup()
    {
        _markedIndices = [];
        _letterCorrections = [];
        _insertedLetters = [];
        _openGapIndex = null;
        _openLetterIndex = null;
    }

    private void GiveHint()
    {
        foreach (var mark in _requiredMarks)
            if (!_markedIndices.Contains(mark))
            {
                _markedIndices.Add(mark);
                return;
            }

        foreach (var marked in _markedIndices)
            if (!_requiredMarks.Contains(marked))
            {
                _markedIndices.Remove(marked);
                return;
            }

        foreach (var (index, expected) in _requiredSubstitutions)
            if (!_letterCorrections.TryGetValue(index, out var typed) ||
                char.ToLowerInvariant(typed) != char.ToLowerInvariant(expected))
            {
                _letterCorrections[index] = expected;
                _markedIndices.Remove(index);
                return;
            }

        foreach (var (gapIndex, expected) in _requiredGaps)
            if (!_insertedLetters.TryGetValue(gapIndex, out var typed) ||
                char.ToLowerInvariant(typed) != char.ToLowerInvariant(expected))
            {
                _insertedLetters[gapIndex] = expected;
                return;
            }
    }

    private string WrongWordRowStyle
    {
        get
        {
            const int comfortableLength = 8;
            var length = _popupWrongWord.Length;
            var scale = length <= comfortableLength ? 1.0 : Math.Max(0.6, (double)comfortableLength / length);
            return $"--k4l-scale:{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
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