using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class KompetenzniveauTests
{
    [Fact]
    public void GetProzent_ReturnsPlaceholder_BeforeFiveAttempts()
    {
        var k = new Kompetenzniveau();
        for (var i = 0; i < 4; i++) k.AddRichtig();

        Assert.Equal("--%", k.GetProzent());
        Assert.Equal(0.0f, k.GetProzentValue());
    }

    [Fact]
    public void GetProzent_ComputesShareOfLast20Attempts()
    {
        var k = new Kompetenzniveau();
        for (var i = 0; i < 4; i++) k.AddRichtig();
        k.AddFalsch();

        Assert.Equal("80%", k.GetProzent());
        Assert.Equal(0.8f, k.GetProzentValue());
    }

    [Fact]
    public void CountLastFalschRow_CountsConsecutiveFailuresFromTheEnd()
    {
        var k = new Kompetenzniveau();
        k.AddRichtig();
        k.AddFalsch();
        k.AddFalsch();
        k.AddFalsch();

        Assert.Equal(3, k.CountLastFalschRow());
        Assert.Equal(0, k.CountLastRichtigRow());
    }

    [Fact]
    public void CountLastRichtigRow_ResetsAfterAFailure()
    {
        var k = new Kompetenzniveau();
        k.AddFalsch();
        k.AddRichtig();
        k.AddRichtig();

        Assert.Equal(2, k.CountLastRichtigRow());
        Assert.Equal(0, k.CountLastFalschRow());
    }

    [Fact]
    public void History_WrapsAroundAfterTwentyAttempts()
    {
        var k = new Kompetenzniveau();
        for (var i = 0; i < 20; i++) k.AddRichtig();
        k.AddFalsch(); // 21st attempt overwrites slot 0

        Assert.Equal(19, k.CountRichtig());
        Assert.Equal(1, k.CountFalsch());
    }
}
