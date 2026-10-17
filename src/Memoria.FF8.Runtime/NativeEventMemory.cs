using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Immutable typed event memory over an implicit zero byte background. Missing bytes read as zero; this is the runtime memory contract, not snapshot reconstruction.</summary>
[JsonConverter(typeof(NativeEventMemoryJsonConverter))]
public sealed class NativeEventMemory
{
    private readonly IReadOnlyDictionary<int, NativeMemoryValue> cells;
    private readonly IReadOnlyDictionary<int, int> knownValues;

    public const int CurrentVersion = 1;
    public const int MaximumAddress = ushort.MaxValue - 3;

    public int Version { get; }
    public IReadOnlyDictionary<int, NativeMemoryValue> Cells => CopyCells(cells);

    internal IEnumerable<(int Address, int Width)> CellRanges()
    {
        foreach ((int address, NativeMemoryValue value) in cells)
        {
            yield return (address, value.Width);
        }
    }

    [JsonIgnore]
    public IReadOnlyDictionary<int, int> KnownValues => knownValues;

    [JsonIgnore]
    public IReadOnlyDictionary<int, NativeMemoryValue> UncertainValues => CopyCells(cells.Where(pair => !pair.Value.Value.IsExact).ToDictionary());

    public static NativeEventMemory Empty { get; } = new(CurrentVersion, new Dictionary<int, NativeMemoryValue>());

    public NativeEventMemory(int version, IReadOnlyDictionary<int, NativeMemoryValue> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Version = version;
        ValidateCells(version, cells);
        this.cells = CopyCells(cells);
        knownValues = new ReadOnlyDictionary<int, int>(this.cells.Where(pair => pair.Value.Value.IsExact).ToDictionary(pair => pair.Key, pair => pair.Value.Value.Values[0]));
    }

    /// <summary>Creates synthetic fixtures whose explicitly declared cells are DWORDs. Never infer persisted cell widths through this helper.</summary>
    public static NativeEventMemory FromDWords(IReadOnlyDictionary<int, int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new NativeEventMemory(CurrentVersion, values.ToDictionary(pair => pair.Key, pair => new NativeMemoryValue(4, NativeScalarValue.Exact(pair.Value))));
    }

    public NativeScalarValue Read(int address, int width, bool signed = false)
    {
        ValidateAccess(address, width);
        NativeMemoryValue? uncertain = null;
        int uncertainCount = 0;
        int uncertainAddress = -1;
        uint value = 0;
        // A validated cell is at most four bytes wide, so only these starts can overlap.
        for (int start = Math.Max(0, address - 3); start < address + width; start++)
        {
            if (!cells.TryGetValue(start, out NativeMemoryValue? entry) || !Overlaps(start, entry.Width, address, width))
            {
                continue;
            }
            if (!entry.Value.IsExact)
            {
                uncertain = entry;
                uncertainCount++;
                uncertainAddress = start;
                continue;
            }
            for (int target = Math.Max(address, start); target < Math.Min(address + width, start + entry.Width); target++)
            {
                uint next = unchecked((uint)entry.Value.Values[0]) >> (8 * (target - start)) & byte.MaxValue;
                value |= next << (8 * (target - address));
            }
        }
        if (uncertain is not null)
        {
            if (uncertainCount != 1 || uncertainAddress != address || uncertain.Width != width)
            {
                throw new NotSupportedException("A native event memory read cannot split or combine uncertain cells.");
            }
            return CopyValue(uncertain.Value).ToWidth(width, signed && width != 4);
        }
        return NativeScalarValue.Exact(unchecked((int)value)).ToWidth(width, signed && width != 4);
    }

