using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect213Controller
{
    private const int PhysicalTargetCount = 3;

    public static NativeBattleEffect213Step AdvanceRoot69EB70(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool side = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(ArenaGlobal) + (uint)(side ? FirstPacketOffset : SecondPacketOffset)) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (Signed(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
                frame = InitializeStreamCopy(frame);
                foreach (NativeBattleCureFamilyPool shape in CommonPools)
                {
                    NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                    frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
                }
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main != 0)
            {
                frame = ClearDwords701200(frame, main + 12, 0x13E);
                frame = InitializeTargets(frame, main, inputs);
            }
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        int survivors = 0;
        if (initialized)
        {
            for (int index = 1; index <= 4; index++)
            {
                (frame, int count) = AdvancePool(frame, index, resources, inputs);
                survivors += count;
            }
        }
        if (initialized && survivors == 0)
        {
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)Math.Min(Signed(Read(frame, address), 12) + 1, 10)))), 0);
    }

    public static NativeBattleEffect213Frame InitializeStreamCopy(NativeBattleEffect213Frame frame)
    {
        NativeBattleEffectArena source = ReadBytes(frame, MutableStreamAddress, MutableStreamLength);
        int minimum = short.MaxValue;
        int maximum = short.MinValue;
        for (int index = 0; index < StreamVertexCount; index++)
        {
            short z = BinaryPrimitives.ReadInt16LittleEndian(source.Read(index * 8 + 4, 2));
            minimum = Math.Min(minimum, z);
            maximum = Math.Max(maximum, z);
        }
        int range = maximum - minimum;
        NativeBattleEffectArena copy = CopyBytes(source, 0, NativeBattleEffectArena.Clear(), 0, MutableStreamLength);
        for (int index = 0; index < StreamVertexCount; index++)
        {
            int z = BinaryPrimitives.ReadInt16LittleEndian(copy.Read(index * 8 + 4, 2));
            copy = copy.Write(index * 8 + 6, BitConverter.GetBytes(unchecked((short)((z << 12) / range))));
        }
        return WriteBytes(frame, frame.State.ReadDword(ArenaGlobal) + StreamCopyOffset, copy, MutableStreamLength);
    }

    public static NativeBattleEffect213Frame InitializeTargets(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect213 requires its current contact header.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
        BattleContactCursor first = header.FirstContact;
        int count = header.ReadContactCount();
        if (count == 0)
        {
            throw new NotSupportedException("Effect213 requires actual captured targets.");
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(14, checked((ushort)count));
        BattleModelOwner[] recipients = new BattleModelOwner[count];
        short[] positions = new short[count];
        int[] order = new int[count];
        for (int index = 0; index < count; index++)
        {
            BattleModelOwner recipient = packets.ReadContact(first.Advance(index)).Primary.Recipient;
            recipients[index] = recipient;
            positions[index] = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, recipient).Read(0x1C, 2));
            order[index] = index;
        }
        for (int outer = 0; outer < count - 1; outer++)
        {
            for (int inner = outer + 1; inner < count; inner++)
            {
                if (positions[order[outer]] < positions[order[inner]])
                {
                    (order[outer], order[inner]) = (order[inner], order[outer]);
                }
            }
        }
        for (int index = 0; index < Math.Min(count, PhysicalTargetCount); index++)
        {
            int ordinal = order[index];
            BattleModelOwner recipient = recipients[ordinal];
            task.WriteExecutionReference(16 + index * 4, 4, new BattleExecutionReference.Model(recipient), packets.ModelAliases.EncodeAddress(recipient));
            task = task.WriteByte(0x1C + index, checked((byte)ordinal));
        }
        task = task.WriteWord(0x22, count > 1 ? (ushort)1 : (ushort)0).WriteWord(0x26, unchecked((ushort)((count << 12) / 40)));
        frame = Write(frame, address, task);
        frame = frame with { State = frame.State with { ExtendedTargetOrder = count > PhysicalTargetCount ? order.ToImmutableArray() : default, FinalContactDispatched = false } };
        NativeBattleEffect213CountStep counted = inputs.CountVertices69EE80(frame, recipients[order[0]]);
        return Write(counted.Frame, address, Read(counted.Frame, address).WriteWord(0x2C, unchecked((ushort)counted.Count)));
    }

    private static int TargetOrdinal(NativeBattleEffect213Frame frame, NativeBattleModulePoolTask task, int index)
    {
        return index < PhysicalTargetCount ? task.ReadByte(0x1C + index) : frame.State.ExtendedTargetOrder[index];
    }

    private static BattleModelOwner TargetOwner(NativeBattleEffect213Frame frame, NativeBattleModulePoolTask task, int index, NativeBattleEffect213Inputs inputs)
    {
        if (index < PhysicalTargetCount)
        {
            return task.ReadExecutionReference<BattleExecutionReference.Model>(16 + index * 4, 4).Owner;
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect213 requires its actual contact header.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        BattleContactCursor cursor = packets.ReadHeader(parameters.Header).FirstContact.Advance(TargetOrdinal(frame, task, index));
        return packets.ReadContact(cursor).Primary.Recipient;
    }
}
