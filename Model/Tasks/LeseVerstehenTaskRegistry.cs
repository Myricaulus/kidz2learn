using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Sentence-level reading comprehension ("Lesedetektiv"): judging whether a sentence makes
///     sense, answering W-questions about a single sentence, and "Wer bin ich?" riddles. Content
///     lives in <see cref="QuatschSaetze" /> / <see cref="WFragen" /> / <see cref="RaetselSammlung" />.
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

    private static readonly List<RaetselTaskDefinition> RaetselDefs =
    [
        new()
        {
            Skills = [Skill.ReadRiddle],
            DifficultyLevel = 1,
            View = "raetsel",
            Generator = RaetselSammlung.GenerateLeicht
        },
        new()
        {
            Skills = [Skill.ReadRiddle],
            DifficultyLevel = 2,
            View = "raetsel",
            Generator = RaetselSammlung.GenerateSchwer
        }
    ];

    public static IReadOnlyList<QuatschTaskDefinition> Quatsch => QuatschDefs;
    public static IReadOnlyList<WFrageTaskDefinition> WFrage => WFrageDefs;
    public static IReadOnlyList<RaetselTaskDefinition> Raetsel => RaetselDefs;
}
