using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectNodeScratch([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] byte KnownMask)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectArena? Prefix2793E08 { get; init; }

    public static NativeBattleEffectNodeScratch FromColdProcess() => new(new byte[8].ToImmutableArray(), byte.MaxValue) { Prefix2793E08 = NativeBattleEffectArena.Clear() };
    public static NativeBattleEffectNodeScratch Unknown() => new(new byte[8].ToImmutableArray(), 0);

    public void Validate()
    {
        Prefix2793E08?.Validate();
        if (Prefix2793E08 is { } prefix && (prefix.Regions.Any(region => region.Offset + region.Data.Length > 40) || prefix.UnknownRegions.Any(region => region.Offset + region.Length > 40)))
        {
            throw new InvalidDataException("Native node scratch prefix exceeds its forty-byte physical owner.");
        }
        if (Bytes.IsDefault || Bytes.Length != 8)
        {
            throw new InvalidDataException("Native node scratch requires its retained eight-byte physical owner.");
        }
    }

    public byte[] ReadPrefix(int offset, int count)
    {
        RequirePrefixRange(offset, count);
        return (Prefix2793E08 ?? throw new NotSupportedException("Native node scratch prefix has no retained physical bytes.")).Read(offset, count);
    }

    public NativeBattleEffectNodeScratch WritePrefix(int offset, ReadOnlySpan<byte> bytes)
    {
        RequirePrefixRange(offset, bytes.Length);
        NativeBattleEffectArena prefix = Prefix2793E08 ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 40);
        return this with { Prefix2793E08 = prefix.Write(offset, bytes) };
    }

    public NativeBattleEffectNodeScratch WriteQuadMatrix(NativeBattleActionMatrix matrix)
    {
        matrix.Validate();
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
        }
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), matrix.Z);
        return WritePrefix(0, rotation).WritePrefix(20, translation);
    }

    private static void RequirePrefixRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > 40 - count)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }

    public NativeBattleVector ReadEuler()
    {
        Validate();
        if ((KnownMask & 0x3F) != 0x3F)
        {
            throw new NotSupportedException("Native placement selector1 requires the retained scratch XYZ words.");
        }
        return new(BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(4)));
    }

    public NativeBattleEffectNodeScratch WritePlacement(NativeBattleVector angles)
    {
        Validate();
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes, angles.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), angles.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), angles.Z);
        return this with { Bytes = bytes.ToImmutableArray(), KnownMask = (byte)(KnownMask | 0x3F) };
    }

    public NativeBattleEffectNodeScratch WriteFlags(uint flags)
    {
        Validate();
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, flags);
        return this with { Bytes = bytes.ToImmutableArray(), KnownMask = (byte)(KnownMask | 0x0F) };
    }

    public NativeBattleEffectNodeScratch WriteSpriteMatrix(NativeBattleActionMatrix matrix)
    {
        Validate();
        matrix.Validate();
        byte[] bytes = Bytes.ToArray();
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix.Rotation[index + 4]);
        }
        return this with { Bytes = bytes.ToImmutableArray(), KnownMask = byte.MaxValue };
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectNodeScratch? NodeScratch2793E30 { get; init; }
}
