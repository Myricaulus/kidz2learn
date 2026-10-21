namespace Kidz2Learn.Model.Merkmale;

/// <summary>
///     The estimates behind the reading profile. Every source asks whether its observations fit "a
///     problem" (the effect at least <c>hoch</c>) better than "none" (the effect at most <c>tief</c>).
///     Its evidence is the log of the generalized likelihood ratio - each side at the value that
///     fits it best - so no prior decides: a few observations fit both sides about equally and stay
///     near 0, and zero errors can only ever count against a problem. <see cref="Wert" /> maps the
///     evidence to -1..+1.
/// </summary>
public static class Statistik
{
    /// <summary>
    ///     <see cref="Wert" /> = tanh(evidence / Skala): a likelihood ratio of 3 gives ±0.3, 7 gives
    ///     ±0.5, 100 gives ±0.86. Plain posterior odds would use 2 (ratio 3 = ±0.5); the larger scale
    ///     discounts the evidence because answers within a session aren't independent.
    /// </summary>
    public static readonly double Skala = Math.Log(3) / Math.Atanh(0.3);

    public static double Wert(double evidenz) => Math.Tanh(evidenz / Skala);

    /// <summary>A rate: a problem from <paramref name="hoch" />, clearly none up to <paramref name="tief" />.</summary>
    public static double RateEvidenz(int k, int n, double hoch, double tief)
    {
        var p = (double)k / n;
        return LogL(k, n, Math.Max(p, hoch)) - LogL(k, n, Math.Min(p, tief));
    }

    /// <summary>
    ///     Two rates: a problem when rate 1 is at least <paramref name="delta" /> above rate 2,
    ///     clearly none when the difference stays at most a third of it.
    /// </summary>
    public static double KontrastEvidenz(int k1, int n1, int k2, int n2, double delta)
    {
        double p1 = (double)k1 / n1, p2 = (double)k2 / n2;
        var frei = LogL(k1, n1, p1) + LogL(k2, n2, p2);
        var problem = p1 - p2 >= delta ? frei : MitAbstand(k1, n1, k2, n2, delta);
        var keins = p1 - p2 <= delta / 3 ? frei : MitAbstand(k1, n1, k2, n2, delta / 3);
        return problem - keins;
    }

    /// <summary>A normally distributed estimate (a mean or a slope with its variance) against the same kind of bounds.</summary>
    public static double NormalEvidenz(double mean, double var, double hoch, double tief)
    {
        var unter = Math.Max(0, hoch - mean);
        var ueber = Math.Max(0, mean - tief);
        return (ueber * ueber - unter * unter) / (2 * Math.Max(var, 1e-12));
    }

    /// <summary>
    ///     Mean and the variance of the mean, widened to that of a t distribution with n-1 degrees of
    ///     freedom, so a few values don't look more certain than they are.
    /// </summary>
    public static (double Mean, double Var) Mittel(IReadOnlyCollection<double> werte)
    {
        var n = werte.Count;
        var mean = werte.Average();
        if (n < 4)
            return (mean, double.PositiveInfinity);
        var var = werte.Sum(w => (w - mean) * (w - mean)) / (n - 1);
        return (mean, var / n * (n - 1) / (n - 3));
    }

    /// <summary>Least-squares slope of y over x and its variance, widened like <see cref="Mittel" /> (n-2 degrees of freedom).</summary>
    public static (double Slope, double Var) Steigung(IReadOnlyList<(double X, double Y)> punkte)
    {
        var n = punkte.Count;
        if (n < 5)
            return (0, double.PositiveInfinity);
        var mx = punkte.Average(p => p.X);
        var my = punkte.Average(p => p.Y);
        var sxx = punkte.Sum(p => (p.X - mx) * (p.X - mx));
        if (sxx <= 0)
            return (0, double.PositiveInfinity);
        var slope = punkte.Sum(p => (p.X - mx) * (p.Y - my)) / sxx;
        var rest = punkte.Sum(p => Math.Pow(p.Y - my - slope * (p.X - mx), 2)) / (n - 2);
        return (slope, rest / sxx * (n - 2) / (n - 4));
    }

    /// <summary>Binomial log-likelihood of <paramref name="k" /> hits in <paramref name="n" /> at rate <paramref name="p" />, up to a constant.</summary>
    private static double LogL(int k, int n, double p)
    {
        p = Math.Clamp(p, 0, 1);
        return (k > 0 ? k * Math.Log(p) : 0) + (n > k ? (n - k) * Math.Log(1 - p) : 0);
    }

    /// <summary>The best fit of both rates with rate 1 exactly <paramref name="abstand" /> above rate 2.</summary>
    private static double MitAbstand(int k1, int n1, int k2, int n2, double abstand)
    {
        double F(double p2) => LogL(k1, n1, p2 + abstand) + LogL(k2, n2, p2);

        // F is concave in p2, so a golden-section search finds its maximum.
        double a = Math.Max(0, -abstand), b = Math.Min(1, 1 - abstand);
        var g = (Math.Sqrt(5) - 1) / 2;
        double x1 = b - g * (b - a), x2 = a + g * (b - a);
        double f1 = F(x1), f2 = F(x2);
        for (var i = 0; i < 100; i++)
            if (f1 < f2)
            {
                (a, x1, f1) = (x1, x2, f2);
                x2 = a + g * (b - a);
                f2 = F(x2);
            }
            else
            {
                (b, x2, f2) = (x2, x1, f1);
                x1 = b - g * (b - a);
                f1 = F(x1);
            }

        return Math.Max(Math.Max(F(a), F(b)), Math.Max(f1, f2));
    }
}
