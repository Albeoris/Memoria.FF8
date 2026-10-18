using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One retained physical Bio mesh; each morph replaces XYZ without resetting its other bytes.</summary>
public sealed record NativeBattleBio22Geometry([property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        NativeBattleEnhancementMorph.Validate(Bytes);
        int count = BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(4));
        if (!((Bytes.Length == 0x1480 && count == 91) || (Bytes.Length == 0xA38 && count == 42) || (Bytes.Length == 0x5028 && count == 467) || (Bytes.Length == 0x4038 && count == 258)))
        {
            throw new InvalidDataException("Native Bio geometry requires one of its four complete current mesh profiles.");
        }
    }

    public void ValidateForAddress(uint address)
    {
        Validate();
        if (!((address == 0x1615CE4 && Bytes.Length == 0x1480) || (address == 0x1617E3C && Bytes.Length == 0xA38) || (address == 0x1619888 && Bytes.Length == 0x5028) || (address == 0x161F750 && Bytes.Length == 0x4038)))
        {
            throw new InvalidDataException("Native Bio geometry profile contradicts its physical mesh address.");
        }
    }

    public static NativeBattleBio22Geometry FromColdProcess(NativeBattleStaticMeshResource authenticatedBaseline)
    {
        if (authenticatedBaseline is null)
        {
            throw new InvalidDataException("Native Bio cold geometry requires its authenticated mesh baseline.");
        }
        NativeBattleBio22Geometry result = new(authenticatedBaseline.Bytes);
        result.ValidateForAddress(authenticatedBaseline.Address);
        return result;
    }

    public NativeBattleBio22Geometry Morph89B0D0(ImmutableArray<byte> first, ImmutableArray<byte> second, short ratio)
    {
        ImmutableArray<byte> next = NativeBattleEnhancementMorph.Morph(Bytes, first, second, ratio);
        if (next.Equals(Bytes))
        {
            return this;
        }
        NativeBattleBio22Geometry result = new(next);
        result.Validate();
        return result;
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22Geometry? Bio22Geometry { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22Geometry? Bio22SecondaryGeometry { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22Geometry? Bio22ThirdGeometry { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22Geometry? Bio22FourthGeometry { get; init; }
}
