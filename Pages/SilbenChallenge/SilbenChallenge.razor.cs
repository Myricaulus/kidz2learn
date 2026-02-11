
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
            int i = 0;
            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "class", "log-entry arithmetik-log");
            builder.AddContent(i++, $"{Zahl1}{Op}{Zahl2} = ");
            builder.OpenElement(i++, "span");
            builder.AddAttribute(i++, "style", $"color: {(UserZahl == Zahl1+Zahl2 ? "green" : "red")}");
            builder.AddContent(i++, UserZahl);
            builder.CloseElement(); // </span>
            builder.AddContent(i++, $" ({Zahl1+Zahl2}) R:{Kompetenz.GetProzent()}");
            builder.CloseElement(); // </div>
        };
}



public partial class SilbenChallenge : ComponentBase, IAsyncDisposable
{
   // Pool: Dateiname = exakt die Silbe
       [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDB { get; set; } = default!;
       [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = default!;
    [Inject] public ScoreService Score { get; set; } = default!;
    [Inject] public SidWidgetService Player { get; set; } = default!;
    List<string> SyllablePool = new()
    {
        "mi", "im", "ma", "am", "mo", "om"
    };

    string CurrentAudio = string.Empty;
    string CorrectSyllable = string.Empty;

    List<string> CurrentOptions = new();

    bool ShowFeedback = false;
    string FeedbackText = string.Empty;
    string FeedbackClass = string.Empty;

    int CorrectCount = 0;
    int WrongCount = 0;

    List<string> WrongSelectedOption = [];
    bool TaskSolved = false;

    readonly Random _rng = new();

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

    string GetOptionClass(string option)
    {
        if (WrongSelectedOption.Contains(option))
            return "k4l-option-wrong";

        return "";
    }

    async Task NextTask()
    {
        var store = new SkillMasteryStore(AufgabenDB);
        var adaptiveTask = new AdaptiveTaskGenerator(store, _rng);
        var taskGen = await adaptiveTask.ChooseTaskAsync<SilbenTaskDefinition>();
        var task = taskGen.Task.Generator(_rng);

        // 1. Silbe auswählen
        CorrectSyllable = task.correct;
        CurrentAudio = $"audio/{CorrectSyllable}.opus";

        // 2. Optionspool vorbereiten
        //    1 richtige + 3 zufällige andere
        var shuffled = task.options
            .OrderBy(_ => _rng.Next())
            .Take(9)
            .ToList();

        CurrentOptions = shuffled;
    }

    async Task PlayAudio()
    {
        await Js.InvokeVoidAsync("k4l_playAudio", "audioPlayer");
    }

    async Task CheckAnswer(string answer)
    {
        string correctAnswer = CorrectSyllable.Replace("-", "");
        bool correct = answer == correctAnswer;

        if (correct)
        {
            TaskSolved = true;
            CorrectCount++;
            FeedbackText = "Richtig!";
            FeedbackClass = "k4l-feedback-correct";
            Score.AddPoints(3,5);

            ShowFeedback = true;
            StateHasChanged();
            // Reset für nächste Runde
            _ = Task.Delay(900).ContinueWith(async _ =>
            {
                ShowFeedback = false;
                await NextTask();
                WrongSelectedOption = [];
                ShowFeedback = false;
                StateHasChanged();
                await PlayAudio();
            });

            
        }
        else
        {
            WrongCount++;
            WrongSelectedOption.Add(answer);

            FeedbackText = "Nochmal versuchen!";
            FeedbackClass = "k4l-feedback-wrong";

            Score.AddPoints(-5,-5);

            ShowFeedback = true;
        }
    }

    private string GetColoredHtml(string silbe)
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
