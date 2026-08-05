using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Microsoft.AspNetCore.Components;
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
