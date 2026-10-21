using Kidz2Learn.Model.Merkmale;
using Xunit;

namespace Kidz2Learn.Tests;

public class FehlerartTests
{
    [Theory]
    [InlineData("Bein", "Bein", Fehlerart.Keine)]
    [InlineData("Bein", "dein", Fehlerart.Spiegel)]
    [InlineData("Puppe", "Quppe", Fehlerart.Spiegel)]
    [InlineData("Dose", "Qose", Fehlerart.Kippung)]
    [InlineData("Bein", "Pein", Fehlerart.Mehrdeutig)]
    [InlineData("Garten", "Karten", Fehlerart.Stimmhaft)]
    [InlineData("Rad", "Rat", Fehlerart.Auslaut)]
    [InlineData("Hand", "Hund", Fehlerart.Vokal)]
    [InlineData("Eis", "Eus", Fehlerart.Diphthong)]
    [InlineData("dein", "dien", Fehlerart.Vokalfolge)]
    [InlineData("Garten", "Gatren", Fehlerart.Vertauschung)]
    [InlineData("Schule", "Chule", Fehlerart.Gruppe)]
    [InlineData("Sonne", "Sone", Fehlerart.Gruppe)]
    [InlineData("Rakete", "Rate", Fehlerart.Auslassung)]
    [InlineData("unter", "munter", Fehlerart.Einfuegung)]
    [InlineData("drunter", "darunter", Fehlerart.Einfuegung)]
    [InlineData("Nase", "Mase", Fehlerart.Formaehnlich)]
    [InlineData("Haus", "Hals", Fehlerart.AndererBuchstabe)]
    [InlineData("finster", "Fenster", Fehlerart.Vokal)]
    [InlineData("Banane", "Nabane", Fehlerart.Silbenfolge)]
    [InlineData("Garten", "Tengar", Fehlerart.Silbenfolge)]
    [InlineData("Bein", "Bin", Fehlerart.Diphthong)]
    [InlineData("Baum", "Bam", Fehlerart.Diphthong)]
    [InlineData("Ben", "Bein", Fehlerart.Diphthong)]
    [InlineData("Garten", "Gaten", Fehlerart.Auslassung)]
    [InlineData("Fernseher", "Seher", Fehlerart.Auslassung)]
    [InlineData("Hund", "Katze", Fehlerart.AnderesWort)]
    public void Bestimme_ClassifiesTheDifference(string ziel, string option, Fehlerart erwartet)
    {
        Assert.Equal(erwartet, FehlerartRechner.Bestimme(ziel, option));
    }

    [Theory]
    [InlineData("drüfen", "dürfen", true)]
    [InlineData("Hund", "Mund", true)]
    [InlineData("Hun", "Hund", true)]
    [InlineData("Hund", "Hund", false)]
    [InlineData("Vimer", "Hund", false)]
    public void IstNachbar_OneEditApart(string a, string b, bool erwartet)
    {
        Assert.Equal(erwartet, FehlerartRechner.IstNachbar(a, b));
    }
}
