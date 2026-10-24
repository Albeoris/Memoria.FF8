using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect140Fields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect140Particles
{
    public static NativeBattleEffect140FactoryStep Spawn6A7CE0(NativeBattleEffect140Frame frame, int centerZ, int rangeX, int rangeZ, int topY, int rangeY, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        if (frame.State.ReadDword(Pause) != 0)
        {
            return new(frame, 0);
        }
        (frame, uint address) = Append(frame, 0x2517AC8, 0x6A7D90);
        if (address == 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)((Roll(ref frame) & (rangeX - 1)) - (rangeX >> 1))));
        task = task.WriteWord(16, unchecked((ushort)(centerZ + (Roll(ref frame) & (rangeZ - 1)) - (rangeZ >> 1))));
        task = task.WriteWord(14, unchecked((ushort)(topY - (Roll(ref frame) & (rangeY - 1))))).WriteWord(18, 0);
        task = task.WriteWord(20, unchecked((ushort)((Roll(ref frame) & 8191) - 4096)));
        task = task.WriteWord(22, unchecked((ushort)((Roll(ref frame) & 8191) - 4096))).WriteWord(24, 0).WriteWord(26, 0);
        return new(Write(frame, address, task), address);
    }

    public static NativeBattleEffect140Step Advance(NativeBattleEffect140Frame frame, uint address, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x6A7BF0)
        {
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 20)) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
            NativeBattleEffectArena context = Put(Put(Context(frame, temporary, 88), 0, 0x11D4B1C), 0x1C, 0xF0).Write(8, new byte[3]);
            context = Put(context, 12, unchecked((uint)(Signed(task, 18) << 8)));
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(0x11D4B1C), inputs.ReadProjection(frame), context, inputs.Colors);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
            frame = inputs.Execute(frame, new NativeBattleEffect140DrawBatch(drawn.Batch, Cursor));
            frame = Release(frame, 88);
        }
        else if (task.Callback == 0x6A7D90)
        {
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 1024, -256) };
            (frame, uint temporary) = Allocate(frame, 180);
            frame = PublishContext(frame, temporary, Word(Word(Put(Context(frame, temporary, 180), 0, 0x11D495C), 4, Signed(task, 18)), 0x24, 0), 180);
            frame = DrawSprite(frame, temporary, resources.Sprite(0x11D495C), resources, inputs);
            frame = Release(frame, 180);
        }
        else
        {
            throw new NotSupportedException("Effect140 particle callback leaves its selected mesh and sprite paths.");
        }
        if (frame.State.ReadDword(Pause) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (task.Callback == 0x6A7BF0)
        {
            task = task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 25))).WriteWord(20, unchecked((ushort)(Signed(task, 20) + Signed(task, 22))));
        }
        else
        {
            int x = unchecked((short)(Signed(task, 20) + Signed(task, 24)));
            int z = unchecked((short)(Signed(task, 22) + Signed(task, 26)));
            task = task.WriteWord(20, unchecked((ushort)x)).WriteWord(22, unchecked((ushort)z));
            task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + (x >> 4)))).WriteWord(16, unchecked((ushort)(Signed(task, 16) + (z >> 4)))).WriteWord(14, unchecked((ushort)(Signed(task, 14) - 75)));
            if (x != 0)
            {
                task = task.WriteWord(24, unchecked((ushort)(Signed(task, 24) + (x > 0 ? -256 : 256))));
            }
            if (z != 0)
            {
                task = task.WriteWord(26, unchecked((ushort)(Signed(task, 26) + (z > 0 ? -256 : 256))));
            }
        }
        short age = unchecked((short)(Signed(task, 18) + 1));
        return new(Write(frame, address, task.WriteWord(18, unchecked((ushort)age))), age >= 16 ? 2 : 0);
    }

    public static NativeBattleEffect140Frame Draw85A0(NativeBattleEffect140Frame frame, uint temporary180, short spriteFrame, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleEffectArena context = Word(Word(Put(Context(frame, temporary180, 180), 0, 0x11D0164), 4, spriteFrame), 0x24, 0);
        frame = PublishContext(frame, temporary180, context, 180);
        return DrawSprite(frame, temporary180, resources.Sprite(0x11D0164), resources, inputs);
    }

    private static NativeBattleEffect140Frame DrawSprite(NativeBattleEffect140Frame frame, uint address, NativeBattleModuleResource source, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, Context(frame, address, 180), _ => source, inputs.ReadProjection(frame), inputs.ReadCameraPose(frame).Roll, 17, 2);
        frame = PublishContext(frame with { Memory = drawn.Memory }, address, drawn.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect140DrawBatch(drawn.Batch, Cursor));
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static int Roll(ref NativeBattleEffect140Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }
}
