namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private byte ReadNativeDescriptorByte(NativeBattleGraphicsState graphics, uint address)
    {
        if (address >= 0x1D972C0 && address < 0x1D972C0 + 7 * 156)
        {
            uint relative = address - 0x1D972C0;
            NativeBattleGraphicsModel model = graphics.Models[checked((int)(relative / 156))];
            int offset = checked((int)(relative % 156));
            if (offset == 4)
            {
                return model.ModelId4 ?? throw new NotSupportedException("Native descriptor memory requires its retained model identity byte.");
            }
            if (offset == 5)
            {
                return model.Kind5 ?? throw new NotSupportedException("Native descriptor memory requires its retained model service byte.");
            }
            if (offset is >= 8 and < 12)
            {
                return ByteOf(model.Flags8 ?? throw new NotSupportedException("Native descriptor memory requires its retained model status DWORD."), offset - 8);
            }
            return CaptureNativeEffect301Model(model).ReadByte(offset);
        }
        if (address >= 0x1D96AB8 && address < 0x1D96AB8 + 30 * 12)
        {
            uint relative = address - 0x1D96AB8;
            NativeBattleDescriptorPacket packet = graphics.Descriptors.Packets[checked((int)(relative / 12))];
            int offset = checked((int)(relative % 12));
            if ((packet.UnknownByteMask & (1 << offset)) != 0)
            {
                throw new NotSupportedException("Native descriptor memory cannot read an unknown physical text pointer byte.");
            }
            return packet.Bytes[offset];
        }
        if (address >= 0x1D96C20 && address < 0x1D96C20 + 30 * 8)
        {
            uint relative = address - 0x1D96C20;
            NativeBattleDescriptorNode node = graphics.Descriptors.Nodes[checked((int)(relative / 8))];
            int offset = checked((int)(relative % 8));
            uint value = node.Data;
            if (offset < 4)
            {
                value = node.Next is int slot ? checked(0x1D96C20u + (uint)slot * 8) : 0;
            }
            return ByteOf(value, offset % 4);
        }
        if (address >= 0x1D96D18 && address < 0x1D96D18 + 4 * 20)
        {
            uint relative = address - 0x1D96D18;
            NativeBattleDescriptorTask task = graphics.Descriptors.Tasks[checked((int)(relative / 20))];
            int offset = checked((int)(relative % 20));
            if (offset < 2)
            {
                return ByteOf(task.Flags, offset);
            }
            if (offset < 4)
            {
                return ByteOf(task.State, offset - 2);
            }
            if (offset < 8)
            {
                uint next = task.Next is int slot ? checked(0x1D96D18u + (uint)slot * 20) : 0;
                return ByteOf(next, offset - 4);
            }
            if (offset < 12)
            {
                throw new NotSupportedException("Native descriptor memory requires its actual retained task callback pointer bytes.");
            }
            return task.Payload[offset - 12];
        }
        if (address >= 0x1D280C4 && address < 0x1D28344)
        {
            uint relative = address - 0x1D280C4;
            return nativeActionPackets.Headers[checked((int)(relative / 20))].ReadByte(checked((int)(relative % 20)));
        }
        if (address >= NativeBattleActionPacketsState.ContactBase && address < NativeBattleActionPacketsState.ContactBase + NativeBattleActionPacketsState.ContactCapacity * 24)
        {
            uint relative = address - NativeBattleActionPacketsState.ContactBase;
            byte[] contact = nativeActionPackets.ReadContact(checked((int)(relative / 24))).Encode();
            return contact[checked((int)(relative % 24))];
        }
        if (address >= 0x1D99774 && address < 0x1D99774 + 11 * 52)
        {
            uint relative = address - 0x1D99774;
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native descriptor memory requires its canonical CPU resource rows.");
            NativeBattleModelCpuResourceRow row = cpu.Rows[checked((int)(relative / 52))];
            int offset = checked((int)(relative % 52));
            int field = offset & ~3;
            if (field == 4 && row.PrivateLength4 is int privateLength)
            {
                return ByteOf(unchecked((uint)privateLength), offset - field);
            }
            if (field == 8 && row.SharedLength8 is int sharedLength)
            {
                return ByteOf(unchecked((uint)sharedLength), offset - field);
            }
            if (offset == 36 && row.LinkedPhysicalIndex36 is byte linked)
            {
                return linked;
            }
            if (!row.Fields.TryGetValue(field, out NativeBattleModelCpuReference? pointer))
            {
                throw new NotSupportedException("Native descriptor memory reaches an unknown retained CPU resource-row byte.");
            }
            // Resource-owned bytes have separate storage, but retain their native placement.
            // Observe that placement here; do not use it to select the resource's backing owner.
            uint value = pointer is null ? 0 : pointer.TryPhysicalAddress() ?? (pointer.Bank < 3 ? checked(pointer.EffectiveLayout.BaseAddress + (uint)pointer.EffectiveLayout.BankLength * pointer.Bank + (uint)pointer.Offset) : pointer.NativePlacementAddress);
            return ByteOf(value, offset - field);
        }
        if (address >= 0x1D99A78 && address < 0x1D99A80 && graphics.OriginalEffect?.Parameters is { } parameters)
        {
            int offset = checked((int)(address - 0x1D99A78));
            return offset switch
            {
                0 => parameters.Actor78,
                1 => parameters.Flags79,
                2 or 3 => ByteOf(parameters.TargetMask7A, offset - 2),
                _ => ByteOf(checked(0x1D280C4u + (uint)parameters.HeaderIndex7C * 20), offset - 4)
            };
        }
        return (NativeFileStaging ?? throw new NotSupportedException("Native descriptor memory requires its addressed retained backing.")).ReadByte(address);

        static byte ByteOf(uint value, int offset)
        {
            return unchecked((byte)(value >> (8 * offset)));
        }
    }
}
