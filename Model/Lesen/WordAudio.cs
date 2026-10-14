namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     Resolves a word as it appears in running text (any capitalization, trailing punctuation) to
///     its recorded clip under <c>wwwroot/audio/</c>, via <see cref="WordMeta.Data" />. Returns
///     <c>null</c> for words that were never recorded, so callers can treat "has audio" as optional.
/// </summary>
public static class WordAudio
{
    private static readonly char[] Punctuation = ['.', ',', '!', '?', ':', ';', '"', '„', '“'];

    public static string? PathFor(string token)
    {
        var word = token.Trim().Trim(Punctuation);
        if (word.Length == 0)
            return null;

        // Sentence-initial articles ("Der") and lowercase nouns both need the other casing to hit
        // the WordMeta key ("der", "Hund").
        var flipped = char.IsUpper(word[0])
            ? char.ToLowerInvariant(word[0]) + word[1..]
            : char.ToUpperInvariant(word[0]) + word[1..];

        return Lookup(word) ?? Lookup(flipped);
    }

    private static string? Lookup(string word)
    {
        return WordMeta.Data.TryGetValue(word, out var info) && info.audio
            ? $"audio/{info.filename}.opus"
            : null;
    }
}
