using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private static uint NativeEffect316CommonBase(NativeBattlePrimitiveProcess process)
    {
        uint bank = process.PacketBank switch
        {
            "off_B6D070" => 0x1901018,
            "off_B6D094" => 0x199B018,
            _ => throw new NotSupportedException("Effect316 requires its actual COMMON packet bank.")
        };
        return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity);
    }

    private NativeBattleEffectArena ReadNativeEffect316Arena(NativeBattleGraphicsState graphics, NativeBattleEffect316Frame frame, uint address, int length)
    {
        if (address == 0x1D8E054 && length == 4)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect316 requires its current COMMON packet process.");
            return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(checked(NativeEffect316CommonBase(process) + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect316 requires its actual COMMON cursor.")))));
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect316 requires its current CPU resources.");
        if (address == 0x1D98B3C && length == 4)
        {
            int size = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Effect316 requires its original loaded scratch capacity.")) * 8);
            return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(checked(0x196B018u - (uint)size)));
        }
        if (TryResolveNativeModelRead(address, length, out int modelOffset))
        {
            if (length > NativeBattleGraphicsModelAddress.RowSize - modelOffset)
            {
                throw new InvalidDataException("Effect316 model read crosses a physical model row.");
            }
            (NativeBattleModelSlot slot, int offset) = NativeBattleGraphicsModelAddress.ResolveOriginalField(address, length);
            return NativeBattleEffectNodeContextMemory.Copy(CaptureNativeEffect301Model(graphics.NativeBindings.ReadOriginalCell(slot, graphics.ActorModels)), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            NativeBattleGraphicsModel model = entry.State;
            if (model.ActiveResourceRow100 is not int rowIndex || model.CurrentBodyBoneMatrices is not { } bones)
            {
                continue;
            }
            NativeBattleModelCpuResourceRow row = cpu.Rows[rowIndex];
            if (!row.Fields.TryGetValue(12, out NativeBattleModelCpuReference? skeleton) || skeleton is null)
            {
                continue;
            }
            if (skeleton.TryPhysicalAddress() is not uint skeletonAddress)
            {
                continue;
            }
            uint first = checked(skeletonAddress + 32);
            long relative = (long)address - first;
            if (relative < 0 || relative >= bones.Matrices.Length * 48)
            {
                continue;
            }
            int bone = (int)(relative / 48);
            int offset = (int)(relative % 48);
            if (length > 48 - offset)
            {
                throw new InvalidDataException("Effect316 matrix read crosses its native bone record.");
            }
            NativeBattleEffectArena record = ReadNativeEffect316Physical(frame, skeletonAddress + 32 + (uint)bone * 48, 48);
            NativeBattleActionMatrix matrix = bones.Matrices[bone];
            for (int lane = 0; lane < 9; lane++)
            {
                record = record.Write(lane * 2, BitConverter.GetBytes(matrix.Rotation[lane]));
            }
            record = record.Write(20, BitConverter.GetBytes(matrix.X)).Write(24, BitConverter.GetBytes(matrix.Y)).Write(28, BitConverter.GetBytes(matrix.Z));
            return NativeBattleEffectNodeContextMemory.Copy(record, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return ReadNativeEffect316Physical(frame, address, length);
    }

    private NativeBattleEffectArena ReadNativeEffect316Physical(NativeBattleEffect316Frame frame, uint address, int length)
    {
        for (int bank = 0; bank < 3; bank++)
        {
            NativeBattlePartyCpuLayout layout = (NativeModelCpuResources ?? throw new NotSupportedException("The effect requires its current CPU allocation.")).Cpu.EffectiveLayout;
            uint first = checked(layout.BaseAddress + (uint)(bank * layout.BankLength));
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + (uint)layout.BankLength)
            {
                NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect316 requires its physical CPU bank.");
                (byte[] data, bool[] known) = NativeBattleModelResourceView.Open(cpu, frame.Staging!, new NativeBattleModelCpuReference((byte)bank, checked((int)(address - first))) { Layout = layout }, length).ReadSnapshot(0, length);
                return NativeBattleEffectArena.Clear().WriteWithKnownness(0, data, known);
            }
        }
        return NativeBattleEffect316Controller.ReadBytes(frame, address, length);
    }

    private NativeBattleEffect316Frame WriteNativeEffect316Arena(NativeBattleEffect316Frame frame, uint address, NativeBattleEffectArena data, int length)
    {
        for (int bank = 0; bank < 3; bank++)
        {
            NativeBattlePartyCpuLayout layout = (NativeModelCpuResources ?? throw new NotSupportedException("The effect requires its current CPU allocation.")).Cpu.EffectiveLayout;
            uint first = checked(layout.BaseAddress + (uint)(bank * layout.BankLength));
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + (uint)layout.BankLength)
            {
                NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect316 requires its physical CPU bank.");
                NativeBattleModelCpuBank updated = cpu.Cpu.Banks[bank].Write(checked((int)(address - first)), data.Read(0, length));
                NativeModelCpuResources = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(bank, updated) } };
                return frame;
            }
        }
        return NativeBattleEffect316Controller.WriteBytes(frame, address, data, length);
    }

    private NativeBattleEffect316Frame DeliverNativeEffect316Batch(ref NativeBattleGraphicsState graphics, NativeBattleEffect316Frame frame, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags = 0)
    {
        batch = NativeBattleActor33Presentation.Depths(batch);
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect316 requires its current ordering table.");
        if (cursorGlobal == 0x1D8E054)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect316 requires its current COMMON packet process.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Effect316 requires its current COMMON packet cursor.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, process.CommonPacketWriteCapacity, index =>
            {
                (byte[] bytes, bool[] known) = process.ReadCommonPacketSnapshot(index, 1);
                return (bytes[0], known[0]);
            });
            var owned = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, cursor, resolved);
            NativeBattleCommonPacketWrite delivered = NativeBattleCommonPacketArena.Append(process, primitive.Pending, resolved);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = delivered.Ordering } };
            return frame with { Effect = frame.Effect with { PresentationPackets = owned }, Memory = frame.Memory with { PrimitiveProcess = delivered.Process } };
        }
        if (cursorGlobal != CursorGlobal)
        {
            throw new NotSupportedException("Effect316 packet delivery requires its actual cursor owner.");
        }
        int privateCursor = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect316 requires its current packet arena.");
        NativeBattleGfGeometryBatch privateBatch = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, privateCursor, NativeBattleEffectArena.Length, index =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(index, 1);
            return (bytes[0], known[0]);
        });
        var privateOwned = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "20DFAB8", 0, privateCursor, privateBatch);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, privateCursor, primitive.Pending, privateBatch, flags);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Effect = frame.Effect with { PresentationPackets = privateOwned }, Memory = written.Memory, State = frame.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
    }
}
