using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Microsoft.AspNetCore.Components;
using Tavenem.DataStorage;

public class ArithemticLog : IIdItem
{
    [JsonIgnore] public int? Zahl1 { get; set; }

    [JsonIgnore] public string Op { get; set; } = string.Empty;

    [JsonIgnore] public int? Zahl2 { get; set; }

    [JsonIgnore] public int? Zahl3 { get; set; }

    [JsonIgnore] public int UserZahl { get; set; }

    public Kompetenzniveau Kompetenz { get; set; } = new();

    [JsonIgnore] public bool Richtig { get; set; }

    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }


    public RenderFragment ToRenderFragment()
    {
        return builder =>
        {
            var i = 0;
            var color = Richtig ? "#2e7d32" : "#c62828"; // angenehmes grün / rot
            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "class", "log-entry arithmetik-log");

            void RenderValue(int? value)
            {
                if (value.HasValue)
                {
                    builder.AddContent(i++, value.Value);
                }
                else
                {
                    builder.OpenElement(i++, "span");
                    builder.AddAttribute(i++, "style", $"color:{color}; font-weight:600;");
                    builder.AddContent(i++, UserZahl);
                    builder.CloseElement();
                }
            }

            RenderValue(Zahl1);
            builder.AddContent(i++, $" {Op} ");
            RenderValue(Zahl2);
            builder.AddContent(i++, " = ");
            RenderValue(Zahl3);
            builder.AddContent(i++, $"  R:{Kompetenz.GetProzent()}");
            builder.CloseElement(); // div
        };
    }
}

internal class ArithemticLogStats : IIdItem
{
    public int Versuche { get; set; }
    public int Erfolgreich { get; set; }

    [JsonPropertyName("id")] public string Id { get; set; } = "0";

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }


    public float RichtigProzent()
    {
        return Versuche == 0 ? 0 : (float)Erfolgreich / Versuche;
    }
}