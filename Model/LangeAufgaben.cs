using Kidz2Learn.Model.Tasks;

namespace Kidz2Learn.Model;

/// <summary>
///     Keeps long tasks apart in a mix: after one, the next <see cref="Abstand" /> picks leave every
///     long task out. Long is what took this learner over <see cref="Grenze" /> here before (the
///     same grid can be quick for one child and a struggle for another), plus everything with a
///     <see cref="BaseTaskDefinition.FesteDauer" />. Lives only as long as one page visit.
/// </summary>
public sealed class LangeAufgaben
{
    public static readonly TimeSpan Grenze = TimeSpan.FromSeconds(30);
    public const int Abstand = 3;

    private readonly HashSet<BaseTaskDefinition> _lang = [];
    private int _seitLang = Abstand;

    public bool IstLang(BaseTaskDefinition definition)
    {
        return definition.FesteDauer || _lang.Contains(definition);
    }

    /// <summary>Whether the picker should leave <paramref name="definition" /> out right now.</summary>
    public bool Gesperrt(BaseTaskDefinition definition)
    {
        return _seitLang < Abstand && IstLang(definition);
    }

    /// <summary>Records a finished task and how long it was on screen.</summary>
    public void Erledigt(BaseTaskDefinition definition, TimeSpan dauer)
    {
        if (dauer > Grenze)
            _lang.Add(definition);
        _seitLang = IstLang(definition) ? 0 : _seitLang + 1;
    }
}
