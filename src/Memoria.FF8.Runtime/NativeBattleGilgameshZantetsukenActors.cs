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
        Bytes.ValidateExtent(Length);
    }

    internal static short ReadWord(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));

    internal static NativeBattleEffectArena WithWord(NativeBattleEffectArena row, int offset, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return row.Write(offset, bytes);
    }

    internal static NativeBattlePackedMatrix32 ReadMatrix(NativeBattleEffectArena row)
    {
        (byte[] bytes, bool[] known) = row.ReadSnapshot(24, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    internal static NativeBattleEffectArena WithMatrix(NativeBattleEffectArena row, NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        return WriteKnownBytes(row, 24, matrix.Bytes.AsSpan(), matrix.Known.AsSpan());
    }

    internal NativeBattleEffectArena ReadRow(byte actor)
    {
        Validate();
        (byte[] data, bool[] known) = Bytes.ReadSnapshot(Offset(actor, 0, RecordStride), RecordStride);
        return WriteKnownBytes(NativeBattleEffectArena.Clear(), 0, data, known);
    }

    internal NativeBattleGilgameshZantetsukenActors WithRow(byte actor, NativeBattleEffectArena row)
    {
        Validate();
        (byte[] data, bool[] known) = row.ReadSnapshot(0, RecordStride);
        return this with { Bytes = WriteKnownBytes(Bytes, Offset(actor, 0, RecordStride), data, known) };
    }

    private static NativeBattleEffectArena WriteKnownBytes(NativeBattleEffectArena arena, int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        arena = arena.Write(offset, bytes);
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                arena = arena.MarkUnknown(offset + index, 1);
            }
        }
        return arena;
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

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    internal ImmutableDictionary<BattleModelOwner, NativeBattleEffectArena> ZantetsukenTransforms { get; init; } = ImmutableDictionary<BattleModelOwner, NativeBattleEffectArena>.Empty;
}
