using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Lower
{
    public static NativeBattleEffect313Step AdvanceDebris(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = AtTask313(NativeBattleGfMatrices.Euler56CD50(new(Signed(task, 28), 0, Signed(task, 32))), task);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 26), Signed(task, 26), Signed(task, 26));
        int age = Signed(task, 12);
        int life = Signed(task, 14);
        bool fade = age >= life - 8;
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xCD8D40u + (uint)Signed(task, 24) * 4), 4));
        frame = DrawMesh313(frame, local, source, fade ? 0xC3u : 0u, fade ? unchecked((age - life + 8) << 9) : null, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = source } : null);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 22);
        task = task.WriteWord(18, unchecked((ushort)(Signed(task, 18) + velocity))).WriteWord(22, unchecked((ushort)(velocity - velocity / 9))).WriteWord(32, unchecked((ushort)(Signed(task, 32) + Signed(task, 34)))).WriteWord(28, unchecked((ushort)(Signed(task, 28) + Signed(task, 30))));
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= Signed(task, 14) ? 2 : 0);
    }

    public static NativeBattleEffect313Step AdvanceRing(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = AtTask313(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), task);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 28), Signed(task, 32), Signed(task, 28));
        int age = Signed(task, 12);
        frame = DrawMesh313(frame, local, 0xCD6038, age >= 10 ? 0xF0u : 0x30u, age >= 10 ? unchecked((age - 10) * 292) : null, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = 0xCD6038 } : null);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short velocity = Signed(task, 34);
        short next = unchecked((short)(Signed(task, 12) + 1));
        task = task.WriteWord(32, unchecked((ushort)(Signed(task, 32) + velocity))).WriteWord(34, unchecked((ushort)(velocity - (velocity >> 1)))).WriteWord(12, unchecked((ushort)next));
        return new(Write(frame, address, task), next >= 24 ? 2 : 0);
    }

    public static NativeBattleEffect313Step AdvanceSparkle(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 14) > 0)
        {
            return new(Paused313(frame, inputs) ? frame : Write(frame, address, task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 1)))), 0);
        }
        NativeBattleActionMatrix local = AtTask313(NativeBattleGfMatrices.Euler56CE30(new(1024, Signed(task, 24), 0)), task);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 28), Signed(task, 28), Signed(task, 28));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord313(PutWord313(Put313(Context(frame, temporary, 180), 0, 0xCD5CB8), 4, 0), 36, 12);
        int quotient = 128 / Signed(task, 32);
        byte tint = unchecked((byte)(128 - unchecked((sbyte)quotient) * unchecked((sbyte)task.ReadByte(12))));
        context = context.Write(28, [tint, tint, tint]);
        frame = DrawSprite313(frame, temporary, context, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)) } : null);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= Signed(task, 32) ? 2 : 0);
    }

    public static NativeBattleEffect313Step AdvanceSmoke(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int scale = Signed(task, 28);
        frame = SetupSprite313(frame, TaskView(task, 16, 8), scale, -(scale >> 3), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord313(PutWord313(Put313(Context(frame, temporary, 180), 0, 0xCD5CE0), 4, task.ReadWord(12)), 36, 0);
        frame = DrawSprite313(frame, temporary, context, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)) } : null);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        task = task.WriteWord(18, unchecked((ushort)(Signed(task, 18) + Signed(task, 22)))).WriteWord(12, unchecked((ushort)next));
        return new(Write(frame, address, task), next >= Signed(task, 14) ? 2 : 0);
    }

    public static NativeBattleEffect313Step AdvanceExpansion(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int scale = Signed(task, 28);
        frame = SetupSprite313(frame, ReadBytes(frame, 0x223CC90, 8), scale, -(scale >> 3), inputs);
        (frame, uint temporary) = Allocate(frame, 88);
        int age = Signed(task, 12);
        int? cue = age < 16 ? unchecked((16 - age) << 8) : age >= 30 ? unchecked((age - 30) * 682) : null;
        NativeBattleEffectArena context = Put313(Put313(Context(frame, temporary, 88).Write(8, new byte[3]), 0, 0xCD7C38), 28, cue.HasValue ? 0xC0u : 0u);
        if (cue is { } depthCue)
        {
            context = Put313(context, 12, unchecked((uint)depthCue));
        }
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(0xCD7C38), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = 0xCD7C38 } : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect313DrawBatch(PreserveNativeDepths(draw.Batch), 0x223CC9C));
        frame = Release(frame, 88);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (Signed(task, 12) < 18)
        {
            short velocity = Signed(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(Signed(task, 28) - velocity))).WriteWord(30, unchecked((ushort)(velocity + (velocity >> 5))));
        }
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= 36 ? 2 : 0);
    }

    public static NativeBattleEffect313Step AdvanceRotatingSprite(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 14) > 0)
        {
            return new(Paused313(frame, inputs) ? frame : Write(frame, address, task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 1)))), 0);
        }
        int scale = Signed(task, 28);
        frame = SetupSprite313(frame, ReadBytes(frame, 0x223CC90, 8), scale, -(scale >> 4), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord313(PutWord313(Put313(Put313(Context(frame, temporary, 180), 8, unchecked((uint)(int)Signed(task, 24))), 0, 0xCD5B0C), 4, task.ReadWord(32)), 36, 1);
        int age = Signed(task, 12);
        if (age < 8 || age >= 22)
        {
            byte tint = unchecked((byte)(age < 8 ? age << 4 : 224 - (unchecked((byte)age) << 4)));
            context = PutWord313(context.Write(28, [tint, tint, tint]), 36, 5);
        }
        frame = DrawSprite313(frame, temporary, context, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)) } : null);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short phase = Word313(ReadBytes(frame, temporary + 40, 2), 0) < 0 ? (short)0 : unchecked((short)(Signed(task, 32) + 1));
        short next = unchecked((short)(Signed(task, 12) + 1));
        task = task.WriteWord(32, unchecked((ushort)phase)).WriteWord(28, unchecked((ushort)(Signed(task, 28) + Signed(task, 30)))).WriteWord(12, unchecked((ushort)next));
        return new(Write(frame, address, task), next >= 30 ? 2 : 0);
    }
}
