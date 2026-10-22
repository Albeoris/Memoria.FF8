using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshZantetsukenActors([property: JsonRequired] NativeBattleEffectArena Bytes)
{
    public const uint Address = 0x2200B7C;
    public const int ActorCount = 7;
    public const int RecordStride = 60;
    public const int Length = ActorCount * RecordStride;

    public static NativeBattleGilgameshZantetsukenActors FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleGilgameshZantetsukenActors FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));

    public static NativeBattleGilgameshZantetsukenActors FromColdProcess(ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault || bytes.Length != Length)
        {
            throw new InvalidDataException("Zantetsuken requires its seven complete retained actor records.");
        }
        return new(NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan()));
    }

    public void Validate()
    {
        if (Bytes is null)
        {
            throw new InvalidDataException("Zantetsuken lost its retained actor memory.");
        }
        Bytes.Validate();
        if (Bytes.Regions.Any(region => region.Offset > Length - region.Data.Length) || Bytes.UnknownRegions.Any(region => region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Zantetsuken actor storage leaves its original seven records.");
        }
    }

    public short ReadWord(byte actor, int offset)
    {
        Validate();
        return BinaryPrimitives.ReadInt16LittleEndian(Bytes.Read(Offset(actor, offset, 2), 2));
    }

    public NativeBattleGilgameshZantetsukenActors WithWord(byte actor, int offset, short value)
    {
        Validate();
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return this with { Bytes = Bytes.Write(Offset(actor, offset, 2), bytes) };
    }

    public NativeBattlePackedMatrix32 ReadMatrix(byte actor)
    {
        Validate();
        (byte[] bytes, bool[] known) = Bytes.ReadSnapshot(Offset(actor, 24, 32), 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public NativeBattleGilgameshZantetsukenActors WithMatrix(byte actor, NativeBattlePackedMatrix32 matrix)
    {
        Validate();
        matrix.Validate();
        int offset = Offset(actor, 24, 32);
        NativeBattleEffectArena bytes = Bytes.Write(offset, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                bytes = bytes.MarkUnknown(offset + index, 1);
            }
        }
        return this with { Bytes = bytes };
    }

    private static int Offset(byte actor, int offset, int length)
    {
        if (actor >= ActorCount || offset < 0 || length < 0 || offset > RecordStride - length)
        {
            throw new InvalidDataException("Zantetsuken actor access leaves its physical record.");
        }
        return actor * RecordStride + offset;
    }
}
