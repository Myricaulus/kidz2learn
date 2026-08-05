namespace Kidz2Learn.Pages.SilbenChallenge;

/// <summary>
/// Berechnet ein Buchstaben-Alignment (Wagner-Fischer / Levenshtein) zwischen dem
/// korrekten Wort und einer falsch gewählten Antwort, um daraus abzuleiten,
/// - welche Buchstaben im falschen Wort markiert ("falsch") werden müssen
///   (Ersetzungen + überzählige Buchstaben), und
/// - an welchen Lücken-Positionen im falschen Wort ein Buchstabe fehlt
///   (inkl. des jeweils erwarteten Buchstabens).
/// </summary>
public static class WordDiff
{
    public enum OpType
    {
        Match,
        Substitute,
        Delete,
        Insert
    }

    public readonly record struct DiffOp(OpType Type, char? CorrectChar, char? WrongChar);

    public readonly record struct DiffRequirements(
        HashSet<int> RequiredMarks,
        Dictionary<int, char> RequiredSubstitutions,
        Dictionary<int, char> RequiredGaps);

    /// <summary>
    /// Liefert die Alignment-Operationen zwischen <paramref name="correct"/> und
    /// <paramref name="wrong"/>, in Reihenfolge des falschen Wortes.
    /// </summary>
    public static List<DiffOp> Align(string correct, string wrong)
    {
        var c = correct;
        var w = wrong;
        var n = c.Length;
        var m = w.Length;

        var dp = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) dp[i, 0] = i;
        for (var j = 0; j <= m; j++) dp[0, j] = j;

        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            if (char.ToLowerInvariant(c[i - 1]) == char.ToLowerInvariant(w[j - 1]))
            {
                dp[i, j] = dp[i - 1, j - 1];
            }
            else
            {
                var sub = dp[i - 1, j - 1] + 1;
                var del = dp[i - 1, j] + 1; // Buchstabe aus "correct" fehlt in "wrong" -> Insert-Op
                var ins = dp[i, j - 1] + 1; // Buchstabe in "wrong" ist überzählig -> Delete-Op
                dp[i, j] = Math.Min(sub, Math.Min(del, ins));
            }
        }

        // Backtracking vom Ende her, danach umdrehen
        var ops = new List<DiffOp>();
        var a = n;
        var b = m;
        while (a > 0 || b > 0)
        {
            if (a > 0 && b > 0 && char.ToLowerInvariant(c[a - 1]) == char.ToLowerInvariant(w[b - 1]))
            {
                ops.Add(new DiffOp(OpType.Match, c[a - 1], w[b - 1]));
                a--;
                b--;
            }
            else if (a > 0 && b > 0 && dp[a, b] == dp[a - 1, b - 1] + 1)
            {
                ops.Add(new DiffOp(OpType.Substitute, c[a - 1], w[b - 1]));
                a--;
                b--;
            }
            else if (b > 0 && dp[a, b] == dp[a, b - 1] + 1)
            {
                // Buchstabe existiert in "wrong", aber nicht im korrekten Wort -> überzählig
                ops.Add(new DiffOp(OpType.Delete, null, w[b - 1]));
                b--;
            }
            else
            {
                // Buchstabe existiert im korrekten Wort, aber nicht in "wrong" -> fehlt (Lücke)
                ops.Add(new DiffOp(OpType.Insert, c[a - 1], null));
                a--;
            }
        }

        ops.Reverse();
        return ops;
    }

    /// <summary>
    /// Wandelt die Alignment-Operationen in die für das Popup benötigten
    /// "Musterlösungen" um:
    /// - RequiredMarks: Indizes überzähliger Buchstaben im falschen Wort (nur markieren, nichts einzutippen).
    /// - RequiredSubstitutions: Indizes von inhaltlich falschen Buchstaben, mit dem jeweils
    ///   korrekten Ersatzbuchstaben, der dort eingetippt werden muss.
    /// - RequiredGaps: Lücken-Positionen mit fehlendem Buchstaben (wie gehabt).
    /// Lücken-Index k bedeutet "vor dem k-ten Buchstaben des falschen Wortes"
    /// (k == Länge des falschen Wortes => Lücke ganz am Ende).
    /// </summary>
    public static DiffRequirements BuildRequirements(string correct, string wrong)
    {
        var ops = Align(correct, wrong);

        var marks = new HashSet<int>();
        var substitutions = new Dictionary<int, char>();
        var gaps = new Dictionary<int, char>();
        var wrongIndex = 0;

        foreach (var op in ops)
            switch (op.Type)
            {
                case OpType.Match:
                    wrongIndex++;
                    break;
                case OpType.Substitute:
                    substitutions[wrongIndex] = op.CorrectChar!.Value;
                    wrongIndex++;
                    break;
                case OpType.Delete:
                    marks.Add(wrongIndex);
                    wrongIndex++;
                    break;
                case OpType.Insert:
                    gaps[wrongIndex] = op.CorrectChar!.Value;
                    break;
            }

        return new DiffRequirements(marks, substitutions, gaps);
    }

    /// <summary>
    /// Reconstructs the word that results from applying marks (delete), substitutions (replace)
    /// and inserted gap letters to <paramref name="wrong"/>, in left-to-right, gap-before-letter
    /// order (gap index k sits "before the k-th letter", k == wrong.Length is the trailing gap).
    /// Used to validate a popup answer by outcome instead of by exact-matching one specific
    /// <see cref="BuildRequirements"/> alignment - words with repeated letters often have several
    /// equally valid ways to mark/correct them, see TECH_DEBT.md #12.
    /// </summary>
    public static string Apply(
        string wrong,
        IReadOnlySet<int> marks,
        IReadOnlyDictionary<int, char> substitutions,
        IReadOnlyDictionary<int, char> gaps)
    {
        var result = new System.Text.StringBuilder();
        for (var i = 0; i <= wrong.Length; i++)
        {
            if (gaps.TryGetValue(i, out var inserted))
                result.Append(inserted);

            if (i >= wrong.Length)
                continue;

            if (marks.Contains(i))
                continue; // marked as extra - dropped, not carried into the result

            result.Append(substitutions.TryGetValue(i, out var corrected) ? corrected : wrong[i]);
        }

        return result.ToString();
    }
}
