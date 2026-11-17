using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[CollectionBuilder(typeof(NativeBattleResolvedCommandList), nameof(NativeBattleResolvedCommandList.Create))]
[JsonConverter(typeof(NativeBattleResolvedCommandListJsonConverter))]
public readonly record struct NativeBattleResolvedCommandList : IReadOnlyList<NativeBattleLogicalResolvedCommand>
{
    private const int PageSize = 256;
    private readonly NativeBattleLogicalResolvedCommand[][]? pages;

    private NativeBattleResolvedCommandList(NativeBattleLogicalResolvedCommand[][] pages, int length)
    {
        this.pages = pages;
        Length = length;
    }

    public static NativeBattleResolvedCommandList Empty { get; } = new([], 0);
    public bool IsDefault => pages is null;
    public bool IsEmpty => Length == 0 && !IsDefault;
    public bool IsDefaultOrEmpty => Length == 0;
    public int Length { get; }
    public int Count => Length;

    public ref readonly NativeBattleLogicalResolvedCommand this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));
            return ref pages![index / PageSize][index % PageSize];
        }
    }

    NativeBattleLogicalResolvedCommand IReadOnlyList<NativeBattleLogicalResolvedCommand>.this[int index] => this[index];

    public static NativeBattleResolvedCommandList Create(ReadOnlySpan<NativeBattleLogicalResolvedCommand> values)
    {
        if (values.IsEmpty)
        {
            return Empty;
        }
        NativeBattleLogicalResolvedCommand[][] pages = new NativeBattleLogicalResolvedCommand[(values.Length - 1) / PageSize + 1][];
        for (int page = 0; page < pages.Length; page++)
        {
            int offset = page * PageSize;
            NativeBattleLogicalResolvedCommand[] entries = new NativeBattleLogicalResolvedCommand[PageSize];
            values.Slice(offset, Math.Min(PageSize, values.Length - offset)).CopyTo(entries);
            pages[page] = entries;
        }
        return new(pages, values.Length);
    }

    public static NativeBattleResolvedCommandList From(IEnumerable<NativeBattleLogicalResolvedCommand> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values is NativeBattleResolvedCommandList frozen)
        {
            return frozen;
        }
        Builder builder = new();
        foreach (NativeBattleLogicalResolvedCommand value in values)
        {
            builder.Add(value);
        }
        return builder.Freeze();
    }

    public Enumerator GetEnumerator() => new(this);
    IEnumerator<NativeBattleLogicalResolvedCommand> IEnumerable<NativeBattleLogicalResolvedCommand>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<NativeBattleLogicalResolvedCommand>
    {
        private readonly NativeBattleResolvedCommandList commands;
        private int index;

        internal Enumerator(NativeBattleResolvedCommandList commands)
        {
            this.commands = commands;
            index = -1;
        }

        public readonly ref readonly NativeBattleLogicalResolvedCommand Current => ref commands[index];
        readonly NativeBattleLogicalResolvedCommand IEnumerator<NativeBattleLogicalResolvedCommand>.Current => Current;
        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (index < commands.Length)
            {
                index++;
            }
            return index < commands.Length;
        }

        void IEnumerator.Reset() => index = -1;
        public readonly void Dispose()
        {
        }
    }

    internal sealed class Builder
    {
        private List<NativeBattleLogicalResolvedCommand[]>? pages;
        private int length;
        private bool frozen;

        internal void Add(NativeBattleLogicalResolvedCommand command)
        {
            RequireOpen();
            int offset = length % PageSize;
            if (offset == 0)
            {
                (pages ??= []).Add(new NativeBattleLogicalResolvedCommand[PageSize]);
            }
            pages![^1][offset] = command;
            length++;
        }

        internal NativeBattleResolvedCommandList Freeze()
        {
            RequireOpen();
            frozen = true;
            NativeBattleResolvedCommandList result = pages is null ? Empty : new(pages.ToArray(), length);
            pages = null;
            return result;
        }

        private void RequireOpen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("The native resolved command builder is already frozen.");
            }
        }
    }

    internal NativeBattleResolvedCommandList MapPayloads(Func<NativeBattleLogicalCommand, NativeBattleLogicalCommand> mapCommand, Func<NativeBattlePrimitiveDrawMode, NativeBattlePrimitiveDrawMode> mapMode)
    {
        if (IsDefaultOrEmpty)
        {
            return this;
        }
        NativeBattleLogicalResolvedCommand[][]? mappedPages = null;
        for (int page = 0; page < pages!.Length; page++)
        {
            NativeBattleLogicalResolvedCommand[] original = pages[page];
            NativeBattleLogicalResolvedCommand[]? mapped = null;
            int used = Math.Min(PageSize, Length - page * PageSize);
            for (int index = 0; index < used; index++)
            {
                ref readonly NativeBattleLogicalResolvedCommand item = ref original[index];
                NativeBattleLogicalCommand command = mapCommand(item.Command);
                NativeBattlePrimitiveDrawMode mode = mapMode(item.DrawMode);
                if (!ReferenceEquals(command, item.Command) || !ReferenceEquals(mode, item.DrawMode))
                {
                    mapped ??= (NativeBattleLogicalResolvedCommand[])original.Clone();
                    mapped[index] = new(command, mode, item.Pass);
                }
            }
            if (mapped is not null)
            {
                mappedPages ??= (NativeBattleLogicalResolvedCommand[][])pages.Clone();
                mappedPages[page] = mapped;
            }
        }
        return mappedPages is null ? this : new(mappedPages, Length);
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
        NativeBattleResolvedCommandList.Builder commands = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return commands.Freeze();
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
