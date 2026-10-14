using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Sentence-level reading comprehension ("Lesedetektiv"): judging whether a sentence makes
///     sense, and answering W-questions about a single sentence. Content lives in
///     <see cref="QuatschSaetze" /> / <see cref="WFragen" />.
/// </summary>
public static class LeseVerstehenTaskRegistry
{
    private static readonly List<QuatschTaskDefinition> QuatschDefs =
    [
        new()
        {
            Skills = [Skill.ReadSentenceTruth],
            DifficultyLevel = 1,
            View = "quatsch-detektor",
            Generator = QuatschSaetze.GenerateLeicht
        },
        new()
        {
            Skills = [Skill.ReadSentenceTruth],
            DifficultyLevel = 2,
            View = "quatsch-detektor",
            Generator = QuatschSaetze.GenerateSchwer
        }
    ];

    private static readonly List<WFrageTaskDefinition> WFrageDefs =
    [
        new()
        {
            Skills = [Skill.ReadWhQuestion],
            DifficultyLevel = 1,
            View = "w-frage",
            Generator = WFragen.GenerateLeicht
        },
        new()
        {
            Skills = [Skill.ReadWhQuestion],
            DifficultyLevel = 2,
            View = "w-frage",
            Generator = WFragen.GenerateSchwer
        }
    ];

    public static IReadOnlyList<QuatschTaskDefinition> Quatsch => QuatschDefs;
    public static IReadOnlyList<WFrageTaskDefinition> WFrage => WFrageDefs;
}
