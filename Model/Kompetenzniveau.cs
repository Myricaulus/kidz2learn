namespace Kidz2Learn.Model;

public class Kompetenzniveau
{
    private const int SIZE = 20;
    public int Versuche { get; set; } = 0;
    public int Richtig { get; set; } = 0;
    public string Historie { get; set; } = "--------------------";

    public void AddRichtig()
    {
        if(Historie.Length<SIZE)
            Historie = Historie.PadRight(SIZE, '-');
        char[] chars = Historie.ToCharArray();
        chars[Versuche++ % SIZE] = 'R'; 
        Historie = new string(chars);
        ++Richtig;
    }
    public void AddFalsch()
    {
        if(Historie.Length<SIZE)
            Historie = Historie.PadRight(SIZE, '-');
        char[] chars = Historie.ToCharArray();
        chars[Versuche++ % SIZE] = 'F'; 
        Historie = new string(chars);
    }

    public int CountRichtig()
    {
        return Historie.Count((c) => c == 'R');
    }
    public int CountFalsch()
    {
        return Historie.Count((c) => c == 'F');
    }

    public string GetProzent()
    {
        var fenster = Math.Min(Versuche, SIZE);
        if (fenster < 5)
            return "--%";
        return $"{CountRichtig() * 100.0 / fenster:0}%";
    } 

    public float GetProzentValue()
    {
        var fenster = Math.Min(Versuche, SIZE);
        if (fenster < 5)
            return 0.0f;
        return CountRichtig() / (float)fenster;
    }
}
