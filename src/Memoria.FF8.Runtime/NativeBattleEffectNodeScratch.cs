using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectNodeScratch([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] byte KnownMask)
{
    public static NativeBattleEffectNodeScratch FromColdProcess() => new(new byte[8].ToImmutableArray(), byte.MaxValue);
    public static NativeBattleEffectNodeScratch Unknown() => new(new byte[8].ToImmutableArray(), 0);

    public void Validate()
    {
        if (Bytes.IsDefault || Bytes.Length != 8)
        {
            throw new InvalidDataException("Native node scratch requires its retained eight-byte physical owner.");
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
        return new(bytes.ToImmutableArray(), (byte)(KnownMask | 0x3F));
    }

    public NativeBattleEffectNodeScratch WriteFlags(uint flags)
    {
        Validate();
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, flags);
        return new(bytes.ToImmutableArray(), (byte)(KnownMask | 0x0F));
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
        return new(bytes.ToImmutableArray(), byte.MaxValue);
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectNodeScratch? NodeScratch2793E30 { get; init; }
}