    /// <summary>Checks explicitly represented exact bytes; implicit zero background does not establish a predicate dependency.</summary>
    public bool HasRepresentedExactBytes(int address, int width)
    {
        ValidateAccess(address, width);
        for (int offset = 0; offset < width; offset++)
        {
            int target = address + offset;
            bool found = false;
            for (int start = Math.Max(0, target - 3); start <= target; start++)
            {
                if (cells.TryGetValue(start, out NativeMemoryValue? cell) && target < start + cell.Width && cell.Value.IsExact)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
        }
        return true;
    }

    public NativeEventMemory Write(int address, int width, NativeScalarValue value)
    {
        ValidateAccess(address, width);
        ArgumentNullException.ThrowIfNull(value);
        NativeScalarValue projected = CopyValue(value).ToWidth(width);
        KeyValuePair<int, NativeMemoryValue>[] overlaps = cells.Where(pair => Overlaps(pair.Key, pair.Value.Width, address, width)).ToArray();
        if (overlaps.Any(pair => !pair.Value.Value.IsExact && (pair.Key != address || pair.Value.Width != width)))
        {
            throw new NotSupportedException("A native event memory write cannot partially overwrite or combine uncertain cells.");
        }
        Dictionary<int, NativeMemoryValue> next = cells.ToDictionary();
        Dictionary<int, byte> retained = [];
        foreach ((int start, NativeMemoryValue entry) in overlaps)
        {
            next.Remove(start);
            if (entry.Value.IsExact)
            {
                for (int index = 0; index < entry.Width; index++)
                {
                    if (projected.IsExact || start + index < address || start + index >= address + width)
                    {
                        retained[start + index] = unchecked((byte)((uint)entry.Value.Values[0] >> (8 * index)));
                    }
                }
            }
        }
        if (projected.IsExact)
        {
            for (int index = 0; index < width; index++)
            {
                retained[address + index] = unchecked((byte)((uint)projected.Values[0] >> (8 * index)));
            }
        }
        else
        {
            next.Add(address, new NativeMemoryValue(width, projected));
        }
        AddExactBytes(next, retained);
        return new NativeEventMemory(CurrentVersion, next);
    }

    public NativeEventMemory Clear(int first, int endExclusive)
    {
        if (first < 0 || first > ushort.MaxValue + 1 || endExclusive < first || endExclusive > ushort.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(first));
        }
        if (first == endExclusive)
        {
            return this;
        }
        KeyValuePair<int, NativeMemoryValue>[] overlaps = cells.Where(pair => pair.Key < endExclusive && pair.Key + pair.Value.Width > first).ToArray();
        if (overlaps.Any(pair => !pair.Value.Value.IsExact && (pair.Key < first || pair.Key + pair.Value.Width > endExclusive)))
        {
            throw new NotSupportedException("A native event memory clear cannot split an uncertain cell.");
        }
        Dictionary<int, NativeMemoryValue> next = cells.ToDictionary();
        foreach ((int start, NativeMemoryValue entry) in overlaps)
        {
            if (start >= first && start + entry.Width <= endExclusive)
            {
                next.Remove(start);
                continue;
            }
            uint value = unchecked((uint)entry.Value.Values[0]);
            for (int target = Math.Max(first, start); target < Math.Min(endExclusive, start + entry.Width); target++)
            {
                value &= ~(255u << (8 * (target - start)));
            }
            next[start] = new NativeMemoryValue(entry.Width, NativeScalarValue.Exact(unchecked((int)value)));
        }
        return new NativeEventMemory(CurrentVersion, next);
    }

    public NativeEventMemory Remove(int address)
    {
        ValidateAccess(address, 1);
        if (!cells.ContainsKey(address))
        {
            return this;
        }
        Dictionary<int, NativeMemoryValue> next = cells.ToDictionary();
        next.Remove(address);
        return new NativeEventMemory(CurrentVersion, next);
    }

    public void Validate()
    {
        ValidateCells(Version, cells);
    }

