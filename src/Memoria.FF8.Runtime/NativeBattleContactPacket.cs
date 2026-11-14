using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One12-byte half emitted by48EF80 from the48FE20/494410 scratch owners.</summary>
public sealed record NativeBattleContactHalf(byte ActorIndex, byte Sequence, byte Flags2, byte HitFlags3, ushort SavedStatuses4, ushort Amount6, uint BattleStatuses8)
{
    public int? ExtendedDamageAmount { get; init; }
    public int PresentedAmount => ExtendedDamageAmount ?? Amount6;

    public static NativeBattleContactHalf FromScratch(byte actorIndex, byte sequence, byte flags2, byte hitFlags3, ushort savedStatuses, int amountDword, uint battleStatuses)
    {
        //48EF80 copies only the lowWORD of signed/nativeDWORD damage; the formula consumer owns its earlier cap/sign rules.
        return new(actorIndex, sequence, flags2, hitFlags3, savedStatuses, unchecked((ushort)amountDword), battleStatuses);
    }

    public ushort ContactFlags => (ushort)(Flags2 | HitFlags3 << 8);
}

/// <summary>Canonical24-byte contact packet consumed by506690; secondary ActorIndexFF means absent.</summary>
public sealed record NativeBattleContactPacket(NativeBattleContactHalf Primary, NativeBattleContactHalf Secondary)
{
    [JsonIgnore]
    public int? PhysicalContactIndex { get; init; }

    public bool Equals(NativeBattleContactPacket? other)
    {
        return other is not null && Primary == other.Primary && Secondary == other.Secondary;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Primary, Secondary);
    }

    public void Validate(int actorCount = NativeBattleGraphicsModelAddress.NativeRowCount)
    {
        if (actorCount is < 1 or > NativeBattleGraphicsModelAddress.MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(actorCount));
        }
        if (Primary is null || Secondary is null || Primary.ActorIndex >= actorCount || Secondary.ActorIndex != 255 && Secondary.ActorIndex >= actorCount)
        {
            throw new InvalidDataException("Native contact packet requires primary and optional secondary indices within its model bank.");
        }
        foreach (NativeBattleContactHalf half in new[] { Primary, Secondary })
        {
            if (half.ExtendedDamageAmount is int amount && (amount is <= short.MaxValue or > 100000 || (half.HitFlags3 & 1) != 0 || half.Amount6 != unchecked((ushort)amount)))
            {
                throw new InvalidDataException("Extended contact damage must preserve its native low WORD and positive damage identity.");
            }
        }
    }

    public static NativeBattleContactPacket Decode(ReadOnlySpan<byte> bytes, int actorCount = NativeBattleGraphicsModelAddress.NativeRowCount)
    {
        if (bytes.Length != 24)
        {
            throw new InvalidDataException("Native48EF80 contact packet requires exactly24 bytes.");
        }
        NativeBattleContactPacket result = new(ReadHalf(bytes), ReadHalf(bytes[12..]));
        result.Validate(actorCount);
        return result;
    }

    public byte[] Encode(int actorCount = NativeBattleGraphicsModelAddress.NativeRowCount)
    {
        Validate(actorCount);
        byte[] result = new byte[24];
        WriteHalf(result, Primary);
        WriteHalf(result.AsSpan(12), Secondary);
        return result;
    }

    private static NativeBattleContactHalf ReadHalf(ReadOnlySpan<byte> bytes)
    {
        return new(bytes[0], bytes[1], bytes[2], bytes[3], BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]));
    }

    private static void WriteHalf(Span<byte> bytes, NativeBattleContactHalf half)
    {
        bytes[0] = half.ActorIndex;
        bytes[1] = half.Sequence;
        bytes[2] = half.Flags2;
        bytes[3] = half.HitFlags3;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[4..], half.SavedStatuses4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[6..], half.Amount6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], half.BattleStatuses8);
    }
}
