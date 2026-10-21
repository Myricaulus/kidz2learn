namespace Kidz2Learn.Model.Merkmale;

/// <summary>Lower bound for reading a text, for <see cref="Entities.BeobachtungsDaten.MindestLeseMs" />.</summary>
public static class Lesezeit
{
    /// <summary>A fluent adult reads about 250 ms per word; a child can't be done faster than that.</summary>
    public const int MsProWort = 250;

    public static int Mindest(params string[] texte) =>
        MsProWort * texte.Sum(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
}
