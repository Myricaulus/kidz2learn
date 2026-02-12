using System.ComponentModel;

namespace Kidz2Learn.Model;

public class Kompetenzniveau
{
    private const int Size = 20;
    public int Versuche { get; set; }
    public int Richtig { get; set; }
    public string Historie { get; set; } = "--------------------";

    public void AddRichtig()
    {
        if(Historie.Length<Size)
            Historie = Historie.PadRight(Size, '-');
        var chars = Historie.ToCharArray();
        chars[Versuche++ % Size] = 'R'; 
        Historie = new string(chars);
        ++Richtig;
    }
    public void AddFalsch()
    {
        if(Historie.Length<Size)
            Historie = Historie.PadRight(Size, '-');
        var chars = Historie.ToCharArray();
        chars[Versuche++ % Size] = 'F'; 
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

    public int CountLastFalschRow()
    {
        var count = 0;
        for(var i=0; i<Size;i++)
        {
            if(Historie[(Versuche-i-1)%Size]=='F')
                count++;
            else 
                break;
        }
        return count;
    }

    public int CountLastRichtigRow()
    {
        var count = 0;
        for(var i=0; i<Size;i++)
        {
            if(Historie[(Versuche-i-1)%Size]=='R')
                count++;
            else 
                break;
        }
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
