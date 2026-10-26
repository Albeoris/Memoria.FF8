using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceScrollMesh6485C0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, ViewMatrix).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 124);
        NativeBattleEffectArena context = Context(frame, temporary, 124).Write(0, BitConverter.GetBytes(0xE40848u)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(1024u)).Write(0x10, BitConverter.GetBytes(unchecked((uint)-age))).Write(0x14, BitConverter.GetBytes(unchecked((uint)age))).Write(0x18, BitConverter.GetBytes((ushort)0)).Write(0x1A, BitConverter.GetBytes((ushort)0)).Write(0x1C, BitConverter.GetBytes((ushort)128)).Write(0x1E, BitConverter.GetBytes((ushort)128)).Write(0x20, BitConverter.GetBytes(0xF3u));
        NativeBattleScrollingMeshContext124Step drawn = NativeBattleScrollingMeshContext124.DrawLoaded(frame.Memory, resources.Mesh(0xE40848), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context124, 124);
        frame = inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(drawn.Batch, 0x1D8E054));
        frame = Release(frame, 124);
        return IncrementMeshClock(frame, address, inputs, 26);
    }

    public static NativeBattleEffect187Step AdvanceFocalMesh6486F0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool paused = (inputs.ReadFlags(frame) & 0x201) != 0;
        if (Signed(task, 14) > 0)
        {
            if (!paused)
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))));
            }
            return new(frame, 0);
        }
        short focal = unchecked((short)(inputs.ReadProjection(frame).FocalLength ?? throw new NotSupportedException("Effect187 focal mesh requires its current focal operand.")));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = focal };
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 0x1C), Signed(task, 0x1C), Signed(task, 0x20));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xE40160u)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(2048u)).Write(0x1C, BitConverter.GetBytes(0xF3u));
        NativeBattleDefaultActMeshStep drawn = NativeBattleEffect187MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0xE40160), inputs.ReadProjection(frame), context, inputs.Colors, 14);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(drawn.Batch, 0x1D8E054));
        frame = Release(frame, 88);
        if (!paused)
        {
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x1E)))));
        }
        return IncrementMeshClock(frame, address, inputs, 23);
    }

    private static NativeBattleEffect187Step IncrementMeshClock(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs, int end)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, next));
        return new(frame, unchecked((short)next) >= end ? 2 : 0);
    }
}
