namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame InitializeActorOverlay62BED0(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs)
    {
        (frame, uint first) = AppendRequired326(frame, 0x62BF40);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xCD38).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0).WriteWord(0x1C, 0x3000));
        (frame, uint second) = AppendRequired326(frame, 0x62C070);
        return Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(0x10, 0).WriteWord(0x12, 0xD120).WriteWord(0x14, 0xEE6C).WriteWord(0x18, 0).WriteWord(0x1C, 0x3000));
    }

    public static NativeBattleEffect326Step AdvanceClosingGrid(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool second = task.Callback == 0x62C070;
        if (!second && task.Callback != 0x62BF40)
        {
            throw new InvalidDataException("Effect326 closing grid requires its actual native callback.");
        }
        int age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        int u = second ? age * 2 : Math.Min(age, 30);
        int v = -u;
        uint color = second || age < 30 ? 0x00404040u : 0x00202020u;
        (frame, uint temporary) = Allocate(frame, 120);
        NativeBattleEffectArena context = Context(frame, temporary, 120).Write(0, BitConverter.GetBytes(second ? 512 : 256)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(second ? 16 : 32)).Write(12, BitConverter.GetBytes(u)).Write(16, BitConverter.GetBytes(v)).Write(0x74, BitConverter.GetBytes(color));
        NativeBattleEffect326OpeningGridStep drawn = NativeBattleEffect326OpeningGrid.Draw62B1C0(frame.Memory, context, inputs.ReadProjection(frame));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context120, 120);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        frame = Release(frame, 120);
        return IncrementMeshClock(frame, address, inputs, second ? 30 : 60);
    }
}
