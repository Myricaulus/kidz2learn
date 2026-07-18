using System.Text.Json;
using System.Text.Json.Serialization;
using Kidz2Learn.Shared;
using Xunit;

namespace Kidz2Learn.Tests;

public class RingBufferTests
{
    [Fact]
    public void Add_WithinCapacity_KeepsInsertionOrder()
    {
        var rb = new RingBuffer<int>(3);
        rb.Add(1);
        rb.Add(2);
        rb.Add(3);

        Assert.Equal(3, rb.Count);
        Assert.Equal([1, 2, 3], new[] { rb[0], rb[1], rb[2] });
    }

    [Fact]
    public void Add_BeyondCapacity_OverwritesOldestAndKeepsLogicalOrder()
    {
        var rb = new RingBuffer<int>(3);
        rb.Add(1);
        rb.Add(2);
        rb.Add(3);
        rb.Add(4); // evicts 1

        Assert.Equal(3, rb.Count);
        Assert.Equal([2, 3, 4], new[] { rb[0], rb[1], rb[2] });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_ClampsCapacityToAtLeastTwo(int requested)
    {
        var rb = new RingBuffer<int>(requested);

        Assert.Equal(2, rb.MaxCapacity);
    }

    [Fact]
    public void RemoveFirst_DropsOldestItem()
    {
        var rb = new RingBuffer<int>(3);
        rb.Add(1);
        rb.Add(2);
        rb.Add(3);

        rb.RemoveFirst();

        Assert.Equal(2, rb.Count);
        Assert.Equal([2, 3], new[] { rb[0], rb[1] });
    }

    [Fact]
    public void RemoveFirst_OnEmptyBuffer_Throws()
    {
        var rb = new RingBuffer<int>(3);

        Assert.Throws<IndexOutOfRangeException>(() => rb.RemoveFirst());
    }

    [Fact]
    public void Clear_ResetsCountToZero()
    {
        var rb = new RingBuffer<int>(3);
        rb.Add(1);
        rb.Add(2);

        rb.Clear();

        Assert.Equal(0, rb.Count);
        Assert.Throws<IndexOutOfRangeException>(() => rb[0]);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var rb = new RingBuffer<int>(3);
        rb.Add(1);

        Assert.Throws<IndexOutOfRangeException>(() => rb[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => rb[1]); // only index 0 is populated
    }

    private class RingBufferHolder
    {
        [JsonConverter(typeof(RingBufferJsonConverter<int>))]
        public RingBuffer<int> Buffer { get; set; } = new(3);
    }

    [Fact(Skip =
        "Known bug, see TECH_DEBT.md #6: the RingBuffer(int, List<T>, int) constructor used by " +
        "RingBufferJsonConverter sets Count = items.Count - 1 unconditionally, so every JSON " +
        "round-trip drops the most recently added item, even with no wraparound. Un-skip once " +
        "that off-by-one is fixed.")]
    public void JsonRoundTrip_WithoutWraparound_PreservesItemsAndOrder()
    {
        var holder = new RingBufferHolder();
        holder.Buffer.Add(1);
        holder.Buffer.Add(2);

        var json = JsonSerializer.Serialize(holder);
        var restored = JsonSerializer.Deserialize<RingBufferHolder>(json)!;

        Assert.Equal(2, restored.Buffer.Count);
        Assert.Equal([1, 2], new[] { restored.Buffer[0], restored.Buffer[1] });
    }

    [Fact(Skip =
        "Known bug, see TECH_DEBT.md #6: on top of the Count off-by-one above, the constructor " +
        "places the already-reordered 'items' at raw buffer index 0 but keeps the old physical " +
        "'itemstart' from before serialization. Once a buffer has wrapped around at least once, " +
        "this additionally misaligns the indexer, dropping/misreading data beyond just the " +
        "off-by-one. Un-skip this once RingBuffer(int, List<T>, int) is fixed.")]
    public void JsonRoundTrip_AfterWraparound_PreservesAllItems()
    {
        var holder = new RingBufferHolder();
        holder.Buffer.Add(1);
        holder.Buffer.Add(2);
        holder.Buffer.Add(3);
        holder.Buffer.Add(4); // capacity 3 -> wraps, logical contents are [2, 3, 4]

        var json = JsonSerializer.Serialize(holder);
        var restored = JsonSerializer.Deserialize<RingBufferHolder>(json)!;

        Assert.Equal(3, restored.Buffer.Count);
        Assert.Equal([2, 3, 4], new[] { restored.Buffer[0], restored.Buffer[1], restored.Buffer[2] });
    }
}
