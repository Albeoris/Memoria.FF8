using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[CollectionBuilder(typeof(NativeBattleResolvedCommandList), nameof(NativeBattleResolvedCommandList.Create))]
[JsonConverter(typeof(NativeBattleResolvedCommandListJsonConverter))]
public readonly record struct NativeBattleResolvedCommandList : IReadOnlyList<NativeBattleLogicalResolvedCommand>
{
    private readonly NativeBattleCommandPages<NativeBattleLogicalResolvedCommand> pages;

    internal NativeBattleResolvedCommandList(NativeBattleCommandPages<NativeBattleLogicalResolvedCommand> pages)
    {
        this.pages = pages;
    }

    public static NativeBattleResolvedCommandList Empty { get; } = new(NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Empty);
    public bool IsDefault => pages.IsDefault;
    public bool IsEmpty => Length == 0 && !IsDefault;
    public bool IsDefaultOrEmpty => Length == 0;
    public int Length => pages.Length;
    public int Count => Length;
    public ref readonly NativeBattleLogicalResolvedCommand this[int index] => ref pages[index];
    NativeBattleLogicalResolvedCommand IReadOnlyList<NativeBattleLogicalResolvedCommand>.this[int index] => this[index];

    public static NativeBattleResolvedCommandList Create(ReadOnlySpan<NativeBattleLogicalResolvedCommand> values) => new(NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Create(values));

    public static NativeBattleResolvedCommandList From(IEnumerable<NativeBattleLogicalResolvedCommand> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values is NativeBattleResolvedCommandList frozen)
        {
            return frozen;
        }
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder builder = new();
        foreach (NativeBattleLogicalResolvedCommand value in values)
        {
            builder.Add(value);
        }
        return new(builder.Freeze());
    }

    public Enumerator GetEnumerator() => new(pages.GetEnumerator());
    IEnumerator<NativeBattleLogicalResolvedCommand> IEnumerable<NativeBattleLogicalResolvedCommand>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<NativeBattleLogicalResolvedCommand>
    {
        private NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Enumerator iterator;

        internal Enumerator(NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Enumerator iterator)
        {
            this.iterator = iterator;
        }

        public readonly ref readonly NativeBattleLogicalResolvedCommand Current => ref iterator.Current;
        readonly NativeBattleLogicalResolvedCommand IEnumerator<NativeBattleLogicalResolvedCommand>.Current => Current;
        readonly object IEnumerator.Current => Current;
        public bool MoveNext() => iterator.MoveNext();
        void IEnumerator.Reset() => iterator.Reset();

        public readonly void Dispose()
        {
        }
    }

    internal NativeBattleResolvedCommandList MapPayloads(Func<NativeBattleLogicalCommandReference, NativeBattleLogicalCommandReference> mapCommand, Func<NativeBattlePrimitiveDrawMode, NativeBattlePrimitiveDrawMode> mapMode)
    {
        return new(pages.Map(
            item => new(mapCommand(item.Command), mapMode(item.DrawMode), item.Pass),
            static (original, mapped) => original.Command.HasSameOwner(mapped.Command) && ReferenceEquals(original.DrawMode, mapped.DrawMode)));
    }
}

internal sealed class NativeBattleResolvedCommandListJsonConverter : JsonConverter<NativeBattleResolvedCommandList>
{
    public override NativeBattleResolvedCommandList Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Native resolved commands require a JSON array.");
        }
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder commands = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return new(commands.Freeze());
            }
            commands.Add(JsonSerializer.Deserialize<NativeBattleLogicalResolvedCommand>(ref reader, options));
        }
        throw new JsonException("Native resolved command array is incomplete.");
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleResolvedCommandList value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in value)
        {
            JsonSerializer.Serialize(writer, command, options);
        }
        writer.WriteEndArray();
    }
}
