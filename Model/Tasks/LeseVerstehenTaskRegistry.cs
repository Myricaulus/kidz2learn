using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks.TaskDefs;

namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Sentence-level reading comprehension ("Lesedetektiv"): judging whether a sentence makes
///     sense, answering W-questions about a single sentence, "Wer bin ich?" riddles, and search
///     grids (nouns among words, one mirror letter among b/d/p/q). Content lives in
///     <see cref="QuatschSaetze" /> / <see cref="WFragen" /> / <see cref="RaetselSammlung" /> /
///     <see cref="NomenSammlung" /> / <see cref="Spiegelbuchstaben" />.
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
        },
        Rechnen(Rechenart.Plus, 2, Rechengeschichten.GenerateRechnenLeicht),
        Rechnen(Rechenart.Minus, 2, Rechengeschichten.GenerateRechnenLeicht),
        Rechnen(Rechenart.Plus, 3, Rechengeschichten.GenerateRechnen),
        Rechnen(Rechenart.Minus, 3, Rechengeschichten.GenerateRechnen),
        new()
        {
            Skills = [Skill.ReadStoryMath, Skill.ReadWhQuestion],
            SkillWeights = new Dictionary<string, float> { [Skill.ReadWhQuestion] = 0.5f },
            DifficultyLevel = 2,
            View = "w-frage",
            Generator = Rechengeschichten.GenerateAblesen
        }
    ];

    /// <summary>
    ///     A word problem to calculate trains <see cref="Skill.ReadStoryMath" /> fully, and reading the
    ///     question and the arithmetic along the way at half weight each.
    /// </summary>
    private static WFrageTaskDefinition Rechnen(Rechenart art, int level, Func<Rechenart, Random, WFrageSerie> generate)
    {
        var rechnen = art == Rechenart.Plus ? Skill.Math.Add10NoCarry : Skill.Math.Sub10;
        return new WFrageTaskDefinition
        {
            Skills = [Skill.ReadStoryMath, Skill.ReadWhQuestion, rechnen],
            SkillWeights = new Dictionary<string, float> { [Skill.ReadWhQuestion] = 0.5f, [rechnen] = 0.5f },
            DifficultyLevel = level,
            View = "w-frage",
            Generator = rng => generate(art, rng)
        };
    }

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

    private static readonly List<SuchRasterTaskDefinition> SuchRasterDefs =
    [
        new()
        {
            Skills = [Skill.ReadNouns],
            DifficultyLevel = 1,
            View = "such-raster",
            Generator = NomenSammlung.GenerateLeicht
        },
        new()
        {
            Skills = [Skill.ReadNouns],
            DifficultyLevel = 2,
            View = "such-raster",
            Generator = NomenSammlung.GenerateMittel
        },
        new()
        {
            Skills = [Skill.ReadNouns],
            DifficultyLevel = 3,
            View = "such-raster",
            Generator = NomenSammlung.GenerateSchwer
        },
        new()
        {
            Skills = [Skill.ReadMirrorLetters],
            DifficultyLevel = 2,
            View = "such-raster",
            Generator = Spiegelbuchstaben.GenerateMittel
        },
        new()
        {
            Skills = [Skill.ReadMirrorLetters],
            DifficultyLevel = 3,
            View = "such-raster",
            Generator = Spiegelbuchstaben.GenerateSchwer
        }
    ];

    public static IReadOnlyList<QuatschTaskDefinition> Quatsch => QuatschDefs;
    public static IReadOnlyList<WFrageTaskDefinition> WFrage => WFrageDefs;
    public static IReadOnlyList<RaetselTaskDefinition> Raetsel => RaetselDefs;
    public static IReadOnlyList<SuchRasterTaskDefinition> SuchRaster => SuchRasterDefs;
}
