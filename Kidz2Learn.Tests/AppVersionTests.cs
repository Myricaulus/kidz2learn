using Kidz2Learn.Shared;
using Xunit;

namespace Kidz2Learn.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("1.0.0+3ad36b8a08a472e46863decb112963b99bce5e08", "2026-10-21", "Version 1.0.0 (3ad36b8) vom 21.10.2026")]
    [InlineData("1.2.0", "2026-10-21", "Version 1.2.0 vom 21.10.2026")]
    [InlineData("1.0.0+abc", null, "Version 1.0.0 (abc)")]
    [InlineData(null, "kaputt", "Version ?")]
    public void Format_LeavesOutWhatIsUnknown(string? informationalVersion, string? buildDate, string erwartet)
    {
        Assert.Equal(erwartet, AppVersion.Format(informationalVersion, buildDate));
    }

    [Fact]
    public void Text_ComesFromTheBuild()
    {
        Assert.Matches(@"^Version \d+\.\d+\.\d+ (\([0-9a-f]{7}\) )?vom \d\d\.\d\d\.\d{4}$", AppVersion.Text);
    }
}
