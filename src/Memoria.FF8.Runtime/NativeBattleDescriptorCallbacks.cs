namespace Memoria.FF8.Runtime;

public static class NativeBattleDescriptorCallbacks
{
    public static bool IsBlocked502F30(IReadOnlyList<NativeBattleGraphicsModel> models, short index, Func<uint, byte> readByte, NativeBattleGraphicsRoster? roster = null)
    {
        roster?.Validate();
        if (models.Count != (roster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount))
        {
            throw new InvalidDataException("Native502F30 requires the graphics records owned by its model bank.");
        }
        if ((uint)index < (uint)models.Count && (roster is null || roster.Rows[index] is not null))
        {
            NativeBattleGraphicsModel model = models[index];
            return (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40, 0x1A);
        }
        // WORD8 survives packet reuse. 502F30 sign-extends it before addressing memory,
        // even when the callback itself does not consume a model index.
        uint address = checked((uint)(0x1D972C0 + NativeBattleGraphicsModelAddress.RowSize * index));
        byte flags = readByte(address);
        if ((flags & 2) == 0 || (flags & 8) != 0)
        {
            return false;
        }
        if ((ReadUInt32(address + 8) & 0x1A) != 0)
        {
            return false;
        }
        if (readByte(address + 5) != 0)
        {
            return true;
        }
        uint resource = ReadUInt32(address + 116);
        int resourceFlags = readByte(checked(resource + 44)) | readByte(checked(resource + 45)) << 8;
        return (resourceFlags & 0x40) == 0;

        uint ReadUInt32(uint pointer)
        {
            return (uint)(readByte(pointer) | readByte(pointer + 1) << 8 | readByte(pointer + 2) << 16 | readByte(pointer + 3) << 24);
        }
    }
}
