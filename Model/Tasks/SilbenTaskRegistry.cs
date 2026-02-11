using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

public static class SilbenTaskRegistry
{
    static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        }
        return dp[a.Length, b.Length];
    }

    private static readonly List<SilbenTaskDefinition> _defs = new()
    {
        // ADDITION
        new() {
            Skills = new[]{ Skill.read_syllables },
            DifficultyLevel = 1,
            Generator = r => {
                var candidates = WordMeta.Data.ToList();
                var target = candidates[r.Next(candidates.Count)].Value.filename;
                var options = candidates.Where(c=>c.Value.filename != target).OrderBy(a=>LevenshteinDistance(a.Key,target.Replace("-",string.Empty))).Take(20).Select(o=>o.Key).ToList();
                var selected_options = options.OrderBy(o=>r.Next(options.Count)).Take(5).Concat([target.Replace("-", "")]).ToArray();
                return (target, selected_options);
            }
        },

    };

    public static IReadOnlyList<SilbenTaskDefinition> All => _defs;
}