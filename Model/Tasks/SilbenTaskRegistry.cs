using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class SilbenTaskRegistry
{
    /// <summary>The recorded words read_precise asks for, with the kinds of distractor each one allows.</summary>
    private static readonly Lazy<List<(string Wort, WordInfo Info, HashSet<Distraktorart> Arten)>> PraeziseWoerter =
        new(() => WordMeta.Data
            .Where(w => w.Key.Length >= 3 && w.Value.audio)
            .Select(w => (w.Key, w.Value,
                ErstleserDistraktorGenerator.Kandidaten(w.Key, w.Value.filename).Select(k => k.Art).ToHashSet()))
            .ToList());

    private static readonly List<SilbenTaskDefinition> Defs =
    [
        new()
        {
            Skills = [Skill.ReadSyllables],
            DifficultyLevel = 1,
            View = "silben-multiple-choice",
            Generator = r =>
            {
                var targetCandidates = WordMeta.Data.Where(w => w.Value.audio).ToList();
                var target = targetCandidates[r.Next(targetCandidates.Count)].Value.filename;
                var options = WordMeta.Data.Where(c => c.Value.filename != target)
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
            View = "silben-multiple-choice",
            Generator = r =>
            {
                // A kind of mistake first, then a word that allows it: every kind gets offered about
                // equally often, although only some words have a b/d or an ei.
                var arten = Enum.GetValues<Distraktorart>();
                var art = arten[r.Next(arten.Length)];
                var woerter = PraeziseWoerter.Value.Where(w => w.Arten.Contains(art)).ToList();
                if (woerter.Count == 0)
                    woerter = PraeziseWoerter.Value;
                var (wort, info, _) = woerter[r.Next(woerter.Count)];
                var selectedOptions = ErstleserDistraktorGenerator.Generate(wort, info.filename, 2, r, art)
                    .Concat([wort]).ToArray();
                return (info.filename, options: selectedOptions);
            }
        },
        new()
        {
            Skills = [Skill.GraphemPhonem],
            DifficultyLevel = 2,
            View = "silben-multiple-choice",
            Generator = r =>
            {
                var groups = WordMeta.Data
                    .SelectMany(w => w.Value.Tags
                        .Where(t => t.StartsWith("g2p-v-f|", StringComparison.OrdinalIgnoreCase))
                        .Select(t => new
                        {
                            Word = w,
                            Key = t.Split('|')[1]
                        })
                    )
                    .GroupBy(x => x.Key)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.Word).ToList()
                    );
                var mainGroup = groups.ElementAt(r.Next(0, groups.Count));
                var bla = SelectMixed(groups, mainGroup.Key, 2, r);
                var target = mainGroup.Value[r.Next(0, mainGroup.Value.Count)].Key;
                return (target, options: bla.Select(wi => wi.Key).Concat([target]).ToArray());
            }
        }
    ];

    public static IReadOnlyList<SilbenTaskDefinition> All => Defs;

    public static List<KeyValuePair<string, WordInfo>> SelectMixed(
        Dictionary<string, List<KeyValuePair<string, WordInfo>>> groups,
        string mainGroupKey,
        int additionalCount, Random r)
    {
        var result = new List<KeyValuePair<string, WordInfo>>();

        // Restliche Gruppen flatten
        var others = groups
            .Where(g => g.Key != mainGroupKey)
            .SelectMany(g => g.Value)
            .ToList();

        // optional: Shuffle für echte Randomisierung
        others = others.OrderBy(_ => r.Next()).ToList();

        // N zusätzliche
        result.AddRange(others.Take(additionalCount));

        return result;
    }

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
