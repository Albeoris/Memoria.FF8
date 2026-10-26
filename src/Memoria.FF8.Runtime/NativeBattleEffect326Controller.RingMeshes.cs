using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceRingMesh(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool inner = task.Callback == 0x62EA10;
        if (!inner && task.Callback != 0x62E8C0)
        {
            throw new InvalidDataException("Effect326 ring requires its actual native callback.");
        }
        int age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 0x1C), Signed(task, 0x20), Signed(task, 0x1C));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, ViewMatrix).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xE18150u)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0x33u));
        int fadeStart = inner ? 4 : 6;
        if (age >= fadeStart)
        {
            context = context.Write(0x1C, BitConverter.GetBytes(0xF3u)).Write(12, BitConverter.GetBytes(unchecked((age - fadeStart) * (inner ? 512 : 682))));
        }
        NativeBattleDefaultActMeshStep drawn = NativeBattleEffect326MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0xE18150), inputs.ReadProjection(frame), context, inputs.Colors, 2);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        frame = Release(frame, 88);
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            task = Read(frame, address);
            int velocityOffset = inner ? 0x1E : 0x22;
            int scaleOffset = inner ? 0x1C : 0x20;
            short velocity = Signed(task, velocityOffset);
            frame = Write(frame, address, task.WriteWord(scaleOffset, unchecked((ushort)(task.ReadWord(scaleOffset) + velocity))).WriteWord(velocityOffset, unchecked((ushort)(velocity - velocity / 4))));
        }
        return IncrementMeshClock(frame, address, inputs, 12);
    }
}
