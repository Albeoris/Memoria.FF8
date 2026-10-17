using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeScalarProjection([property: JsonRequired] int Width, [property: JsonRequired] bool Signed, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SaturateSignedLong = false);
public sealed record NativeTimerReadOrigin(int Version, int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, NativeExamTimerState Timer, uint Mask = uint.MaxValue, bool SignedWord = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeScalarProjection[]? Projections = null);

/// <summary>An executed PGETINFO read outside the retained body table; it supplies no invented memory value.</summary>
public sealed record NativeActorInfoReadOrigin(int Version, int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, int RawOperand, int BankAddress, byte NativeActorIndex, int RetainedActorCount, int ResultRegister);

/// <summary>An exact integer, timer alternatives, or an explicitly unknown executed actor-info read. No representative value is selected.</summary>
[JsonConverter(typeof(NativeScalarValueJsonConverter))]
public sealed record NativeScalarValue(int[] Values, NativeTimerReadOrigin? Origin = null, NativeActorInfoReadOrigin? ActorInfoOrigin = null)
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
        if (ActorInfoOrigin is NativeActorInfoReadOrigin read)
        {
            if (Values is not { Length: 0 } || Origin is not null || read.Version != 1 || read.FieldId < 72 || read.ProgramHash is not { Length: 64 } readHash || !readHash.All(Uri.IsHexDigit) || read.ObjectId < 0 || read.ScriptId < 0 || read.Operation < 0 || read.Frame < 0 || read.BankAddress is < 0 or >= 248 || (long)read.RawOperand + 194 != read.BankAddress || read.RetainedActorCount is < 1 or > 255 || read.NativeActorIndex < read.RetainedActorCount || read.ResultRegister is not (0 or 1 or 2 or 4 or 5 or 6))
            {
                throw new InvalidDataException("Unknown actor-info scalar lacks its executed out-of-retained-table read provenance.");
            }
            return;
        }
        if (Values is null || Values.Length == 0 || Values.Length > 64)
        {
            throw new InvalidDataException("Native scalar alternatives must be nonempty, distinct and ordered.");
        }
        for (int index = 1; index < Values.Length; index++)
        {
            if (Values[index - 1] >= Values[index])
            {
                throw new InvalidDataException("Native scalar alternatives must be nonempty, distinct and ordered.");
            }
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
        if (Origin.Version != 1 || Origin.FieldId < 72 || Origin.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || Origin.ObjectId < 0 || Origin.ScriptId < 0 || Origin.Operation < 0 || Origin.Frame < 0 || Origin.Mask is not (byte.MaxValue or ushort.MaxValue or uint.MaxValue) || Origin.SignedWord && Origin.Mask == uint.MaxValue || Origin.Projections is { Length: > 64 } || Origin.Projections?.Any(projection => projection is null || projection.Width is not (1 or 2 or 4) || projection.Signed && projection.Width == 4 || projection.SaturateSignedLong && (projection.Width != 4 || projection.Signed)) == true || !Values.SequenceEqual(Project(Origin)))
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
        if (signedWord && width == 4)
        {
            throw new ArgumentException("Signed native projection requires a BYTE or WORD.", nameof(signedWord));
        }
        Validate();
        if (ActorInfoOrigin is not null)
        {
            throw new NotSupportedException("An out-of-retained-table actor-info result has no known value to project or store.");
        }
        if (IsExact)
        {
            int value = unchecked((int)((uint)Values[0] & mask));
            return Exact(signedWord ? width == 1 ? unchecked((sbyte)value) : unchecked((short)value) : value);
        }
        NativeTimerReadOrigin origin = Origin!;
        if (width == 1 && signedWord || origin.Projections is not null)
        {
            return FromTimer(origin with { Projections = [.. origin.Projections ?? [], new NativeScalarProjection(width, signedWord)] });
        }
        return FromTimer(origin with { Mask = origin.Mask & mask, SignedWord = width == 4 ? origin.SignedWord : signedWord });
    }

    public NativeScalarValue SaturateSignedLong()
    {
        Validate();
        if (ActorInfoOrigin is not null)
        {
            throw new NotSupportedException("An out-of-retained-table actor-info result cannot be projected as signed event memory.");
        }
        if (IsExact)
        {
            return Exact(Math.Clamp(Values[0], -0x800000, 0x7FFFFF));
        }
        if (Values.All(value => value is >= -0x800000 and <= 0x7FFFFF))
        {
            return this;
        }
        return FromTimer(Origin! with { Projections = [.. Origin!.Projections ?? [], new NativeScalarProjection(4, false, true)] });
    }

    private static int[] Project(NativeTimerReadOrigin origin)
    {
        IEnumerable<int> values = origin.Timer.Alternatives.Select(item => unchecked((int)((uint)item.Remaining & origin.Mask))).Select(value => origin.SignedWord ? unchecked((short)value) : value);
        foreach (NativeScalarProjection projection in origin.Projections ?? [])
        {
            values = values.Select(value => projection.SaturateSignedLong ? Math.Clamp(value, -0x800000, 0x7FFFFF) : projection.Width == 1 ? projection.Signed ? unchecked((sbyte)value) : unchecked((byte)value) : projection.Width == 2 ? projection.Signed ? unchecked((short)value) : unchecked((ushort)value) : value).ToArray();
        }
        return values.Distinct().Order().ToArray();
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
        NativeTimerReadOrigin? timerOrigin = root.TryGetProperty("Origin", out JsonElement timer) ? timer.Deserialize<NativeTimerReadOrigin>(options) : null;
        NativeActorInfoReadOrigin? actorOrigin = root.TryGetProperty("ActorInfoOrigin", out JsonElement actor) ? actor.Deserialize<NativeActorInfoReadOrigin>(options) : null;
        NativeScalarValue value = new(root.GetProperty("Values").Deserialize<int[]>(options)!, timerOrigin, actorOrigin);
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
        if (value.ActorInfoOrigin is NativeActorInfoReadOrigin actorOrigin)
        {
            writer.WritePropertyName("ActorInfoOrigin");
            JsonSerializer.Serialize(writer, actorOrigin, options);
        }
        else
        {
            writer.WritePropertyName("Origin");
            JsonSerializer.Serialize(writer, value.Origin, options);
        }
        writer.WriteEndObject();
    }
}

public sealed record NativeMemoryValue(int Width, NativeScalarValue Value)
{
    public static void Validate(IReadOnlyDictionary<int, NativeMemoryValue> memory, IReadOnlyDictionary<int, int> knownVariables)
    {
        foreach ((int address, NativeMemoryValue entry) in memory)
        {
            if (address < 0 || address > ushort.MaxValue - 3 || entry is null || entry.Width is not (1 or 2 or 4) || entry.Value is null || entry.Value.ActorInfoOrigin is not null)
            {
                throw new InvalidDataException("Invalid typed native memory entry.");
            }
            entry.Value.Validate();
            NativeScalarValue projected = entry.Value.ToWidth(entry.Width);
            if (!entry.Value.Values.SequenceEqual(projected.Values) || knownVariables.Keys.Any(key => key >= address && key < address + entry.Width) || memory.Any(other => other.Key != address && other.Key < address + entry.Width && other.Key + other.Value.Width > address))
            {
                throw new InvalidDataException("Typed native memory overlaps an alias or has an inconsistent width projection.");
            }
        }
    }
}