    public bool Overlaps(int address, int width)
    {
        if (address < 0 || width <= 0 || (long)address + width > ushort.MaxValue + 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "An ownership range must fit the physical event address space.");
        }
        return cells.Any(pair => Overlaps(pair.Key, pair.Value.Width, address, width));
    }

    private static void ValidateCells(int version, IReadOnlyDictionary<int, NativeMemoryValue> entries)
    {
        if (version != CurrentVersion)
        {
            throw new InvalidDataException("Unsupported native event memory version.");
        }
        int previousEnd = -1;
        foreach ((int address, NativeMemoryValue entry) in entries.OrderBy(pair => pair.Key))
        {
            if (address < 0 || address > MaximumAddress || entry is null || entry.Width is not (1 or 2 or 4) || entry.Value is null || entry.Value.ActorInfoOrigin is not null || address < previousEnd)
            {
                throw new InvalidDataException("Native event memory requires valid, nonoverlapping typed cells.");
            }
            entry.Value.Validate();
            bool fitsWidth = entry.Value.IsExact ? entry.Width == 4 || entry.Value.Values[0] == (entry.Width == 1 ? unchecked((byte)entry.Value.Values[0]) : unchecked((ushort)entry.Value.Values[0])) : entry.Value.Values.SequenceEqual(entry.Value.ToWidth(entry.Width).Values);
            if (!fitsWidth)
            {
                throw new InvalidDataException("Native event memory cell differs from its declared width projection.");
            }
            previousEnd = address + entry.Width;
        }
    }

    private static void ValidateAccess(int address, int width)
    {
        if (address < 0 || address > MaximumAddress || width is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Native memory requires an address in 0..65532 and a BYTE, WORD or DWORD width.");
        }
    }

    private static bool Overlaps(int first, int firstWidth, int second, int secondWidth) => first < second + secondWidth && first + firstWidth > second;

    private static IReadOnlyDictionary<int, NativeMemoryValue> CopyCells(IReadOnlyDictionary<int, NativeMemoryValue> source)
    {
        return new ReadOnlyDictionary<int, NativeMemoryValue>(source.ToDictionary(pair => pair.Key, pair => new NativeMemoryValue(pair.Value.Width, CopyValue(pair.Value.Value))));
    }

    private static NativeScalarValue CopyValue(NativeScalarValue value)
    {
        value.Validate();
        NativeTimerReadOrigin? origin = value.Origin;
        if (origin is not null)
        {
            origin = origin with { Timer = origin.Timer with { Alternatives = [.. origin.Timer.Alternatives] }, Projections = origin.Projections is null ? null : [.. origin.Projections] };
        }
        return value with { Values = [.. value.Values], Origin = origin };
    }

    private static void AddExactBytes(Dictionary<int, NativeMemoryValue> target, Dictionary<int, byte> bytes)
    {
        int[] addresses = bytes.Keys.Order().ToArray();
        int index = 0;
        while (index < addresses.Length)
        {
            int start = addresses[index];
            int end = index + 1;
            while (end < addresses.Length && addresses[end] == addresses[end - 1] + 1)
            {
                end++;
            }
            while (index < end)
            {
                start = addresses[index];
                int remaining = end - index;
                int width = remaining >= 4 ? 4 : remaining >= 2 ? 2 : 1;
                if (addresses[end - 1] > MaximumAddress && start < MaximumAddress && remaining - width < 4)
                {
                    width = MaximumAddress - start >= 2 ? 2 : 1;
                }
                if (start > MaximumAddress)
                {
                    throw new NotSupportedException("Retained native bytes cannot be represented within the supported cell address range.");
                }
                uint value = 0;
                for (int offset = 0; offset < width; offset++)
                {
                    value |= (uint)bytes[start + offset] << (8 * offset);
                }
                target.Add(start, new NativeMemoryValue(width, NativeScalarValue.Exact(unchecked((int)value))));
                index += width;
            }
        }
    }
}

public sealed class NativeEventMemoryJsonConverter : JsonConverter<NativeEventMemory>
{
    public override NativeEventMemory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Count(property => property.NameEquals(nameof(NativeEventMemory.Version))) != 1 || properties.Count(property => property.NameEquals(nameof(NativeEventMemory.Cells))) != 1)
        {
            throw new JsonException("Native event memory requires explicit unique Version and Cells properties.");
        }
        Dictionary<int, NativeMemoryValue> cells = [];
        foreach (JsonProperty property in root.GetProperty(nameof(NativeEventMemory.Cells)).EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int address) || !cells.TryAdd(address, property.Value.Deserialize<NativeMemoryValue>(options)!))
            {
                throw new JsonException("Native event memory cell addresses must be unique integers.");
            }
        }
        return new NativeEventMemory(root.GetProperty(nameof(NativeEventMemory.Version)).GetInt32(), cells);
    }

    public override void Write(Utf8JsonWriter writer, NativeEventMemory value, JsonSerializerOptions options)
    {
        value.Validate();
        writer.WriteStartObject();
        writer.WriteNumber(nameof(NativeEventMemory.Version), value.Version);
        writer.WritePropertyName(nameof(NativeEventMemory.Cells));
        JsonSerializer.Serialize(writer, value.Cells, options);
        writer.WriteEndObject();
    }
}
