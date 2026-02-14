using System.Reflection;
using System.Text;

namespace Kidz2Learn.Model;

public static class StringAbbreviator
{
    public static string Abbreviate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return value;

        var result = new StringBuilder();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (i == 0)
            {
                // erstes Segment → nur erster Buchstabe
                result.Append(char.ToLowerInvariant(part[0]));
                continue;
            }

            if (part.All(char.IsDigit))
                result.Append(part);
            else
                result.Append(char.ToLowerInvariant(part[0]));
        }

        return result.ToString();
    }
}

public static class ConstStringCollisionChecker
{
    public static void CheckForAbbreviationCollisions<TBase>()
    {
        var baseType = typeof(TBase);
        var assembly = baseType.Assembly;

        var types = assembly
            .GetTypes()
            .Where(t => baseType.IsAssignableFrom(t));

        var values = new List<(Type type, string fieldName, string value)>();

        foreach (var type in types)
        {
            var fields = type.GetFields(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
                if (field.FieldType == typeof(string) &&
                    field.IsLiteral &&
                    !field.IsInitOnly)
                {
                    var val = (string)field.GetRawConstantValue()!;
                    values.Add((type, field.Name, val));
                }
        }

        var grouped = values
            .Select(v => new
            {
                v.type,
                v.fieldName,
                v.value,
                Abbrev = StringAbbreviator.Abbreviate(v.value)
            })
            .GroupBy(x => x.Abbrev)
            .Where(g => g.Count() > 1);

        foreach (var collision in grouped)
        {
            Console.WriteLine($"Collision for '{collision.Key}':");

            foreach (var item in collision)
                Console.WriteLine(
                    $"  {item.type.FullName}.{item.fieldName} = {item.value}");
        }
    }
}