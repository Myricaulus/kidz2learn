
using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;
using Kidz2Learn.Model.Tasks.TaskDefs;
using MudBlazor;

namespace Kidz2Learn.Pages.SilbenChallenge;

public class SilbenLog : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonIgnore]
    public int Zahl1 { get; set; }
    [JsonIgnore]
    public string Op { get; set; } = string.Empty;
    [JsonIgnore]
    public int Zahl2 { get; set; }
    [JsonIgnore]
    public int UserZahl { get; set; }

    public Kompetenzniveau Kompetenz { get; set; } = new();
    [JsonIgnore]
    public int Richtig { get; set; }
    [JsonIgnore]
    public int Falsch { get; set; }

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }


    public RenderFragment ToRenderFragment() => builder =>
        {
            var i = 0;
            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "class", "log-entry arithmetik-log");
            builder.AddContent(i++, $"{Zahl1}{Op}{Zahl2} = ");
            builder.OpenElement(i++, "span");
            builder.AddAttribute(i++, "style", $"color: {(UserZahl == Zahl1+Zahl2 ? "green" : "red")}");
            builder.AddContent(i++, UserZahl);
            builder.CloseElement(); // </span>
            builder.AddContent(i, $" ({Zahl1+Zahl2}) R:{Kompetenz.GetProzent()}");
            builder.CloseElement(); // </div>
        };
}



// ReSharper disable once ClassNeverInstantiated.Global
public partial class SilbenChallenge : ComponentBase, IAsyncDisposable
{
    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    //[Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] public ScoreService Score { get; set; } = null!;
    [Inject] public SidWidgetService Player { get; set; } = null!;

    private string _currentAudio = string.Empty;
    private string _correctSyllable = string.Empty;

    private List<string> _currentOptions = [];

    private bool _showFeedback;
    private string _feedbackText = string.Empty;
    private string _feedbackClass = string.Empty;

    private int _correctCount;
    private int _wrongCount;

    private List<string> _wrongSelectedOption = [];

    private readonly Random _rng = new();

    protected override async Task OnInitializedAsync()
    {
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

    public async ValueTask DisposeAsync()
    {
        await Player.SetVolume(1.0);
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
        var taskGen = await adaptiveTask.ChooseTaskAsync<SilbenTaskDefinition>();
        var task = taskGen.Task.Generator(_rng);

        // 1. Silbe auswählen
        _correctSyllable = task.correct;
        _currentAudio = $"audio/{_correctSyllable}.opus";

        // 2. Optionspool vorbereiten
        //    1 richtige + 3 zufällige andere
        var shuffled = task.options
            .OrderBy(_ => _rng.Next())
            .Take(9)
            .ToList();

        _currentOptions = shuffled;
    }

    private async Task PlayAudio()
    {
        await Js.InvokeVoidAsync("k4l_playAudio", "audioPlayer");
    }

    private void CheckAnswer(string answer)
    {
        var correctAnswer = _correctSyllable.Replace("-", "");
        var correct = answer == correctAnswer;

        if (correct)
        {
            _correctCount++;
            _feedbackText = "Richtig!";
            _feedbackClass = "k4l-feedback-correct";
            Score.AddPoints(3,5);

            _showFeedback = true;
            StateHasChanged();
            // Reset für nächste Runde
            _ = Task.Delay(900).ContinueWith(async _ =>
            {
                await NextTask();
                _wrongSelectedOption = [];
                _showFeedback = false;
                StateHasChanged();
                await PlayAudio();
            });
        }
        else
        {
            _wrongCount++;
            _wrongSelectedOption.Add(answer);

            _feedbackText = "Nochmal versuchen!";
            _feedbackClass = "k4l-feedback-wrong";

            Score.AddPoints(-5,-5);

            _showFeedback = true;
        }
    }

    private static string GetColoredHtml(string silbe)
    {
        var result = "";

        foreach (var c in silbe)
        {
            if ("aeiouäöüAEIOUÄÖÜ".Contains(c))
            {
                // Vokal
                result += $"<span style='color:#0077ff;font-weight:bold'>{c}</span>";
            }
            else
            {
                // Konsonant
                result += $"<span style='color:#ff0066;font-weight:bold'>{c}</span>";
            }
        }

        return result;
    }


}
