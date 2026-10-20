namespace Kidz2Learn.Model.Tasks;

/// <summary>
///     Rules of the mixer's short Turbo round ("turbo-runde", <c>TurboRundeView</c>): as many
///     <see cref="ArithTaskRegistry.TurboAufgabe" /> tasks as fit into 20-30 seconds, scored as one
///     attempt at the end.
/// </summary>
public static class TurboRunde
{
    /// <summary>Fewer right answers than this don't count as a solved round, however few were wrong.</summary>
    public const int MindestRichtig = 3;

    /// <summary>The number the task leaves out.</summary>
    public static int Loesung((int? x, int? y, int? z, ArithOperator op) aufgabe)
    {
        var (x, y, z, op) = aufgabe;
        var plus = op == ArithOperator.Addition;
        if (z is null)
            return plus ? x!.Value + y!.Value : x!.Value - y!.Value;
        if (x is null)
            return plus ? z.Value - y!.Value : z.Value + y!.Value;
        return plus ? z.Value - x!.Value : x!.Value - z.Value;
    }

    /// <summary>At least <see cref="MindestRichtig" /> right and at most a quarter of all answers wrong.</summary>
    public static bool Geschafft(int richtig, int falsch) => richtig >= MindestRichtig && 3 * falsch <= richtig;

    public static string Sterne(int richtig, int falsch)
    {
        var gesamt = richtig + falsch;
        if (gesamt == 0)
            return string.Empty;
        var fehlerquote = (double)falsch / gesamt;
        return fehlerquote <= 0.10 ? "⭐⭐⭐" : fehlerquote <= 0.25 ? "⭐⭐" : "⭐";
    }

    public static string Text((int? x, int? y, int? z, ArithOperator op) aufgabe, int? eingabe = null)
    {
        string Zahl(int? wert) => wert?.ToString() ?? (eingabe?.ToString() ?? "_");
        return $"{Zahl(aufgabe.x)}{(aufgabe.op == ArithOperator.Addition ? "+" : "-")}{Zahl(aufgabe.y)}={Zahl(aufgabe.z)}";
    }
}
