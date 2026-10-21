using Kidz2Learn.Model.Merkmale;
using Xunit;

namespace Kidz2Learn.Tests;

public class StatistikTests
{
    private static double Kontrast(int k1, int n1, int k2, int n2) =>
        Statistik.Wert(Statistik.KontrastEvidenz(k1, n1, k2, n2, 0.15));

    private static double Rate(int k, int n) => Statistik.Wert(Statistik.RateEvidenz(k, n, 0.10, 0.03));

    [Fact]
    public void Wert_LikelihoodRatioOfThree_IsTheHintThreshold()
    {
        Assert.Equal(0.3, Statistik.Wert(Math.Log(3)), 6);
        Assert.Equal(-0.3, Statistik.Wert(-Math.Log(3)), 6);
    }

    [Fact]
    public void Kontrast_ClearDifference_IsNearPlusOne()
    {
        Assert.True(Kontrast(30, 40, 4, 40) > 0.9);
    }

    [Fact]
    public void Kontrast_ClearlyNoDifference_IsNearMinusOne()
    {
        Assert.True(Kontrast(20, 400, 20, 400) < -0.9);
    }

    [Theory]
    [InlineData(2, 5, 1, 5)]
    [InlineData(0, 5, 0, 5)]
    [InlineData(1, 5, 1, 5)]
    [InlineData(1, 5, 0, 5)]
    public void Kontrast_FewData_StaysUnclear(int k1, int n1, int k2, int n2)
    {
        Assert.InRange(Kontrast(k1, n1, k2, n2), -0.3, 0.3);
    }

    [Fact]
    public void Kontrast_NoErrorsOnEitherSide_CountsMoreAgainstWithMoreData()
    {
        var werte = new[] { 5, 10, 20, 40 }.Select(n => Kontrast(0, n, 0, n)).ToList();

        Assert.All(werte, w => Assert.True(w < 0));
        Assert.Equal(werte.OrderByDescending(w => w), werte);
    }

    [Fact]
    public void Rate_NoErrors_NeverCountsForAProblem()
    {
        for (var n = 1; n <= 60; n++)
            Assert.True(Rate(0, n) < 0, $"0 von {n}");
    }

    [Fact]
    public void Rate_NoErrorsInFewTasks_StaysUnclear()
    {
        Assert.InRange(Rate(0, 8), -0.3, 0);
    }

    [Fact]
    public void Rate_ARareErrorSeveralTimesInFewTasks_IsAClearHint()
    {
        // 12-3=11 three times in eight ten-crossing subtractions: no slip rate of 3 % explains that.
        Assert.True(Rate(3, 8) > 0.5);
    }

    [Fact]
    public void Rate_OneErrorInFewTasks_StaysUnclear()
    {
        Assert.InRange(Rate(1, 8), 0, 0.3);
    }

    [Theory]
    [InlineData(0, 5, 0, 5)]
    [InlineData(1, 18, 1, 42)]
    [InlineData(3, 10, 0, 12)]
    [InlineData(8, 23, 1, 10)]
    [InlineData(30, 40, 4, 40)]
    [InlineData(4, 20, 5, 20)]
    public void KontrastEvidenz_MatchesABruteForceSearch(int k1, int n1, int k2, int n2)
    {
        // Every pair of rates on a 0.0025 grid; the difference bounds 0.15 and 0.05 are 60 and 20 steps.
        double LogL(int k, int n, int i)
        {
            var p = i / 400.0;
            return (k > 0 ? k * Math.Log(p) : 0) + (n > k ? (n - k) * Math.Log(1 - p) : 0);
        }

        double problem = double.NegativeInfinity, keins = double.NegativeInfinity;
        for (var i1 = 0; i1 <= 400; i1++)
        for (var i2 = 0; i2 <= 400; i2++)
        {
            var l = LogL(k1, n1, i1) + LogL(k2, n2, i2);
            if (i1 - i2 >= 60)
                problem = Math.Max(problem, l);
            if (i1 - i2 <= 20)
                keins = Math.Max(keins, l);
        }

        var erwartet = problem - keins;
        Assert.InRange(Statistik.KontrastEvidenz(k1, n1, k2, n2, 0.15), erwartet - 0.02, erwartet + 0.02);
    }

    [Fact]
    public void NormalEvidenz_HalfwayBetweenTheBounds_IsNeutral()
    {
        Assert.Equal(0, Statistik.NormalEvidenz(100, 400, 150, 50), 9);
    }

    [Fact]
    public void NormalEvidenz_WithoutAnEstimate_IsNeutral()
    {
        Assert.Equal(0, Statistik.NormalEvidenz(0, double.PositiveInfinity, 150, 50));
    }

    [Fact]
    public void Steigung_FindsTheSlope()
    {
        var (slope, _) = Statistik.Steigung([(3, 1000), (5, 1400), (7, 1800), (9, 2200), (11, 2600)]);
        Assert.Equal(200, slope, 6);
    }

    [Fact]
    public void Mittel_FewValues_HaveAWiderVarianceThanTheNormal()
    {
        var werte = new[] { 1.0, 2, 3, 4, 5, 6, 7, 8 };
        var (_, var) = Statistik.Mittel(werte);

        // Sample variance 6 over 8 values, times 7/5 for the t distribution with 7 degrees of freedom.
        Assert.Equal(6.0 / 8 * 7 / 5, var, 9);
    }
}
