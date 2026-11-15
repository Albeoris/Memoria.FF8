namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool TryResolveNativeModelRead(uint address, int length, out int offset)
    {
        if (!NativeBattleGraphicsModelAddress.TryResolveOriginalField(address, out NativeBattleModelSlot slot, out offset))
        {
            return false;
        }
        return (uint)length <= (uint)((NativeBattleModelBoundary.CellCount - slot.Offset) * NativeBattleGraphicsModelAddress.RowSize - offset);
    }

    private static bool TryResolveNativeContactRead(uint address, int length, out BattleContactOccurrence occurrence, out int offset)
    {
        if (!NativeBattleContactAddress.TryResolveOriginalField(address, 0, out occurrence, out offset))
        {
            return false;
        }
        return (uint)length <= (uint)((NativeBattleContactAddress.NativeRowCount - occurrence.Ordinal) * NativeBattleContactAddress.RowSize - offset);
    }

    private byte[] ReadOriginalContactBytes(BattleContactOccurrence occurrence, int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (occurrence.Ordinal >= NativeBattleContactAddress.NativeRowCount || offset >= NativeBattleContactAddress.RowSize || length > NativeBattleContactAddress.RowSize - offset)
        {
            throw new InvalidDataException("The native contact byte span must stay within one original row.");
        }
        NativeBattleRawContactPacket packet = nativeActionPackets.ExportRetained().Contacts[occurrence.Ordinal] ??
            throw new NotSupportedException("Native contact memory requires an observed original row.");
        for (int index = offset; index < offset + length; index++)
        {
            if (packet.UnknownBitsAt(index) != 0)
            {
                throw new NotSupportedException("Native contact memory reaches an unobserved byte.");
            }
        }
        return NativeBattleContactPacketCodec.Encode(packet).AsSpan(offset, length).ToArray();
    }

    private bool TryReadOriginalHeaderBytes(uint address, int length, out byte[] bytes)
    {
        uint relative = unchecked(address - NativeBattleEffectParameterWindow.HeaderBase);
        if (relative >= NativeBattleRetainedActionPackets.HeaderCapacity * NativeBattleEffectParameterWindow.HeaderSize)
        {
            bytes = [];
            return false;
        }
        int offset = (int)(relative % NativeBattleEffectParameterWindow.HeaderSize);
        if (length < 0 || length > NativeBattleEffectParameterWindow.HeaderSize - offset)
        {
            throw new InvalidDataException("The native header byte span must stay within one original row.");
        }
        NativeBattleRetainedActionHeaderSlot header = nativeActionPackets.ExportRetained().Headers[(int)(relative / NativeBattleEffectParameterWindow.HeaderSize)];
        bytes = new byte[length];
        for (int index = 0; index < length; index++)
        {
            bytes[index] = header.ReadKnownByte(offset + index) ?? throw new NotSupportedException("Native action header memory reaches an unobserved byte.");
        }
        return true;
    }

    private byte ReadNativeDescriptorByte(NativeBattleGraphicsState graphics, uint address)
    {
        if (NativeBattleGraphicsModelAddress.TryResolveOriginalField(address, out NativeBattleModelSlot modelSlot, out int modelOffset))
        {
            int offset = modelOffset;
            NativeBattleGraphicsModel model = graphics.NativeBindings.ReadOriginalCell(modelSlot, graphics.ActorModels);
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
        if (TryReadOriginalHeaderBytes(address, 1, out byte[] headerBytes))
        {
            return headerBytes[0];
        }
        if (NativeBattleContactAddress.TryResolveOriginalField(address, 1, out BattleContactOccurrence contact, out int contactOffset))
        {
            return ReadOriginalContactBytes(contact, contactOffset, 1)[0];
        }
        int resourceRowCount = NativeModelCpuResources?.Rows.Length ?? NativeBattleModelResourceRowAddress.NativeRowCount;
        if (NativeBattleModelResourceRowAddress.TryResolveField(address, 1, out int alias, out int resourceOffset, resourceRowCount))
        {
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native descriptor memory requires its canonical CPU resource rows.");
            NativeBattleModelCpuResourceRow row = cpu.Rows[alias];
            int offset = resourceOffset;
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
            if (pointer is not null && NativeBattlePresentationCpuBanks.IsExtension(pointer.Bank))
            {
                throw new NotSupportedException("Hosted CPU resource sections have no numeric guest address.");
            }
            // Resource-owned bytes have separate storage, but retain their native placement.
            // Observe that placement here; do not use it to select the resource's backing owner.
            uint value = pointer is null ? 0 : pointer.TryPhysicalAddress() ?? (pointer.Bank < 3 ? checked(pointer.EffectiveLayout.BaseAddress + (uint)pointer.EffectiveLayout.BankLength * (uint)pointer.Bank + (uint)pointer.Offset) : pointer.NativePlacementAddress);
            return ByteOf(value, offset - field);
        }
        if (address >= NativeBattleModelResourceRowAddress.HostedBase && NativeBattleModelResourceRowAddress.TryResolveField(address, 1, out _, out _, NativeBattleModelResourceRowAddress.MaximumEncodedRowCount))
        {
            throw new NotSupportedException("Hosted resource row address leaves the owned CPU alias array.");
        }
        uint parameterOffset = unchecked(address - NativeBattleEffectParameterWindow.Address);
        if (parameterOffset < NativeBattleEffectParameterWindow.Length)
        {
            NativeBattleEffectParameterWindow window = graphics.OriginalEffect?.Parameters?.OriginalWindow ??
                throw new NotSupportedException("Native effect parameter memory requires its publishing writer's original byte projection.");
            return window.ReadByte((int)parameterOffset);
        }
        return (NativeFileStaging ?? throw new NotSupportedException("Native descriptor memory requires its addressed retained backing.")).ReadByte(address);

        static byte ByteOf(uint value, int offset)
        {
            return unchecked((byte)(value >> (8 * offset)));
        }
    }
}
