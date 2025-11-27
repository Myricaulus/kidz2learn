using System.ComponentModel;
using System.Drawing;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Kidz2Learn.Shared;
using Kidz2Learn.Model;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;

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

    Random rng = new();

    protected override void OnInitialized()
    {
        NextTask();
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

    void NextTask()
    {
        // 1. Silbe auswählen
        CorrectSyllable = SyllablePool[rng.Next(SyllablePool.Count)];
        CurrentAudio = $"audio/{CorrectSyllable}.mp3";

        // 2. Optionspool vorbereiten
        //    1 richtige + 3 zufällige andere
        var shuffled = SyllablePool.OrderBy(_ => rng.Next()).ToList();

        CurrentOptions = shuffled
            .Where(s => s != CorrectSyllable)
            .Take(5)
            .Append(CorrectSyllable)
            .OrderBy(_ => rng.Next())
            .ToList();
    }

    async Task PlayAudio()
    {
        await Js.InvokeVoidAsync("k4l_playAudio", "audioPlayer");
    }

    void CheckAnswer(string answer)
    {
        bool correct = answer == CorrectSyllable;

        if (correct)
        {
            CorrectCount++;
            FeedbackText = "Richtig!";
            FeedbackClass = "k4l-feedback-correct";
            Score.AddPoints(5);
        }
        else
        {
            WrongCount++;
            FeedbackText = $"Falsch – das war '{CorrectSyllable}'.";
            FeedbackClass = "k4l-feedback-wrong";
            Score.AddPoints(-10);
        }
        
        ShowFeedback = true;
        StateHasChanged();

        _ = Task.Delay(900).ContinueWith(async _ =>
        {
            ShowFeedback = false;
            NextTask();
            StateHasChanged();
            await PlayAudio();
        });
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
