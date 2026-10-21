using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFinalStage142State
{
    public void Validate()
    {
        ValidateBank(OverlayBank0);
        ValidateBank(OverlayBank1);
        if (ClutSource is { } source && (source.Data.IsDefault || source.Data.Length != 8192))
        {
            throw new InvalidDataException("Native final stage CLUT requires its actual8192-byte original slice.");
        }
    }

    public byte[] ReadOverlay(uint address, int length)
    {
        (int bank, int offset) = OverlayRange(address, length);
        Validate();
        return (bank == 0 ? OverlayBank0 : OverlayBank1).Read(offset, length);
    }

    public NativeBattleFinalStage142State WriteOverlay(uint address, ImmutableArray<byte> data, ImmutableArray<NativeBattleGfPacketByteState> mask)
    {
        if (data.IsDefault || mask.IsDefault || data.Length != mask.Length)
        {
            throw new InvalidDataException("Native final stage overlay stores require matching bytes and physical write masks.");
        }
        (int bank, int start) = OverlayRange(address, data.Length);
        Validate();
        NativeBattleEffectArena bytes = bank == 0 ? OverlayBank0 : OverlayBank1;
        for (int offset = 0; offset < data.Length;)
        {
            NativeBattleGfPacketByteState state = mask[offset];
            int end = offset + 1;
            while (end < data.Length && mask[end] == state)
            {
                end++;
            }
            switch (state)
            {
                case NativeBattleGfPacketByteState.Known:
                    bytes = bytes.Write(start + offset, data.AsSpan().Slice(offset, end - offset));
                    break;
                case NativeBattleGfPacketByteState.Unknown:
                    bytes = bytes.MarkUnknown(start + offset, end - offset);
                    break;
                case NativeBattleGfPacketByteState.Untouched:
                    break;
                default:
                    throw new InvalidDataException("Native final stage overlay physical write mask is invalid.");
            }
            offset = end;
        }
        return bank == 0 ? this with { OverlayBank0 = bytes } : this with { OverlayBank1 = bytes };
    }

    private static (int Bank, int Offset) OverlayRange(uint address, int length)
    {
        uint first = NativeBattleFinalStage142.OverlayBankAddress;
        if (length < 0 || address < first || (ulong)address + (uint)length > (ulong)first + 2048)
        {
            throw new InvalidDataException("Native final stage overlay access leaves its two physical packet banks.");
        }
        uint relative = address - first;
        int bank = (int)(relative / 1024);
        int offset = (int)(relative % 1024);
        if (bank > 1 || length > 1024 - offset)
        {
            throw new InvalidDataException("Native final stage overlay access crosses a physical packet-bank boundary.");
        }
        return (bank, offset);
    }

    private static void ValidateBank(NativeBattleEffectArena bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset > 1024 - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset > 1024 - region.Length))
        {
            throw new InvalidDataException("Native final stage overlay bank storage exceeds its original1024 bytes.");
        }
    }
}
