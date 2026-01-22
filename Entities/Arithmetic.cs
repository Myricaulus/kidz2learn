using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Microsoft.AspNetCore.Components;
using Tavenem.DataStorage;

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
            builder.AddAttribute(i++, "style", $"color: {(UserZahl == (Op=="+"?Zahl1+Zahl2:Zahl1-Zahl2) ? "green" : "red")}");
            builder.AddContent(i++, UserZahl);
            builder.CloseElement(); // </span>
            builder.AddContent(i++, $" ({Zahl1+Zahl2}) R:{Kompetenz.GetProzent()}");
            builder.CloseElement(); // </div>
        };
}

internal class ArithemticLogStats : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "0";
    public int Versuche {get;set;}
    public int Erfolgreich {get;set;}

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }


    public float RichtigProzent()
    {
        return Versuche == 0 ? 0 : (float)Erfolgreich / Versuche;
    }
}