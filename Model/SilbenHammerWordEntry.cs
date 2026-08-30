using System.Text.Json.Serialization;

namespace Kidz2Learn.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WordTier
{
    Grundschule,
    Schlaukopf
}

/// <summary>
///     One entry of the Silbenhammer word catalog (wwwroot/data/silben-hammer-words.json),
///     generated offline by WaveSplit/GenerateSilbenHammerWords.py from an open German
///     word-frequency list, hyphenated via pyphen. Deliberately not audio-backed (unlike
///     <see cref="WordInfo" />/WordMeta.g.cs) - Silbenhammer is read aloud by the child, not
///     played back.
/// </summary>
public sealed record SilbenHammerWordEntry(string Word, string[] Syllables, WordTier Tier)
{
    /// <summary>Session-local identity for "already used this burst" tracking.</summary>
    public string Key => Word;
}
