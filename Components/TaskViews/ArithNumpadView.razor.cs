using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "arith-numpad" view: written-addition/subtraction digit grid + virtual numpad. Extracted out
///     of the old ArithmeticChallenge.razor(.cs); see TASK_PRESENTATION_REDESIGN.md Baustein 4/5.
///     Registered under "arith-numpad" in TaskPresentationRegistry - rendered generically by
///     TaskHost via DynamicComponent, not routed to directly.
/// </summary>
public partial class ArithNumpadView : ComponentBase, ITaskView
{
    // Konfiguration
    private const int MaxLength = 2;

    private readonly List<Dictionary<string, string>> _numpadLayout =
    [
        new() { { "7", "7" }, { "8", "8" }, { "9", "9" } },
        new() { { "4", "4" }, { "5", "5" }, { "6", "6" } },
        new() { { "1", "1" }, { "2", "2" }, { "3", "3" } },
        new() { { "0", "0" }, { "➡️", "Backspace" }, { "↩️", "Enter" } }
    ];

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    private readonly int?[] _userDigits = new int?[MaxLength + 1];
    private int _expectedResult;

    private string _feedback = "";
    private bool _isAddition;

    private int _number1;
    private int[] _number1Digits = new int[MaxLength];
    private int _number2;
    private int[] _number2Digits = new int[MaxLength];

    private ElementReference[] _refs = new ElementReference[MaxLength + 1];

    // Guards the reset below against firing again on incidental re-renders that don't actually
    // carry a new ChosenTask - see SilbenMultipleChoiceView / TASK_PRESENTATION_REDESIGN.md Baustein 5.
    private IChosenTask? _loadedForTask;

    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private int CurrentIndex { get; set; }
    private IndexedDbStore ArithDb { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        ArithDb = AufgabenDb["ArithmetikAufgaben"] ?? throw new Exception("IndexedDb not instanced");

        var stats = await ArithDb.GetItemAsync<ArithemticLogStats>("0") ?? new ArithemticLogStats();
        Logger.Erfolgreich = stats.RichtigProzent();
        Logger.GesamtAnzahl = stats.Versuche;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        var (x, y, _, op) = ((int? x, int? y, int? z, ArithOperator op))ChosenTask.Payload;
        _number1 = x!.Value;
        _number2 = y!.Value;
        _isAddition = op == ArithOperator.Addition;
        _expectedResult = _isAddition ? _number1 + _number2 : _number1 - _number2;

        _number1Digits = ExtractDigits(_number1, MaxLength);
        _number2Digits = ExtractDigits(_number2, MaxLength);

        _feedback = "";

        await Js.InvokeVoidAsync("elementInterop.emptyElementById", "digit-", MaxLength + 1);

        // Fokus auf erster Stelle rechts (wie bei schriftlichem Rechnen)
        _ = Task.Delay(100).ContinueWith(async _ =>
        {
            for (var i = 0; i < _userDigits.Length; i++) _userDigits[i] = null;
            await Fokus(MaxLength);
        });
    }

    private static int[] ExtractDigits(int number, int length)
    {
        var digits = new int[length];
        for (var i = length - 1; i >= 0; i--)
        {
            digits[i] = number % 10;
            number /= 10;
        }

        return digits;
    }

    private async Task Fokus(int index)
    {
        await InvokeAsync(async () =>
        {
            await Task.Delay(1); // minimal warten auf Render-Fertigstellung
            index = Math.Clamp(index, 0, MaxLength);
            await _refs[index].FocusAsync();
        });
    }

    private async Task OnVirtualKeyPress(string key)
    {
        var fakeEvent = new KeyboardEventArgs { Key = key };
        await OnKeyDown(fakeEvent, CurrentIndex);
    }

    private async Task OnKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Key == "Backspace" || e.Key == "Delete")
        {
            if (_userDigits[index] is not null)
            {
                _userDigits[index] = null;
            }
            else if (index < MaxLength)
            {
                _userDigits[index + 1] = null;
                await Fokus(index + 1);
            }
        }
        else if (char.IsDigit(e.Key.FirstOrDefault()))
        {
            _userDigits[index] = int.Parse(e.Key);
            // Weiter zur nächsten Stelle
            if (index > 0)
                await Fokus(index - 1);
            else
                await Evaluate();
        }
        else if (e.Key == "Enter")
        {
            await Evaluate();
        }
        // Sonst: ignorieren
    }

    private async Task Evaluate()
    {
        var userValue = 0;
        for (var i = 0; i < MaxLength + 1; i++) userValue = userValue * 10 + (_userDigits[i] ?? 0);

        var id = $"{_number1}{(_isAddition ? "+" : "-")}{_number2}";
        var stats = await ArithDb.GetItemAsync<ArithemticLogStats>("0") ?? new ArithemticLogStats();
        var log = await ArithDb.GetItemAsync<ArithemticLog>(id) ?? new ArithemticLog
        {
            Id = id
        };

        log.Zahl1 = _number1;
        log.Zahl2 = _number2;
        log.Zahl3 = null;
        log.Op = _isAddition ? "+" : "-";
        log.UserZahl = userValue;
        log.Richtig = userValue == _expectedResult;

        if (userValue == _expectedResult)
        {
            log.Kompetenz.AddRichtig();
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, 2, 8);
            stats.Erfolgreich++;
            stats.Versuche++;
        }
        else
        {
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, -5, 0);
            stats.Versuche++;
            _feedback =
                $"Falsch! Richtige Lösung: {_expectedResult}.<br />Versuche: {log.Kompetenz.Versuche}. Richtig:{log.Kompetenz.GetProzent()}";
            await Js.InvokeVoidAsync("elementInterop.emptyElementById", "digit-", MaxLength + 1);

            // Fokus auf erster Stelle rechts (wie bei schriftlichem Rechnen)
            _ = Task.Delay(100).ContinueWith(async _ =>
            {
                for (var i = 0; i < _userDigits.Length; i++) _userDigits[i] = null;
                await Fokus(MaxLength);
            });
        }

        await ArithDb.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());

        Logger.Erfolgreich = stats.RichtigProzent();
        Logger.GesamtAnzahl = stats.Versuche;
        await ArithDb.StoreItemAsync(stats);

        if (log.Richtig)
            await OnNext.InvokeAsync();

        StateHasChanged();
    }
}
