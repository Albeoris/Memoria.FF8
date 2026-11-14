using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

/// <summary>Retained bytes of one original contact half, independent of a current model binding.</summary>
public sealed record NativeBattleRawContactHalf(byte ActorIndex, byte Sequence, byte Flags2, byte HitFlags3, ushort SavedStatuses4, ushort Amount6, uint BattleStatuses8)
{
    public int? ExtendedDamageAmount { get; init; }
    public int PresentedAmount => ExtendedDamageAmount ?? Amount6;
    public ushort ContactFlags => (ushort)(Flags2 | HitFlags3 << 8);
}

public sealed record NativeBattleRawContactPacket(NativeBattleRawContactHalf Primary, NativeBattleRawContactHalf Secondary);

public static class NativeBattleContactPacketCodec
{
    private const int HalfSize = 12;

    public static NativeBattleRawContactPacket Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 2 * HalfSize)
        {
            throw new InvalidDataException("An original contact packet requires exactly 24 bytes.");
        }
        return new(ReadHalf(bytes), ReadHalf(bytes[HalfSize..]));
    }

    public static byte[] Encode(NativeBattleRawContactPacket raw)
    {
        Validate(raw);
        byte[] bytes = new byte[2 * HalfSize];
        WriteHalf(bytes, raw.Primary);
        WriteHalf(bytes.AsSpan(HalfSize), raw.Secondary);
        return bytes;
    }

    public static void Validate(NativeBattleRawContactPacket raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Primary is null || raw.Secondary is null)
        {
            throw new InvalidDataException("A raw contact packet requires both retained halves.");
        }
        NativeBattleContactHalf.ValidateExtendedDamage(raw.Primary.ExtendedDamageAmount, raw.Primary.HitFlags3, raw.Primary.Amount6);
        NativeBattleContactHalf.ValidateExtendedDamage(raw.Secondary.ExtendedDamageAmount, raw.Secondary.HitFlags3, raw.Secondary.Amount6);
    }

    public static NativeBattleContactPacket DecodeOriginal(NativeBattleRawContactPacket raw, NativeBattleModelBoundary bindings, BattleActorModels models)
    {
        Validate(raw);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(models);
        NativeBattleContactHalf DecodeHalf(NativeBattleRawContactHalf half)
        {
            BattleModelOwner recipient = bindings.DecodeLiteral(half.ActorIndex, models);
            BattleModelOwner? coverSource = (half.HitFlags3 & 8) == 0 ? null : bindings.DecodeLiteral((byte)(half.HitFlags3 >> 6), models);
            return new(recipient, half.Sequence, half.Flags2, (byte)(half.HitFlags3 & 0x3F), half.SavedStatuses4, half.Amount6, half.BattleStatuses8)
            {
                CoverSource = coverSource,
                ExtendedDamageAmount = half.ExtendedDamageAmount
            };
        }

        return new(DecodeHalf(raw.Primary), raw.Secondary.ActorIndex == byte.MaxValue ? null : DecodeHalf(raw.Secondary));
    }

    public static bool TryEncodeOriginal(NativeBattleContactPacket live, NativeBattleModelBoundary bindings, BattleActorModels models, [NotNullWhen(true)] out NativeBattleRawContactPacket? raw)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(models);
        live.Validate();
        raw = null;
        if (!TryEncodeHalf(live.Primary, bindings, models, out NativeBattleRawContactHalf? primary))
        {
            return false;
        }
        NativeBattleRawContactHalf? secondary = null;
        if (live.Secondary is { } present && !TryEncodeHalf(present, bindings, models, out secondary))
        {
            return false;
        }
        raw = new(primary, secondary ?? new(byte.MaxValue, 0, 0, 0, 0, 0, 0));
        return true;
    }

    private static bool TryEncodeHalf(NativeBattleContactHalf half, NativeBattleModelBoundary bindings, BattleActorModels models, [NotNullWhen(true)] out NativeBattleRawContactHalf? raw)
    {
        raw = null;
        if (!bindings.TryEncodeExactActor(half.Recipient, models, out byte recipient))
        {
            return false;
        }
        byte hitFlags = half.HitFlags;
        if (half.CoverSource is { } cover)
        {
            if (!bindings.TryEncodeExactActor(cover, models, out byte source) || source > 3)
            {
                return false;
            }
            hitFlags |= (byte)(source << 6);
        }
        raw = new(recipient, half.Sequence, half.Flags2, hitFlags, half.SavedStatuses4, half.Amount6, half.BattleStatuses8) { ExtendedDamageAmount = half.ExtendedDamageAmount };
        return true;
    }

    private static NativeBattleRawContactHalf ReadHalf(ReadOnlySpan<byte> bytes)
    {
        return new(bytes[0], bytes[1], bytes[2], bytes[3], BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]), BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]));
    }

    private static void WriteHalf(Span<byte> bytes, NativeBattleRawContactHalf half)
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
