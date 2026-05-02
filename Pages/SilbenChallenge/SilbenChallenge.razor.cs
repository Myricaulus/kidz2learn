using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
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
        _currentTaskDef = await adaptiveTask.ChooseTaskAsync<SilbenTaskDefinition>(skill: Skill.ReadPrecise);
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
        var correct = answer == correctAnswer;

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
            _isProcessing = false;
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