using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
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
        if (address >= 0x1D972C0 && (ulong)address + (uint)length <= 0x1D972C0ul + (uint)graphics.Models.Length * 156)
        {
            int actor = checked((int)((address - 0x1D972C0) / 156));
            int offset = checked((int)((address - 0x1D972C0) % 156));
            if (length > 156 - offset)
            {
                throw new InvalidDataException("Effect316 model read crosses a physical model row.");
            }
            return NativeBattleEffectNodeContextMemory.Copy(CaptureNativeEffect301Model(graphics.Models[actor]), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        for (int actor = 0; actor < graphics.Models.Length; actor++)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (model.ActiveResourceRow100 is not int rowIndex || model.CurrentBodyBoneMatrices is not { } bones)
            {
                continue;
            }
            NativeBattleModelCpuResourceRow row = cpu.Rows[rowIndex];
            if (!row.Fields.TryGetValue(12, out NativeBattleModelCpuReference? skeleton) || skeleton is null)
            {
                continue;
            }
            uint first = checked(skeleton.PhysicalAddress + 32);
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
            NativeBattleEffectArena record = ReadNativeEffect316Physical(frame, skeleton.PhysicalAddress + 32 + (uint)bone * 48, 48);
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
            uint first = 0x196B018u + (uint)bank * 0x10000;
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + 0x10000)
            {
                NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect316 requires its physical CPU bank.");
                return NativeBattleEffectArena.Clear().Write(0, cpu.Cpu.Banks[bank].Read(checked((int)(address - first)), length));
            }
        }
        return NativeBattleEffect316Controller.ReadBytes(frame, address, length);
    }

    private NativeBattleEffect316Frame WriteNativeEffect316Arena(NativeBattleEffect316Frame frame, uint address, NativeBattleEffectArena data, int length)
    {
        for (int bank = 0; bank < 3; bank++)
        {
            uint first = 0x196B018u + (uint)bank * 0x10000;
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + 0x10000)
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
