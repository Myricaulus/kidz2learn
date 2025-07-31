﻿using System.ComponentModel;
using System.Drawing;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Kidz2Learn.Layout;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;

namespace Kidz2Learn.Pages;

public class Kompetenzniveau
{
    private const int SIZE = 20;
    public int Versuche { get; set; } = 0;
    public string Historie { get; set; } = "--------------------";

    public void AddRichtig()
    {
        if(Historie.Length<20)
            Historie = Historie.PadRight(20, '-');
        char[] chars = Historie.ToCharArray();
        chars[Versuche++ % SIZE] = 'R'; 
        Historie = new string(chars);
    }
    public void AddFalsch()
    {
        if(Historie.Length<20)
            Historie = Historie.PadRight(20, '-');
        char[] chars = Historie.ToCharArray();
        chars[Versuche++ % SIZE] = 'F'; 
        Historie = new string(chars);
    }

    public int CountRichtig()
    {
        return Historie.Count((c) => c == 'R');
    }
    public int CountFalsch()
    {
        return Historie.Count((c) => c == 'F');
    }

    public double Verhältniss()
    {
        if (Versuche < 5)
            return 0;
        return CountRichtig() * 100.0 / Versuche;
    }

    public string GetProzent()
    {
        if (Versuche < 5)
            return "--%";
        var divisor = Versuche < SIZE ? Versuche : SIZE;
        return $"{Verhältniss():0}%";
    } 
}

public class ArithemticLog : IIdItem
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

public partial class ArithmeticChallenge : ComponentBase
{
    [CascadingParameter] public MainLayout.UpdatePointsHook UpdatePoints { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;
    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDB { get; set; } = default!;
    [Inject] private LoggerService Logger { get; set; } = default!;

    private int CurrentIndex { get; set; }
    public IndexedDbStore ArithDb { get; private set; } = default!;

    // Konfiguration
    private const int MaxLength = 1;
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
    private bool _showOkButton;
    private int _expectedResult;

    private int[] _number1Digits = new int[MaxLength];
    private int[] _number2Digits = new int[MaxLength];
    private int?[] _userDigits = new int?[MaxLength + 1];

    private ElementReference[] _refs = new ElementReference[MaxLength + 1];

    private string _feedback = "";
    private static readonly SHA256 sha;
    static ArithmeticChallenge()
    {
        sha = SHA256.Create();
    }

    protected override async Task OnInitializedAsync()
    {
        _refs = new ElementReference[MaxLength + 1];
        await GenerateNewTask();
        ArithDb = AufgabenDB["ArithmetikAufgaben"] ?? throw new Exception("IndexedDb not instanced");
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
        while (_expectedResult < 10);
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
    
    private static string GetHash(HashAlgorithm hashAlgorithm, string input)
    {

        // Convert the input string to a byte array and compute the hash.
        byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Create a new Stringbuilder to collect the bytes
        // and create a string.
        var sBuilder = new StringBuilder();

        // Loop through each byte of the hashed data
        // and format each one as a hexadecimal string.
        for (int i = 0; i < data.Length; i++)
        {
            sBuilder.Append(data[i].ToString("x2"));
        }

        // Return the hexadecimal string.
        return sBuilder.ToString();
    }

    private async Task Evaluate()
    {
        int userValue = 0;
        for (int i = 0; i < MaxLength + 1; i++)
        {
            userValue = userValue * 10 + (_userDigits[i] ?? 0);
        }

        var id = $"{_number1}+{_number2}";
        var log = await ArithDb.GetItemAsync<ArithemticLog>(id) ?? new ArithemticLog()
        {
            Id = id
        };
        log.Zahl1 = _number1;
        log.Zahl2 = _number2;
        log.Op = "+";
        log.UserZahl = userValue;
        if (userValue == _expectedResult)
        {
            log.Kompetenz.AddRichtig();
            _feedback = $"Richtig!<br />Versuche: {log.Kompetenz.Versuche}. Richtig:{log.Kompetenz.GetProzent()}";
            UpdatePoints(10);

            await Task.Delay(1000).ContinueWith(_ =>
            {
                InvokeAsync(GenerateNewTask);
            });

        }
        else
        {
            log.Kompetenz.AddFalsch();
            _feedback = $"Falsch! Richtige Lösung: {_expectedResult}.<br />Versuche: {log.Kompetenz.Versuche}. Richtig:{log.Kompetenz.GetProzent()}";
            UpdatePoints(-5);
            _showOkButton = true;
        }

        await ArithDb.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());
    }

    private async Task OnOkClickedAsync()
    {
        _showOkButton = false;
        await GenerateNewTask();
    }

}