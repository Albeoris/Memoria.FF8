namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceOpening(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool second = task.Callback == 0x62B4F0;
        if (!second && task.Callback != 0x62B080)
        {
            throw new InvalidDataException("Effect326 opening requires its actual native callback.");
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        int age = Signed(task, 12);
        int lane = age < 8 ? age * 8 : age >= (second ? 38 : 46) ? (54 - age) * (second ? 4 : 8) : 64;
        uint color = unchecked((uint)((lane << 16) | (lane << 8) | lane));
        (frame, uint temporary) = Allocate(frame, 120);
        NativeBattleEffectArena context = Context(frame, temporary, 120).Write(0, BitConverter.GetBytes(second ? 512 : 256)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(second ? 16 : 32)).Write(12, BitConverter.GetBytes(second ? age * 2 : age)).Write(16, BitConverter.GetBytes(age * 2)).Write(0x74, BitConverter.GetBytes(color));
        NativeBattleEffect326OpeningGridStep drawn = NativeBattleEffect326OpeningGrid.Draw62B1C0(frame.Memory, context, inputs.ReadProjection(frame));
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context120, 120);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        frame = Release(frame, 120);
        return IncrementMeshClock(frame, address, inputs, 54);
    }
}
