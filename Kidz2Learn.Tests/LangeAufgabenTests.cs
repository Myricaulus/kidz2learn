using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Xunit;

namespace Kidz2Learn.Tests;

public class LangeAufgabenTests
{
    private static readonly BaseTaskDefinition Raster = LeseVerstehenTaskRegistry.SuchRaster[0];
    private static readonly BaseTaskDefinition Raetsel = LeseVerstehenTaskRegistry.Raetsel[0];
    private static readonly BaseTaskDefinition Turbo = EventTaskRegistry.All.First(d => d.FesteDauer);

    private static readonly TimeSpan Kurz = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Lang = TimeSpan.FromSeconds(60);

    [Fact]
    public void ATask_IsOnlyLong_OnceItTookLongerThanTheLimit_OrRunsForAFixedTime()
    {
        var lange = new LangeAufgaben();
        Assert.False(lange.IstLang(Raster));
        Assert.True(lange.IstLang(Turbo));

        lange.Erledigt(Raster, Kurz);
        Assert.False(lange.IstLang(Raster));

        lange.Erledigt(Raster, Lang);
        Assert.True(lange.IstLang(Raster));
    }

    [Fact]
    public void AfterALongTask_LongOnesStayLockedForThreeOthers()
    {
        var lange = new LangeAufgaben();
        Assert.False(lange.Gesperrt(Turbo));

        lange.Erledigt(Raster, Lang);
        Assert.True(lange.Gesperrt(Raster));
        Assert.True(lange.Gesperrt(Turbo));
        Assert.False(lange.Gesperrt(Raetsel));

        for (var i = 0; i < LangeAufgaben.Abstand - 1; i++)
        {
            lange.Erledigt(Raetsel, Kurz);
            Assert.True(lange.Gesperrt(Raster));
        }

        lange.Erledigt(Raetsel, Kurz);
        Assert.False(lange.Gesperrt(Raster));
    }
}
