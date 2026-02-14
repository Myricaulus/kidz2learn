using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class SilbenTaskRegistry
{
    private static readonly List<SilbenTaskDefinition> Defs =
    [
        new()
        {
            Skills = [Skill.ReadSyllables],
            DifficultyLevel = 1,
            Generator = r =>
            {
                var candidates = WordMeta.Data.ToList();
                var target = candidates[r.Next(candidates.Count)].Value.filename;
                var options = candidates.Where(c => c.Value.filename != target)
                    .OrderBy(a => LevenshteinDistance(a.Key, target.Replace("-", string.Empty))).Take(20)
                    .Select(o => o.Key).ToList();
                var selectedOptions = options.OrderBy(o => r.Next(options.Count)).Take(5)
                    .Concat([target.Replace("-", "")]).ToArray();
                return (target, options: selectedOptions);
            }
        },
        new()
        {
            Skills = [Skill.ReadPrecise],
            DifficultyLevel = 2,
            Generator = r =>
            {
                var candidates = WordMeta.Data.Where(w => w.Key.Length >= 3).ToList();
                var target = candidates[r.Next(candidates.Count)];
                var selectedOptions = ErstleserDistraktorGenerator.Generate(target.Key, 5, r).Concat([target.Key])
                    .ToArray();
                return (target.Value.filename, options: selectedOptions);
            }
        }
    ];

    public static IReadOnlyList<SilbenTaskDefinition> All => Defs;

    private static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
        }

        return dp[a.Length, b.Length];
    }
}

public static class ErstleserDistraktorGenerator
{
    private static readonly Dictionary<char, char[]> VisualConfusions = new()
    {
        ['b'] = ['d', 'p'],
        ['d'] = ['b', 't'],
        ['p'] = ['b'],
        ['n'] = ['m', 'r'],
        ['m'] = ['n'],
        ['f'] = ['t', 'v'],
        ['v'] = ['f'],
        ['u'] = ['n']
    };

    private static readonly Dictionary<char, char[]> PhoneticConfusions = new()
    {
        ['g'] = ['k'],
        ['k'] = ['g'],
        ['d'] = ['t'],
        ['t'] = ['d'],
        ['b'] = ['p'],
        ['p'] = ['b'],
        ['s'] = ['z'],
        ['z'] = ['s']
    };

    private static readonly Dictionary<char, char[]> VowelConfusions = new()
    {
        ['a'] = ['e'],
        ['e'] = ['i', 'a'],
        ['i'] = ['e'],
        ['o'] = ['u'],
        ['u'] = ['o']
    };

    private static readonly Dictionary<string, string[]> ClusterConfusions = new()
    {
        ["pf"] = ["f"],
        ["sch"] = ["ch", "s"],
        ["ch"] = ["h"],
        ["ck"] = ["k"],
        ["ss"] = ["s"],
        ["mm"] = ["m"],
        ["ll"] = ["l"],
        ["nn"] = ["n"],
        ["tz"] = ["z"]
    };

    public static List<string> Generate(string word, int count, Random r)
    {
        var isCapital = word[0] >= 'A' && word[0] <= 'Z';
        word = word.ToLowerInvariant();

        var results = new HashSet<string>();

        // Positionen außer erstes und letztes Zeichen
        for (var i = 0; i < word.Length; i++)
        {
            var current = word[i];

            foreach (var replacement in GetReplacements(current))
            {
                var chars = word.ToCharArray();
                chars[i] = replacement;

                results.Add(Capitalize(new string(chars), isCapital));
            }
        }

        // Optional: Buchstabentausch (Swap)
        for (var i = 1; i < word.Length - 2; i++)
        {
            var chars = word.ToCharArray();
            (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
            results.Add(Capitalize(new string(chars), isCapital));
        }

        foreach (var newWord in GenerateClusterVariants(word)) results.Add(Capitalize(newWord, isCapital));

        // Original nicht drin haben
        results.Remove(Capitalize(word, isCapital));
        Console.WriteLine($"Found Distractions {results.Count}");
        return results
            .OrderBy(_ => r.Next())
            .Take(count)
            .ToList();
    }

    private static IEnumerable<string> GenerateClusterVariants(string word)
    {
        foreach (var cluster in ClusterConfusions.Keys.OrderByDescending(k => k.Length))
        {
            var index = word.IndexOf(cluster, StringComparison.Ordinal);

            while (index >= 0)
            {
                // Optional: ersten/letzten Buchstaben schützen
                if (index > 0 && index + cluster.Length < word.Length)
                    foreach (var replacement in ClusterConfusions[cluster])
                    {
                        var variant =
                            string.Concat(word.AsSpan(0, index), replacement, word.AsSpan(index + cluster.Length));

                        yield return variant;
                    }

                index = word.IndexOf(cluster, index + 1, StringComparison.Ordinal);
            }
        }

        // Doppelkonsonanten-Erweiterung (z.B. m → mm)
        for (var i = 1; i < word.Length - 1; i++)
        {
            var c = word[i];

            if ("mnsl".Contains(c))
            {
                var chars = word.ToCharArray();
                var doubled =
                    word[..i] +
                    c + c +
                    word[(i + 1)..];

                yield return doubled;
            }
        }
    }

    private static string Capitalize(string word, bool isCapital)
    {
        if (isCapital)
            return char.ToUpper(word[0]) + word[1..];
        return word;
    }

    private static IEnumerable<char> GetReplacements(char c)
    {
        if (VisualConfusions.TryGetValue(c, out var visual))
            foreach (var v in visual)
                yield return v;

        if (PhoneticConfusions.TryGetValue(c, out var phon))
            foreach (var p in phon)
                yield return p;

        if (VowelConfusions.TryGetValue(c, out var vowel))
            foreach (var v in vowel)
                yield return v;
    }
}