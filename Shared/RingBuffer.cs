
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using MudBlazor;

namespace Kidz2Learn.Shared;

/// <summary>
/// This is a simple RingBuffer implementation.
/// </summary>
public class RingBuffer<T>
{
    // all buffer properties
    private readonly T[] _buffer;
    private int _itemStart;
    private int _itemLength;
    /// <summary>
    /// Gets the object at the specified index.
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0){ throw new IndexOutOfRangeException(); }
            if (index >= _itemLength) { throw new IndexOutOfRangeException(); }
            return _buffer[(_itemStart + index) % _buffer.Length]!;
        }
    }
    /// <summary>
    /// Gets the total count of items.
    /// </summary>
    public int Count => _itemLength;
    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int MaxCapacity => _buffer.Length;

    public int Itemstart { get => _itemStart; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RingBuffer{T}"/> class.
    /// </summary>
    /// <param name="maxItemCount">The maximum count of items.</param>        
    public RingBuffer(int maxItemCount = 20)
    {
        maxItemCount = Math.Clamp(maxItemCount,2,int.MaxValue);
        _buffer = new T[maxItemCount];
        _itemStart = 0;
        _itemLength = 0;
    }
    /// <summary>
    /// Json init constructor
    /// </summary>
    /// <param name="maxItemCount"></param>
    /// <param name="items"></param>
    /// <param name="itemStart"></param>
    public RingBuffer(int maxItemCount, List<T> items, int itemStart)
    {
        maxItemCount = Math.Clamp(maxItemCount,2,int.MaxValue);
        _buffer = new T[maxItemCount];
        for(var i = 0; i< Math.Min(maxItemCount,items.Count);i++)
            _buffer[i] = items[i];
        _itemStart = itemStart;
        _itemLength = Math.Min(maxItemCount,items.Count)-1;
    }
    /// <summary>
    /// Adds a new item to the buffer and overrides the oldest item
    /// if the count of items reached the maximum.
    /// </summary>
    /// <param name="newItem">The new item to be added.</param>
    public void Add(T newItem)
    {
        if (_itemLength < _buffer.Length)
        {
            _buffer[(_itemStart + _itemLength) % _buffer.Length] = newItem;
            _itemLength++;
        }
        else
        {
            _itemStart = (_itemStart + 1) % _buffer.Length;
            var nextIndex = _itemStart - 1;
            if (nextIndex < 0) { nextIndex = _buffer.Length - 1; }
            _buffer[nextIndex] = newItem;
        }
    }
    // ...
    /// <summary>
    /// Removes the first entry.
    /// </summary>
    public void RemoveFirst()
    {
        if (_itemLength == 0)
        {
            throw new IndexOutOfRangeException();
        }
        _itemStart++;
        _itemLength--;
        if (_itemStart == _buffer.Length)
        {
            _itemStart = 0;
        }
    }
    /// <summary>
    /// Clears this collection.
    /// </summary>
    public void Clear()
    {
        _itemLength = 0;
        _itemStart = 0;
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

        if(items is not null && capacity > 2)
            return new RingBuffer<T>(capacity, items, itemStart);
        else 
            return new();
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

        for (var i = 0; i < value.Count; i++)
        {
            JsonSerializer.Serialize(writer, value[i], options);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
