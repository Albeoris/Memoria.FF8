using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175WarpState([property: JsonRequired] NativeBattleEffectArena GridA, [property: JsonRequired] NativeBattleEffectArena GridB, [property: JsonRequired] NativeBattleEffectArena Faces, [property: JsonRequired] NativeBattleEffectArena PhysicalPackets, [property: JsonRequired] ImmutableArray<uint>? PointPointerCache)
{
    public const uint GridAAddress = 0x2392CE8;
    public const uint GridBAddress = 0x23756F0;
    public const uint FacesAddress = 0x2377748;
    public const uint PhysicalPacketsAddress = 0x23814E0;
    public const int GridALength = 315 * 16;
    public const int GridBLength = 280 * 16;
    public const int FacesLength = 1120 * 36;
    public const int PhysicalPacketsLength = 280 * 256;

    public static NativeBattleHoly175WarpState Unknown() => new(UnknownBuffer(GridALength), UnknownBuffer(GridBLength), UnknownBuffer(FacesLength), UnknownBuffer(PhysicalPacketsLength), null);

    public void Validate()
    {
        ValidateOwner(GridA, GridALength);
        ValidateOwner(GridB, GridBLength);
        ValidateOwner(Faces, FacesLength);
        ValidateOwner(PhysicalPackets, PhysicalPacketsLength);
        if (PointPointerCache is ImmutableArray<uint> cache && (cache.IsDefault || cache.Length != 5))
        {
            throw new InvalidDataException("Native Holy warp requires all five retained last-cell pointers when known.");
        }
    }

    public byte[] ReadPoint(uint address, int fieldOffset, int count)
    {
        if (fieldOffset < 0 || count < 0 || fieldOffset > 16 - count)
        {
            throw new InvalidDataException("Native Holy warp point field leaves its actual 16-byte row.");
        }
        if (address >= GridAAddress && address - GridAAddress < GridALength && (address - GridAAddress) % 16 == 0)
        {
            return GridA.Read(checked((int)(address - GridAAddress) + fieldOffset), count);
        }
        if (address >= GridBAddress && address - GridBAddress < GridBLength && (address - GridBAddress) % 16 == 0)
        {
            return GridB.Read(checked((int)(address - GridBAddress) + fieldOffset), count);
        }
        throw new InvalidDataException("Native Holy warp face selects an absent physical grid point.");
    }

    public static int PacketOffset(int cell, int triangle, int buffer)
    {
        if ((uint)cell >= 280 || (uint)triangle >= 4 || (uint)buffer >= 2)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "Native Holy warp packet requires an actual cell, triangle and buffer.");
        }
        return cell * 256 + triangle * 64 + buffer * 32;
    }

    private static NativeBattleEffectArena UnknownBuffer(int length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length);

    private static void ValidateOwner(NativeBattleEffectArena owner, int length)
    {
        ArgumentNullException.ThrowIfNull(owner);
        owner.Validate();
        owner.ValidateExtent(length);
    }
}
