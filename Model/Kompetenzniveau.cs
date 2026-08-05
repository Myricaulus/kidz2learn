using System.Text.Json.Serialization;

namespace Kidz2Learn.Model;

public class Kompetenzniveau
{
    private const int Size = 20;

    // System.Text.Json's default reflection deserializer only writes properties with a public
    // setter - without [JsonInclude], these two silently stayed at 0 after every IndexedDB
    // round-trip (only Historie, with its public setter, survived), even though serialization
    // itself included the real values. Since callers reload the entity fresh on every attempt,
    // Versuche/Richtig never accumulated, and GetProzent()'s "at least 5 attempts" threshold was
    // never reached. See TECH_DEBT.md #10.
    [JsonInclude] public int Versuche { get; private set; }
    [JsonInclude] public int Richtig { get; private set; }
    public string Historie { get; set; } = "--------------------";

    public void AddRichtig()
    {
        if (Historie.Length < Size)
            Historie = Historie.PadRight(Size, '-');
        var chars = Historie.ToCharArray();
        chars[Versuche++ % Size] = 'R';
        Historie = new string(chars);
        ++Richtig;
    }

    public void AddFalsch()
    {
        if (Historie.Length < Size)
            Historie = Historie.PadRight(Size, '-');
        var chars = Historie.ToCharArray();
        chars[Versuche++ % Size] = 'F';
        Historie = new string(chars);
    }

    public int CountRichtig()
    {
        return Historie.Count(c => c == 'R');
    }

    public int CountFalsch()
    {
        return Historie.Count(c => c == 'F');
    }

    public int CountLastFalschRow()
    {
        var count = 0;
        for (var i = 0; i < Size; i++)
            if (Historie[(Versuche - i + Size - 1) % Size] == 'F')
                count++;
            else
                break;
        return count;
    }

    public int CountLastRichtigRow()
    {
        var count = 0;
        for (var i = 0; i < Size; i++)
            if (Historie[(Versuche - i + Size - 1) % Size] == 'R')
                count++;
            else
                break;
        return count;
    }

    public string GetProzent()
    {
        var fenster = Math.Min(Versuche, Size);
        if (fenster < 5)
            return "--%";
        return $"{CountRichtig() * 100.0 / fenster:0}%";
    }

    public float GetProzentValue()
    {
        var fenster = Math.Min(Versuche, Size);
        if (fenster < 5)
            return 0.0f;
        return CountRichtig() / (float)fenster;
    }
}