using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kidz2Learn.Shared;

/// <summary>
///     This is a simple RingBuffer implementation.
/// </summary>
public class RingBuffer<T>
{
    // all buffer properties
    private readonly T[] _buffer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RingBuffer{T}" /> class.
    /// </summary>
    /// <param name="maxItemCount">The maximum count of items.</param>
    public RingBuffer(int maxItemCount = 20)
    {
        maxItemCount = Math.Clamp(maxItemCount, 2, int.MaxValue);
        _buffer = new T[maxItemCount];
        Itemstart = 0;
        Count = 0;
    }

    /// <summary>
    ///     Json init constructor. <paramref name="items" /> is expected already in logical order (as
    ///     written by <see cref="RingBufferJsonConverter{T}.Write" />, which walks the indexer from 0
    ///     to Count), so it is placed back at raw index 0 and <paramref name="itemStart" /> (the old
    ///     physical offset, now meaningless against the freshly rebuilt array) is ignored.
    /// </summary>
    /// <param name="maxItemCount"></param>
    /// <param name="items"></param>
    /// <param name="itemStart"></param>
    public RingBuffer(int maxItemCount, List<T> items, int itemStart)
    {
        maxItemCount = Math.Clamp(maxItemCount, 2, int.MaxValue);
        _buffer = new T[maxItemCount];
        for (var i = 0; i < Math.Min(maxItemCount, items.Count); i++)
            _buffer[i] = items[i];
        Itemstart = 0;
        Count = Math.Min(maxItemCount, items.Count);
    }

    /// <summary>
    ///     Gets the object at the specified index.
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0) throw new IndexOutOfRangeException();
            if (index >= Count) throw new IndexOutOfRangeException();
            return _buffer[(Itemstart + index) % _buffer.Length]!;
        }
    }

    /// <summary>
    ///     Gets the total count of items.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    ///     Gets the maximum capacity of the buffer.
    /// </summary>
    public int MaxCapacity => _buffer.Length;

    public int Itemstart { get; private set; }

    /// <summary>
    ///     Adds a new item to the buffer and overrides the oldest item
    ///     if the count of items reached the maximum.
    /// </summary>
    /// <param name="newItem">The new item to be added.</param>
    public void Add(T newItem)
    {
        if (Count < _buffer.Length)
        {
            _buffer[(Itemstart + Count) % _buffer.Length] = newItem;
            Count++;
        }
        else
        {
            Itemstart = (Itemstart + 1) % _buffer.Length;
            var nextIndex = Itemstart - 1;
            if (nextIndex < 0) nextIndex = _buffer.Length - 1;
            _buffer[nextIndex] = newItem;
        }
    }

    // ...
    /// <summary>
    ///     Removes the first entry.
    /// </summary>
    public void RemoveFirst()
    {
        if (Count == 0) throw new IndexOutOfRangeException();
        Itemstart++;
        Count--;
        if (Itemstart == _buffer.Length) Itemstart = 0;
    }

    /// <summary>
    ///     Clears this collection.
    /// </summary>
    public void Clear()
    {
        Count = 0;
        Itemstart = 0;
    }

    /// <summary>
    ///     Returns a new buffer with capacity <paramref name="newCapacity" />, holding
    ///     <paramref name="source" />'s items in the same logical order (works regardless of
    ///     whether <paramref name="source" /> has wrapped around - the indexer already normalizes
    ///     that). Pure/I/O-free on purpose, so migrations that need to grow a persisted buffer's
    ///     capacity (e.g. <c>SkillMigrationHelper</c>) can be unit-tested without IndexedDB/JSRuntime -
    ///     see TECH_DEBT.md #9 addendum ("Fensterbreite"). Returns <paramref name="source" /> itself,
    ///     unchanged, if it's already at least as large as requested.
    /// </summary>
    public static RingBuffer<T> Resize(RingBuffer<T> source, int newCapacity)
    {
        if (source.MaxCapacity >= newCapacity)
            return source;

        var items = new List<T>(source.Count);
        for (var i = 0; i < source.Count; i++)
            items.Add(source[i]);

        return new RingBuffer<T>(newCapacity, items, 0);
    }
}

public sealed class RingBufferJsonConverter<T> : JsonConverter<RingBuffer<T>>
{
    public override RingBuffer<T>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var capacity = 0;
        List<T>? items = null;
        var itemStart = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "capacity":
                    capacity = reader.GetInt32();
                    break;

                case "items":
                    items = JsonSerializer.Deserialize<List<T>>(ref reader, options);
                    break;

                case "itemstart":
                    itemStart = reader.GetInt32();
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        if (items is not null && capacity > 2)
            return new RingBuffer<T>(capacity, items, itemStart);
        return new RingBuffer<T>();
    }

    public override void Write(
        Utf8JsonWriter writer,
        RingBuffer<T> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("capacity", value.MaxCapacity);
        writer.WriteNumber("itemstart", value.Itemstart);

        writer.WritePropertyName("items");
        writer.WriteStartArray();

        for (var i = 0; i < value.Count; i++) JsonSerializer.Serialize(writer, value[i], options);

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}