using System.Text.Json.Serialization;
using Tavenem.DataStorage;

namespace Kidz2Learn.Entities;

/// <summary>
///     One item inside an attempt that covers several (a Lese-Turbo word, a wrongly handled
///     search-grid tile).
/// </summary>
/// <param name="Echt">Lese-Turbo: true for a real word, false for a nonsense word.</param>
/// <param name="Ergebnis">
///     "richtig", "falsch" or "zeit" (left too long) for Lese-Turbo words; "falsch-markiert" or
///     "vergessen" for search-grid tiles.
/// </param>
/// <param name="Ms">Time the item was on screen before the answer, 0 where not measured.</param>
/// <param name="Stelle">Search-grid tiles: index in the grid, row by row. Null where not recorded.</param>
public sealed record BeobachtungsItem(string Text, bool? Echt, string Ergebnis, int Ms, int? Stelle = null);

/// <summary>
///     The task-specific part of a <see cref="Beobachtung" />, filled in by the view that asked the
///     question; <c>TaskSessionController</c> adds the common part.
/// </summary>
/// <param name="MindestLeseMs">
///     Shortest time in which the text needed for the answer can have been read at all; a wrong
///     answer faster than this is a guess (<c>Model/Merkmale</c>). Null where nothing has to be read.
/// </param>
/// <param name="Werte">Task-specific values, e.g. <c>frage=Wo</c> or <c>a=12, b=3, op=-</c>.</param>
public sealed record BeobachtungsDaten(
    string? Ziel = null,
    string? Antwort = null,
    IReadOnlyList<string>? Optionen = null,
    int? MindestLeseMs = null,
    IReadOnlyDictionary<string, string>? Werte = null,
    IReadOnlyList<BeobachtungsItem>? Items = null);

/// <summary>
///     One answered attempt in IndexedDB store "Beobachtungen", the raw material of the reading
///     profile (<c>Model/Merkmale</c>, LESE_MERKMALE.md). Kept unlimited: at 50-100 attempts a day
///     that's 15-20 MB a year.
/// </summary>
public sealed class Beobachtung : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    public DateTime Zeit { get; set; }
    public string View { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public int Level { get; set; }

    /// <summary>1 for the first answer to a question, 2 for the next try at the same one, ...</summary>
    public int Versuch { get; set; }

    public bool Richtig { get; set; }

    /// <summary>Time since the question was shown, so it keeps growing across retries.</summary>
    public int DauerMs { get; set; }

    /// <summary>How often the child replayed the target audio before this answer.</summary>
    public int Vorgespielt { get; set; }

    /// <summary>Words the child tapped to hear them (<c>LeseSatz</c>) before this answer.</summary>
    public List<string> Angetippt { get; set; } = [];

    public string? Ziel { get; set; }
    public string? Antwort { get; set; }
    public List<string>? Optionen { get; set; }
    public int? MindestLeseMs { get; set; }
    public Dictionary<string, string>? Werte { get; set; }
    public List<BeobachtungsItem>? Items { get; set; }

    public string? Wert(string key) => Werte?.GetValueOrDefault(key);

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }
}

/// <summary>One feature's state in a <see cref="MerkmalSchnappschuss" />.</summary>
public sealed record MerkmalStand(string Id, double? Wert, string Status, int N);

/// <summary>
///     The reading profile as of one ISO week, in IndexedDB store "MerkmalVerlauf" (Id like
///     "2026-W43", overwritten until the week is over) - the history that shows whether a
///     feature gets better or turns out wrong.
/// </summary>
public sealed class MerkmalSchnappschuss : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    public DateTime Zeit { get; set; }
    public List<MerkmalStand> Merkmale { get; set; } = [];

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }
}
