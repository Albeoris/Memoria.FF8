using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeTimerReadOrigin(int Version, int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, NativeExamTimerState Timer, uint Mask = uint.MaxValue, bool SignedWord = false);

/// <summary>An exact integer or all possible results of one executed native timer read. No representative value is selected.</summary>
[JsonConverter(typeof(NativeScalarValueJsonConverter))]
public sealed record NativeScalarValue(int[] Values, NativeTimerReadOrigin? Origin = null)
{
    public bool IsExact => Values is { Length: 1 };

    public static NativeScalarValue Exact(int value)
    {
        return new NativeScalarValue([value]);
    }

    public static NativeScalarValue FromTimer(NativeTimerReadOrigin origin)
    {
        int[] values = Project(origin);
        return values.Length == 1 ? Exact(values[0]) : new NativeScalarValue(values, origin);
    }

    public void Validate()
    {
        if (Values is null || Values.Length == 0 || Values.Length > 64 || !Values.SequenceEqual(Values.Distinct().Order()))
        {
            throw new InvalidDataException("Native scalar alternatives must be nonempty, distinct and ordered.");
        }
        if (Origin is null)
        {
            if (!IsExact)
            {
                throw new InvalidDataException("Uncertain native scalar has no executed read provenance.");
            }
            return;
        }
        if (Origin.Timer is null)
        {
            throw new InvalidDataException("Native scalar source has no timer snapshot.");
        }
        Origin.Timer.Validate();
        if (Origin.Version != 1 || Origin.FieldId < 72 || Origin.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || Origin.ObjectId < 0 || Origin.ScriptId < 0 || Origin.Operation < 0 || Origin.Frame < 0 || Origin.Mask is not (byte.MaxValue or ushort.MaxValue or uint.MaxValue) || Origin.SignedWord && Origin.Mask == uint.MaxValue || !Values.SequenceEqual(Project(Origin)))
        {
            throw new InvalidDataException("Native scalar values differ from their executed timer read and width projection.");
        }
    }

    public int RequireExact()
    {
        Validate();
        return IsExact ? Values[0] : throw new NotSupportedException("This native integer remains uncertain; no representative value can be used.");
    }

    public NativeScalarValue ToWidth(int width, bool signedWord = false)
    {
        uint mask = width switch
        {
            1 => byte.MaxValue,
            2 => ushort.MaxValue,
            4 => uint.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(width))
        };
        if (signedWord && width != 2)
        {
            throw new ArgumentException("Signed native projection requires a WORD.", nameof(signedWord));
        }
        Validate();
        if (IsExact)
        {
            int value = unchecked((int)((uint)Values[0] & mask));
            return Exact(signedWord ? unchecked((short)value) : value);
        }
        NativeTimerReadOrigin origin = Origin!;
        return FromTimer(origin with { Mask = origin.Mask & mask, SignedWord = width == 4 ? origin.SignedWord : signedWord });
    }

    private static int[] Project(NativeTimerReadOrigin origin)
    {
        return origin.Timer.Alternatives.Select(item => unchecked((int)((uint)item.Remaining & origin.Mask))).Select(value => origin.SignedWord ? unchecked((short)value) : value).Distinct().Order().ToArray();
    }

    public static implicit operator NativeScalarValue(int value) => Exact(value);
    public static explicit operator int(NativeScalarValue value) => value.RequireExact();
    public static explicit operator uint(NativeScalarValue value) => unchecked((uint)value.RequireExact());
}

public sealed class NativeScalarValueJsonConverter : JsonConverter<NativeScalarValue>
{
    public override NativeScalarValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return NativeScalarValue.Exact(reader.GetInt32());
        }
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        NativeScalarValue value = new(root.GetProperty("Values").Deserialize<int[]>(options)!, root.GetProperty("Origin").Deserialize<NativeTimerReadOrigin>(options));
        value.Validate();
        return value;
    }

    public override void Write(Utf8JsonWriter writer, NativeScalarValue value, JsonSerializerOptions options)
    {
        value.Validate();
        if (value.IsExact)
        {
            writer.WriteNumberValue(value.Values[0]);
            return;
        }
        writer.WriteStartObject();
        writer.WritePropertyName("Values");
        JsonSerializer.Serialize(writer, value.Values, options);
        writer.WritePropertyName("Origin");
        JsonSerializer.Serialize(writer, value.Origin, options);
        writer.WriteEndObject();
    }
}

public sealed record NativeMemoryValue(int Width, NativeScalarValue Value)
{
    public static void Validate(IReadOnlyDictionary<int, NativeMemoryValue> memory, IReadOnlyDictionary<int, int> knownVariables)
    {
        foreach ((int address, NativeMemoryValue entry) in memory)
        {
            if (address < 0 || address > ushort.MaxValue - 3 || entry is null || entry.Width is not (1 or 2 or 4) || entry.Value is null || entry.Value.IsExact)
            {
                throw new InvalidDataException("Invalid uncertain native memory entry.");
            }
            entry.Value.Validate();
            NativeScalarValue projected = entry.Value.ToWidth(entry.Width);
            if (!entry.Value.Values.SequenceEqual(projected.Values) || knownVariables.Keys.Any(key => key >= address && key < address + entry.Width) || memory.Any(other => other.Key != address && other.Key < address + entry.Width && other.Key + other.Value.Width > address))
            {
                throw new InvalidDataException("Uncertain native memory overlaps an alias or has an inconsistent width projection.");
            }
        }
    }
}
