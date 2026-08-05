using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;

namespace Kidz2Learn.Pages.SilbenChallenge;


// ReSharper disable once ClassNeverInstantiated.Global
public partial class GraphemChallenge : ComponentBase, IAsyncDisposable
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
    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] public ScoreService Score { get; set; } = null!;
    [Inject] public SidWidgetService Player { get; set; } = null!;
    [Inject] public AffirmationService Affirmation { get; set; } = null!;
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
        _currentTaskDef = await adaptiveTask.ChooseTaskAsync<SilbenTaskDefinition>([Skill.GraphemPhonem]);
        var task = _currentTaskDef.Task.Generator(_rng);

        // 1. Silbe auswählen
        _correctSyllable = task.correct;

        // 2. Optionspool vorbereiten
        //    1 richtige + 3 zufällige andere
        var shuffled = task.options
            .OrderBy(_ => _rng.Next())
            .ToList();

        _currentOptions = shuffled;
    }

    private async Task CheckAnswer(string answer)
    {
        if(_isProcessing)
            return;
        _isProcessing = true;
            
        var correctAnswer = _correctSyllable.Replace("-", "");
        var correct = answer == correctAnswer;

        if (correct)
        {
            _correctCount++;
            _feedbackText = "Richtig!";
            _feedbackClass = "k4l-feedback-correct";
            Score.AddPoints(5, 5);
            await Affirmation.PlayErfolgAsync();
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
            // Diese Antwort war richtig - die vorangegangenen Fehlversuche (falls welche) haben
            // bereits im else-Zweig unten je einen eigenen AddFalsch()/Fail()-Eintrag bekommen.
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
            _isProcessing = false;
            await Affirmation.PlayMisserfolgAsync();

            if (_currentTaskDef is not null)
            {
                var id = SilbenLog.GenId(correctAnswer, _currentTaskDef.Task.Skills.First());
                var log = await ReadingDb.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
                log.Wort = correctAnswer;
                log.Falsch = _wrongCount;
                log.Kompetenz.AddFalsch();
                await _currentTaskDef.Fail(log.Kompetenz);
                await ReadingDb.StoreItemAsync(log);
            }
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