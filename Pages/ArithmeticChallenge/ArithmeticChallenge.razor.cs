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

namespace Kidz2Learn.Pages.ArithmeticChallenge;

public class LevelDefinition
{
    public int LevelNumber { get; init; }
    public required Func<Random, (int number1, int number2)> Generator { get; init; }
}


public static class LevelRegistry
{
    private static readonly Dictionary<ArithOperator, List<LevelDefinition>> _levels =
        new();

    static LevelRegistry()
    {
        _levels[ArithOperator.Addition] = new()
        {
            new LevelDefinition {
                LevelNumber = 1,
                Generator = rng => (rng.Next(1,5), rng.Next(1,5))
            },
            new LevelDefinition {
                LevelNumber = 2,
                Generator = rng => {
                    int a, b;
                    do {
                        a = rng.Next(1,10);
                        b = rng.Next(1,10);
                    } while(a + b >= 10);
                    return (a, b);
                }
            },
            new LevelDefinition {
                LevelNumber = 3,
                Generator = rng => (rng.Next(3,10), rng.Next(3,10))
            },
            new LevelDefinition {
                LevelNumber = 4,
                Generator = rng => (rng.Next(1,20), rng.Next(1,10))
            },
            new LevelDefinition {
                LevelNumber = 5,
                Generator = rng => (rng.Next(1,20), rng.Next(1,20))
            },
            new LevelDefinition {
                LevelNumber = 6,
                Generator = rng => (rng.Next(1,50), rng.Next(1,50))
            },
            new LevelDefinition {
                LevelNumber = 7,
                Generator = rng => (rng.Next(1,100), rng.Next(1,100))
            }
        };
    }

    public static IReadOnlyList<LevelDefinition> Get(ArithOperator op) => _levels[op];
}


