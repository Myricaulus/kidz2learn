using Kidz2Learn.Layout;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Pages;

public partial class ArithmeticChallenge : ComponentBase
{
    [CascadingParameter]public MainLayout.UpdatePointsHook UpdatePoints { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;
    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDB { get; set; } = default!;

    private int CurrentIndex { get; set; }

    // Konfiguration
    private const int MaxLength = 1;
    private readonly Random _rng = new ();
    
    private readonly List<Dictionary<string,string>> _numpadLayout = 
    [
        new() {{"7","7"}, {"8","8"}, {"9","9"}},
        new() {{"4","4"}, {"5","5"}, {"6","6"}},
        new() {{"1","1"}, {"2","2"}, {"3","3"}},
        new() {{"0","0"}, { "➡️","Backspace" }, {"↩️","Enter"}}
    ];

    private int _number1;
    private int _number2;
    private bool _isAddition;
    private bool _showOkButton;
    private int _expectedResult;

    private int[] _number1Digits = new int[MaxLength];
    private int[] _number2Digits = new int[MaxLength];
    private int?[] _userDigits = new int?[MaxLength+1];

    private ElementReference[] _refs = new ElementReference[MaxLength+1];

    private string _feedback = "";

    protected override async Task OnInitializedAsync()
    {
        _refs = new ElementReference[MaxLength+1];
        await GenerateNewTask();
    }

    private async Task GenerateNewTask()
    {
        _isAddition = true; //_rng.Next(2) == 0;
        do
        {
            _number1 = _rng.Next(1, (int)Math.Pow(10, MaxLength));
            _number2 = _rng.Next(1, (int)Math.Pow(10, MaxLength));

            if (!_isAddition)
            {
                // Keine negativen Ergebnisse
                if (_number2 > _number1)
                {
                    (_number1, _number2) = (_number2, _number1);
                }
            }

            _expectedResult = _isAddition ? _number1 + _number2 : _number1 - _number2;
        }
        while (_expectedResult <10 ) ;
        _number1Digits = ExtractDigits(_number1, MaxLength);
        _number2Digits = ExtractDigits(_number2, MaxLength);

        _feedback = "";
        await Js.InvokeVoidAsync("elementInterop.emptyElementById", "digit-",MaxLength+1);

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
                Evaluate();
            }
        } else if(e.Key == "Enter")
            Evaluate();
        // Sonst: ignorieren
    }

    private void Evaluate()
    {
        int userValue = 0;
        for (int i = 0; i < MaxLength + 1; i++)
        {
            userValue = userValue * 10 + (_userDigits[i] ?? 0);
        }

        if (userValue == _expectedResult)
        {
            _feedback = "Richtig!";
            UpdatePoints(10);

            Task.Delay(1000).ContinueWith(_ =>
            {
                InvokeAsync(GenerateNewTask);
            });
        }
        else
        {
            _feedback = $"Falsch! Richtige Lösung: {_expectedResult}";
            UpdatePoints(-5);
            _showOkButton = true;
        }
    }

    private async Task OnOkClickedAsync()
    {
        _showOkButton = false;
        await GenerateNewTask();
    }

}