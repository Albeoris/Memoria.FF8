using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal interface INativeBattleOrderingCollectionScope
{
    void ValidateBorrow(long generation);
}

/// <summary>A current read-only buffer borrow. Native container reset invalidates it; it is not historical state.</summary>
public readonly struct NativeBattleOrderingList<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T>? items;
    private readonly Action? validation;
    private readonly INativeBattleOrderingCollectionScope? scope;
    private readonly long generation;

    internal NativeBattleOrderingList(IReadOnlyList<T> items, Action? validation = null, INativeBattleOrderingCollectionScope? scope = null, long generation = 0)
    {
        this.items = items;
        this.validation = validation;
        this.scope = scope;
        this.generation = generation;
    }
    private void Validate()
    {
        validation?.Invoke();
        scope?.ValidateBorrow(generation);
    }
    internal static NativeBattleOrderingList<T> FromSnapshot(ImmutableArray<T> values) => values.IsDefault ? default : new(values);
    public bool IsDefault
    {
        get
        {
            Validate();
            return items is null;
        }
    }
    public bool IsDefaultOrEmpty => IsDefault || Length == 0;
    public int Length
    {
        get
        {
            Validate();
            return items?.Count ?? 0;
        }
    }
    public int Count => Length;
    public T this[int index]
    {
        get
        {
            Validate();
            return items is null ? throw new InvalidOperationException("Ordering collection storage is unknown.") : items[index];
        }
    }
    public Enumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<T>
    {
        private readonly NativeBattleOrderingList<T> source;
        private int index;
        internal Enumerator(NativeBattleOrderingList<T> source)
        {
            this.source = source;
            index = -1;
        }
        public T Current => source[index];
        object? IEnumerator.Current => Current;
        public bool MoveNext() => ++index < source.Length;
        public void Reset() => index = -1;
        public void Dispose()
        {
        }
    }
}

internal sealed record NativeBattleLogicalOrderingData([property: JsonRequired] ImmutableArray<NativeBattlePrimitiveLink> Table, [property: JsonRequired] ImmutableArray<NativeBattleLogicalProxy> Proxies, [property: JsonRequired] ImmutableArray<NativeBattleLogicalPacket> Packets, int? ProxyCursor, byte? TableBuffer, ImmutableArray<NativeBattlePrimitiveLink>? OtherTable);

internal sealed class NativeBattleLogicalOrderingJsonConverter : JsonConverter<NativeBattleLogicalOrdering>
{
    public NativeBattleLogicalOrderingJsonConverter()
    {
    }
    public override NativeBattleLogicalOrdering Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        NativeBattleLogicalOrderingData data = JsonSerializer.Deserialize<NativeBattleLogicalOrderingData>(ref reader, options) ?? throw new JsonException("Native ordering snapshot is missing.");
        return NativeBattleLogicalOrdering.FromSnapshot(data);
    }
    public override void Write(Utf8JsonWriter writer, NativeBattleLogicalOrdering value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value.RequireSnapshot(), options);
}