public partial class ArithmeticChallenge : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDB { get; set; } = default!;
    [Inject] private LoggerService Logger { get; set; } = default!;
    [Inject] public ScoreService Score { get; set; } = default!;
    [Inject] private HUDStateService HUD { get; set; } = default!;

    private int CurrentIndex { get; set; }
    public IndexedDbStore ArithDb { get; private set; } = default!;

    // Konfiguration
    private const int MaxLength = 2;
    private readonly Random _rng = new();

    private readonly List<Dictionary<string, string>> _numpadLayout =
    [
        new() {{"7","7"}, {"8","8"}, {"9","9"}},
        new() {{"4","4"}, {"5","5"}, {"6","6"}},
        new() {{"1","1"}, {"2","2"}, {"3","3"}},
        new() {{"0","0"}, { "➡️","Backspace" }, {"↩️","Enter"}}
    ];

    private int _number1;
    private int _number2;
    private bool _isAddition;
    private int _expectedResult;

    private int[] _number1Digits = new int[MaxLength];
    private int[] _number2Digits = new int[MaxLength];
    private int?[] _userDigits = new int?[MaxLength + 1];

    private ElementReference[] _refs = new ElementReference[MaxLength + 1];

    private string _feedback = "";
    private LearningTask? _currentTaskDef;
    private static readonly SHA256 sha;
    private readonly byte MaxValue = 20;

    static ArithmeticChallenge()
    {
        sha = SHA256.Create();
    }

    protected override async Task OnInitializedAsync()
    {
        _refs = new ElementReference[MaxLength + 1];
        ArithDb = AufgabenDB["ArithmetikAufgaben"] ?? throw new Exception("IndexedDb not instanced");
        await GenerateNewTask();
    }

    protected override async Task OnParametersSetAsync()
    {
        var log = await ArithDb.GetItemAsync<ArithemticLogStats>("0") ?? new ArithemticLogStats();
        Logger.erfolgreich = log.RichtigProzent();
        Logger.gesamtAnzahl = log.Versuche;
        HUD.ResetAll();
        StateHasChanged();
    }

    private async Task GenerateNewTask()
    {
        var store = new SkillMasteryStore(AufgabenDB);
        var adaptiveTask = new AdaptiveTaskGenerator(store, _rng);

        _currentTaskDef = adaptiveTask.ChooseTask("math");
        var _currentTask = _currentTaskDef.Task.Generator(_rng);

        _number1 = _currentTask.a;
        _number2 = _currentTask.b;
        
        _expectedResult = _currentTaskDef.Task.Operator==ArithOperator.Addition ? _number1 + _number2 : _number1 - _number2;
        _isAddition = _currentTaskDef.Task.Operator==ArithOperator.Addition;

        _number1Digits = ExtractDigits(_number1, MaxLength);
        _number2Digits = ExtractDigits(_number2, MaxLength);

        _feedback = "";
        
        await Js.InvokeVoidAsync("elementInterop.emptyElementById", "digit-", MaxLength + 1);

        // Fokus auf erster Stelle rechts (wie bei schriftlichem Rechnen)
        _ = Task.Delay(100).ContinueWith(_ =>
        {
            for (var i = 0; i < _userDigits.Length; i++)
            {
                _userDigits[i] = null;
            }
            InvokeAsync(() => _refs[MaxLength].FocusAsync());
        });
        StateHasChanged();
    }

    private int[] ExtractDigits(int number, int length)
    {
        var digits = new int[length];
        for (int i = length - 1; i >= 0; i--)
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
                _userDigits[index] = null;
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
            {
                await Fokus(index - 1);
            }
            else
            {
                await Evaluate();
            }
        }
        else if (e.Key == "Enter")
            await Evaluate();
        // Sonst: ignorieren
    }

    private async Task Evaluate()
    {
        int userValue = 0;
        for (int i = 0; i < MaxLength + 1; i++)
        {
            userValue = userValue * 10 + (_userDigits[i] ?? 0);
        }

        var id = $"{_number1}{(_isAddition?"+":"-")}{_number2}";
        var stats = await ArithDb.GetItemAsync<ArithemticLogStats>("0") ?? new ArithemticLogStats();
        var log = await ArithDb.GetItemAsync<ArithemticLog>(id) ?? new ArithemticLog()
        {
            Id = id
        };
        
        log.Zahl1 = _number1;
        log.Zahl2 = _number2;
        log.Op = _isAddition?"+":"-";
        log.UserZahl = userValue;
        if (userValue == _expectedResult)
        {
            log.Kompetenz.AddRichtig();
            HUD.IncrementCombo();
            _feedback = $"Richtig!<br />Versuche: {log.Kompetenz.Versuche}. Richtig:{log.Kompetenz.GetProzent()}";
            Score.AddPoints(2,8);
            _currentTaskDef?.Success();
            stats.Erfolgreich++;
            stats.Versuche++;
            StateHasChanged();
            await Task.Delay(1000).ContinueWith(_ =>
            {
                InvokeAsync(GenerateNewTask);
            });

        }
        else
        {
            log.Kompetenz.AddFalsch();
            _currentTaskDef?.Fail();
            HUD.SetCombo(0);
            stats.Versuche++;
            _feedback = $"Falsch! Richtige Lösung: {_expectedResult}.<br />Versuche: {log.Kompetenz.Versuche}. Richtig:{log.Kompetenz.GetProzent()}";
            Score.AddPoints(-5,0);
            await Js.InvokeVoidAsync("elementInterop.emptyElementById", "digit-", MaxLength + 1);

            // Fokus auf erster Stelle rechts (wie bei schriftlichem Rechnen)
            _ = Task.Delay(100).ContinueWith(_ =>
            {
                for (var i = 0; i < _userDigits.Length; i++)
                {
                    _userDigits[i] = null;
                }
                InvokeAsync(() => _refs[MaxLength].FocusAsync());
            });      
        }

        await ArithDb.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());

        Logger.erfolgreich = stats.RichtigProzent();
        Logger.gesamtAnzahl = stats.Versuche;
        await ArithDb.StoreItemAsync(stats);
        StateHasChanged();
    }
}

