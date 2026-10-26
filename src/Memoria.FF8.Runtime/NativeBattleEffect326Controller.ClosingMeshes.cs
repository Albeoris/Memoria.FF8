namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame InitializeActorBurst62C180(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs)
    {
        for (int index = 0; index < 4; index++)
        {
            (frame, uint address) = AppendRequired326(frame, 0x62C2F0);
            bool second = (index & 1) != 0;
            bool opposite = index >= 2;
            NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0).WriteWord(14, second ? (ushort)1 : (ushort)0).WriteWord(0x10, 0).WriteWord(0x12, unchecked((ushort)(opposite ? -12450 : -12000))).WriteWord(0x14, unchecked((ushort)(second ? -1100 : -4500))).WriteWord(0x16, opposite ? (ushort)0x800 : (ushort)0).WriteWord(0x18, 0).WriteWord(0x1C, 0x3000).WriteWord(0x1E, unchecked((ushort)(second ? 33 : -33))).WriteWord(0x20, unchecked((ushort)(second ? 33 : -33))).WriteWord(0x22, unchecked((ushort)(second ? -40 : 50)));
            frame = Write(frame, address, task);
        }
        (frame, uint center) = AppendRequired326(frame, 0x62C430);
        return Write(frame, center, Read(frame, center).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xCF2C).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0).WriteWord(0x1C, 0x120).WriteWord(0x1E, 0x120).WriteWord(0x20, 0x4800));
    }

    public static NativeBattleEffect326Step AdvanceClosingMesh62C2F0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10) + Signed(task, 0x20), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        frame = DrawMesh326(frame, resources, inputs, Signed(task, 14) == 0 ? 0xE16CF0u : 0xE17450u, 0xF3, Signed(task, 0x16), 2);
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + Signed(task, 0x22)))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + Signed(task, 0x1E) / 4))));
        }
        return IncrementMeshClock(frame, address, inputs, 30);
    }

    public static NativeBattleEffect326Step AdvanceClosingCenter62C430(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), Signed(task, 0x1C), Signed(task, 0x20), Signed(task, 0x20)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        frame = DrawMesh326(frame, resources, inputs, 0xE17BB0, 0x33, null, 2);
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            task = Read(frame, address);
            int velocity = Signed(task, 0x1E);
            frame = Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity + (velocity >> 5)))));
        }
        return IncrementMeshClock(frame, address, inputs, 30);
    }

    private static NativeBattleEffect326Frame DrawMesh326(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs, uint source, uint flags, int? cue, byte shift)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(flags));
        if (cue is int value)
        {
            context = context.Write(12, BitConverter.GetBytes(value));
        }
        NativeBattleDefaultActMeshStep drawn = NativeBattleEffect326MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(source), inputs.ReadProjection(frame), context, inputs.Colors, shift);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        return Release(frame, 88);
    }
}
