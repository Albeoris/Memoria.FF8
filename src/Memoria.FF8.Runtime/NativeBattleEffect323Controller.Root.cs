using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect323Controller
{
    public static NativeBattleEffect323Step AdvanceRoot659B30(NativeBattleEffect323Frame frame, uint address, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool side = root.ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main != 0)
            {
                frame = ClearDwords701200(frame, main + 12, 0x2C);
                frame = CaptureRecipients(frame, main, inputs);
            }
        }
        int mains = 1;
        int meshes = 1;
        int programmes = 1;
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        if (initialized)
        {
            (frame, mains) = AdvancePool(frame, 1, resources, inputs);
            (frame, meshes) = AdvancePool(frame, 2, resources, inputs);
            (frame, programmes) = AdvancePool(frame, 3, resources, inputs);
            (frame, _) = AdvancePool(frame, 4, resources, inputs);
        }
        if (initialized && mains == 0 && meshes == 0 && programmes == 0)
        {
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    public static NativeBattleEffect323Frame CaptureRecipients(NativeBattleEffect323Frame frame, uint main, NativeBattleEffect323Inputs inputs)
    {
        int count = CurrentHeader(frame, inputs.ReadPackets(frame)).ReadByte(16);
        RequireRecipientCount(count);
        frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
        for (int index = 0; index < count; index++)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = CurrentHeader(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Effect323 requires its actual contact pointer.");
            uint model = ModelAddress(packets.ReadContact(first + index).Primary.ActorIndex);
            frame = Write(frame, main, Read(frame, main).WriteDword(0x20 + index * 4, model));
        }
        for (int left = 0; left < count - 1; left++)
        {
            for (int right = left + 1; right < count; right++)
            {
                NativeBattleModulePoolTask task = Read(frame, main);
                uint a = task.ReadDword(0x20 + left * 4);
                uint b = task.ReadDword(0x20 + right * 4);
                if (ModelX(frame, a, inputs) < ModelX(frame, b, inputs))
                {
                    frame = Write(frame, main, task.WriteDword(0x20 + left * 4, b).WriteDword(0x20 + right * 4, a));
                }
            }
        }
        frame = Write(frame, main, Read(frame, main).WriteWord(0x12, unchecked((ushort)-1000)).WriteWord(0x14, 1000).WriteWord(0x16, 0).WriteWord(0x18, 0));
        for (int index = 0; index < count; index++)
        {
            uint target = Read(frame, main).ReadDword(0x20 + index * 4);
            int offset = 0x2C + index * 8;
            NativeBattleEffect323PointStep anchor = inputs.Anchor571400(frame, target, TaskView(Read(frame, main), offset, 8));
            frame = Write(anchor.Frame, main, CopyTask(Read(anchor.Frame, main), offset, anchor.Output8, 8));
            NativeBattleVector point = Vector(anchor.Output8);
            NativeBattleModulePoolTask task = Read(frame, main);
            task = task.WriteWord(0x16, unchecked((ushort)(task.ReadWord(0x16) + point.Y))).WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + point.Z)));
            if (Signed(task, 0x12) > point.X)
            {
                task = task.WriteWord(0x12, unchecked((ushort)point.X));
            }
            if (Signed(task, 0x14) < point.X)
            {
                task = task.WriteWord(0x14, unchecked((ushort)point.X));
            }
            frame = Write(frame, main, task);
        }
        NativeBattleModulePoolTask current = Read(frame, main);
        int meanY = Signed(current, 0x16) / count;
        int meanZ = Signed(current, 0x18) / count;
        return Write(frame, main, current.WriteWord(0x12, unchecked((ushort)(current.ReadWord(0x12) - 1000))).WriteWord(0x14, unchecked((ushort)(current.ReadWord(0x14) + 1000))).WriteWord(0x16, unchecked((ushort)meanY)).WriteWord(0x18, unchecked((ushort)(meanZ - 1000))));
    }

    private static void RequireRecipientCount(int count)
    {
        if (count is < 1 or > 3)
        {
            throw new NotSupportedException("Effect323 requires one to three native recipients within its physical target and path arrays.");
        }
    }

    private static short ModelX(NativeBattleEffect323Frame frame, uint model, NativeBattleEffect323Inputs inputs) => BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, model).Read(0x1C, 2));

    private static NativeBattleActionHeaderSlot CurrentHeader(NativeBattleEffect323Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect323 requires its current action header.");
        return packets.Headers[parameters.HeaderIndex7C];
    }
}
