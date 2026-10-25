using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class SpielzeitTests
{
    private static readonly DateOnly Tag = new(2026, 10, 25);

    [Fact]
    public void FullBar_WithoutBomb_StaysFullAndLosesTheRest()
    {
        var zeit = new Spielzeit(Tag);

        Assert.Equal(0, zeit.AddPunkte(45, Tag));

        Assert.Equal(30, zeit.Punkte);
        Assert.True(zeit.BrauchtBombe);
        Assert.Equal(0, zeit.Minuten);
    }

    [Fact]
    public void BombOnAFullBar_CreditsTenMinutes_AndTheNextFillCostsFiveMore()
    {
        var zeit = new Spielzeit(Tag);
        zeit.AddPunkte(30, Tag);

        Assert.Equal(1, zeit.BombeGeloest(Tag));

        Assert.Equal(10, zeit.Minuten);
        Assert.Equal(0, zeit.Punkte);
        Assert.Equal(35, zeit.Ziel);
        Assert.False(zeit.MauerWeg);
    }

    [Fact]
    public void BombBeforeTheBarIsFull_PaysOutOnceFull_AndTheOverflowIsLost()
    {
        var zeit = new Spielzeit(Tag);
        zeit.BombeGeloest(Tag);

        Assert.Equal(1, zeit.AddPunkte(80, Tag));

        Assert.Equal(0, zeit.Punkte);
        Assert.Equal(10, zeit.Minuten);
        Assert.True(zeit.Punkte < zeit.Ziel);
    }

    [Fact]
    public void Penalties_NeverGoBelowZero_NorTakeBackMinutes()
    {
        var zeit = new Spielzeit(Tag, new SpielzeitStand(Tag, 2, 20, 5, false));

        zeit.AddPunkte(-30, Tag);

        Assert.Equal(0, zeit.Punkte);
        Assert.Equal(20, zeit.Minuten);
        Assert.Equal(40, zeit.Ziel);
    }

    [Fact]
    public void ANewDay_StartsTheFillCountOver_ButKeepsTheMinutes()
    {
        var zeit = new Spielzeit(Tag.AddDays(1), new SpielzeitStand(Tag, 4, 15, 12, true));

        Assert.Equal(0, zeit.Fuellungen);
        Assert.Equal(30, zeit.Ziel);
        Assert.Equal(15, zeit.Minuten);
        Assert.Equal(12, zeit.Punkte);
        Assert.True(zeit.MauerWeg);
    }

    [Fact]
    public void AStoredState_RoundTripsThroughStand()
    {
        var stand = new SpielzeitStand(Tag, 3, 10, 7.5f, true);

        Assert.Equal(stand, new Spielzeit(Tag, stand).Stand);
    }

    [Fact]
    public void ABombOnAFullBar_PaysOut_AndLeavesTheBarEmpty()
    {
        // TaskSessionController adds the bomb's points first, then reports the bomb.
        var zeit = new Spielzeit(Tag);
        zeit.AddPunkte(30, Tag);

        zeit.AddPunkte(112, Tag);
        Assert.Equal(1, zeit.BombeGeloest(Tag));

        Assert.Equal(0, zeit.Punkte);
        Assert.Equal(35, zeit.Ziel);
        Assert.Equal(10, zeit.Minuten);
    }
}
