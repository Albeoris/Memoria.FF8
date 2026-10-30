using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceScrollMesh62BC70(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, ViewMatrix).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 144);
        NativeBattleEffectArena context = Context(frame, temporary, 144).Write(0, BitConverter.GetBytes(0xE163F8u)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(1024u)).Write(0x10, BitConverter.GetBytes(unchecked((uint)-age))).Write(0x14, BitConverter.GetBytes(unchecked((uint)age))).Write(0x18, BitConverter.GetBytes((ushort)0)).Write(0x1A, BitConverter.GetBytes((ushort)0)).Write(0x1C, BitConverter.GetBytes((ushort)128)).Write(0x1E, BitConverter.GetBytes((ushort)128)).Write(0x20, BitConverter.GetBytes(0xF3u)).Write(0x24, BitConverter.GetBytes(0x00808080u));
        NativeBattleWindowedMesh144Step drawn = NativeBattleWindowedMesh144.DrawLoaded(frame.Memory, resources.Mesh(0xE163F8), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, groupMask: (1 << 2) | (1 << 3) | (1 << 6) | (1 << 7), pilotIdentity: NativeBattleEffect326Presentation.Task(inputs, 0xE163F8));
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context144, 144);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(inputs.CapturePresentation ? NativeBattleEffect326Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch, 0x1D8E054));
        frame = Release(frame, 144);
        return IncrementMeshClock(frame, address, inputs, 26);
    }

    public static NativeBattleEffect326Step AdvanceFocalMesh62BDA0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
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
        short focal = unchecked((short)(inputs.ReadProjection(frame).FocalLength ?? throw new NotSupportedException("Effect326 focal mesh requires its current focal operand.")));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = focal };
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 0x1C), Signed(task, 0x1C), Signed(task, 0x20));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xE15D10u)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(2048u)).Write(0x1C, BitConverter.GetBytes(0xF3u));
        NativeBattleDefaultActMeshStep drawn = NativeBattleEffect326MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0xE15D10), inputs.ReadProjection(frame), context, inputs.Colors, 14, pilotIdentity: NativeBattleEffect326Presentation.Task(inputs, 0xE15D10));
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(inputs.CapturePresentation ? NativeBattleEffect326Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch, 0x1D8E054));
        frame = Release(frame, 88);
        if (!paused)
        {
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x1E)))));
        }
        return IncrementMeshClock(frame, address, inputs, 23);
    }

    private static NativeBattleEffect326Step IncrementMeshClock(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs, int end)
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
