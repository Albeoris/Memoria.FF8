using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Lower
{
    private static NativeBattleEffect210Step AdvanceParticles(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs) => NativeBattleEffect210Particles.Advance(frame, address, resources, inputs);
}

public static class NativeBattleEffect210Particles
{
    public static NativeBattleEffect210Frame PrepareSprite571BC0(NativeBattleEffect210Frame frame, NativeBattleEffectArena point8, int scale, int depth, NativeBattleEffect210Inputs inputs)
    {
        (byte[] bytes, bool[] known) = point8.ReadSnapshot(6, 2);
        NativeBattleVector point = new(SignedWord(point8, 0), SignedWord(point8, 2), SignedWord(point8, 4));
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, w, unchecked((short)scale), unchecked((short)depth)) };
    }

    public static NativeBattleEffect210Step Advance(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x6F6C00)
        {
            return Advance6C00(frame, address, inputs);
        }
        if (Signed(task, 14) > 0)
        {
            if (!Paused(frame))
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 1))));
            }
            return new(frame, 0);
        }
        if (task.Callback == 0x6F6DA0)
        {
            return Advance6DA0(frame, address, resources, inputs);
        }
        if (task.Callback is not (0x6F7130 or 0x6F7230))
        {
            throw new NotSupportedException("Effect210 requires its selected particle callback.");
        }
        bool falling = task.Callback == 0x6F7130;
        int scale = Signed(task, falling ? 0x20 : 0x1C);
        frame = PrepareSprite571BC0(frame, TaskView(task, 0x10, 8), scale, -(scale >> 3), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleModuleResource? common = null;
        uint source = 0x1470C18;
        if (falling)
        {
            common = (inputs.ReadCommonSprite5710B0 ?? throw new NotSupportedException("Effect210 requires its current shared sprite resource."))(frame, 5);
            source = common.Address;
        }
        NativeBattleEffectArena context = Put(Context(frame, temporary, 180), 0, source);
        context = PutWord(PutWord(context, 4, falling ? 9 - Signed(task, 12) : Signed(task, 12)), 0x24, falling ? 0x10 : 0);
        if (falling)
        {
            context = context.Write(0x20, [4]);
        }
        frame = PublishContext(frame, temporary, context, 180);
        if (falling)
        {
            NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, Context(frame, temporary, 180), candidate => candidate == source ? common! : throw new InvalidDataException("Effect210 shared sprite address changed."), inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, allowFirstPaletteOverride: true, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(210, task) with { ResourceGeneration = source });
            frame = frame with { Memory = draw.Memory };
            frame = PublishContext(frame, temporary, draw.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect210DrawBatch(draw.Batch, CursorGlobal));
        }
        else
        {
            frame = DrawSprite(frame, temporary, resources.Sprite, inputs, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(210, task));
        }
        frame = Release(frame, 180);
        if (Paused(frame))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (falling)
        {
            int vx = Signed(task, 0x18);
            int vy = Signed(task, 0x1A);
            int vz = Signed(task, 0x1C);
            task = task.WriteWord(0x20, unchecked((ushort)(Signed(task, 0x20) - 64)));
            task = AddWord(task, 0x10, vx);
            task = AddWord(task, 0x12, vy);
            task = AddWord(task, 0x14, vz);
            task = task.WriteWord(0x18, unchecked((ushort)(vx - (vx >> 4)))).WriteWord(0x1A, unchecked((ushort)(vy + (vy >> 4)))).WriteWord(0x1C, unchecked((ushort)(vz - (vz >> 4))));
        }
        else
        {
            int velocity = Signed(task, 0x16);
            task = AddWord(task, 0x12, velocity).WriteWord(0x16, unchecked((ushort)(velocity + (velocity >> 5))));
            task = task.WriteWord(0x1C, unchecked((ushort)(scale - (scale >> 6))));
        }
        short next = unchecked((short)(Signed(task, 12) + (falling ? -1 : 1)));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        return new(frame, falling ? next < 0 ? 2 : 0 : next >= 16 ? 2 : 0);
    }

    private static NativeBattleEffect210Step Advance6C00(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Inputs inputs)
    {
        if (Paused(frame))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 0x20);
        if (task.ReadWord(12) == 1)
        {
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, actor);
            int radius = Math.Min(unchecked(SignedWord(model, 0x26) * 2800) >> 12, 1000);
            NativeBattleEffect210PointStep anchor = inputs.Anchor571400(frame, actor, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = anchor.Frame;
            for (int index = 0; index < 4; index++)
            {
                (frame, uint child) = Append(frame, EarlyDescriptor, 0x6F6DA0);
                if (child == 0)
                {
                    continue;
                }
                NativeBattleModulePoolTask born = Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(Random(frame, 6, out frame) + 2 * index)));
                born = CopyTask(born, 0x10, anchor.Output8, 8);
                born = AddWord(born, 0x10, Random(frame, radius, out frame) - (radius >> 1));
                born = AddWord(born, 0x12, Random(frame, radius, out frame) - (radius >> 1));
                born = AddWord(born, 0x14, Random(frame, radius, out frame) - (radius >> 1));
                short angle = SignedWord(inputs.ReadModelArena9C(frame, actor), 0x24);
                born = born.WriteWord(0x16, unchecked((ushort)angle));
                if (Signed(born, 0x12) > angle - 550)
                {
                    born = AddWord(born, 0x12, -500 - Random(frame, 300, out frame));
                }
                born = born.WriteWord(0x1C, unchecked((ushort)(3328 + Random(frame, 1536, out frame))));
                frame = Write(frame, child, born);
            }
        }
        if (task.ReadWord(12) == 6)
        {
            frame = inputs.Anchor571400(frame, actor, NativeBattleEffectArena.Clear().MarkUnknown(0, 8)).Frame;
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= 22 ? 2 : 0);
    }

    private static NativeBattleEffect210Step Advance6DA0(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int scale = Signed(task, 0x1C);
        frame = PrepareSprite571BC0(frame, TaskView(task, 0x10, 8), scale, -(scale >> 4), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Put(Context(frame, temporary, 180), 0, 0x14708C0);
        context = PutWord(PutWord(context, 4, Signed(task, 12)), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        frame = DrawSprite(frame, temporary, resources.Sprite, inputs, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(210, task));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)), scale, scale, 4096) with { X = Signed(task, 0x10), Y = Signed(task, 0x16), Z = Signed(task, 0x14) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadCpuMatrix(frame, composed);
        int intensity = 160 + (unchecked((Math.Max((int)Signed(task, 0x12), -2000) << 12) / 2000 * 160) / 4096);
        context = Put(Context(frame, temporary, 180), 0, 0x1470A6C);
        context = PutWord(PutWord(context, 4, Signed(task, 12)), 0x24, 12).Write(0x1C, [unchecked((byte)intensity), unchecked((byte)intensity), unchecked((byte)intensity)]);
        frame = PublishContext(frame, temporary, context, 180);
        frame = DrawSprite(frame, temporary, resources.Sprite, inputs, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(210, task));
        frame = Release(frame, 180);
        if (Paused(frame))
        {
            return new(frame, 0);
        }
        if (task.ReadWord(12) == 0)
        {
            frame = BirthChildren(frame, task);
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= 16 ? 2 : 0);
    }

    private static NativeBattleEffect210Frame BirthChildren(NativeBattleEffect210Frame frame, NativeBattleModulePoolTask parent)
    {
        for (int index = 0; index < 14; index++)
        {
            bool falling = index >= 4;
            (frame, uint child) = Append(frame, EarlyDescriptor, falling ? 0x6F7130u : 0x6F7230u);
            if (child == 0)
            {
                continue;
            }
            NativeBattleModulePoolTask born = Read(frame, child);
            born = born.WriteWord(12, unchecked((ushort)(falling ? 5 + Random(frame, 4, out frame) : 0)));
            born = born.WriteWord(14, unchecked((ushort)(Random(frame, falling ? 4 : 6, out frame) + (falling ? index - 4 : index))));
            born = CopyTask(born, 0x10, TaskView(parent, 0x10, 8), 8);
            int spread = falling ? 400 : 300;
            born = AddWord(born, 0x10, Random(frame, spread, out frame) - spread / 2);
            born = AddWord(born, 0x12, -Random(frame, falling ? 200 : 100, out frame));
            born = AddWord(born, 0x14, Random(frame, spread, out frame) - spread / 2);
            if (falling)
            {
                born = born.WriteWord(0x18, unchecked((ushort)(Random(frame, 80, out frame) - 40)));
                born = born.WriteWord(0x1A, unchecked((ushort)(-20 - Random(frame, 50, out frame))));
                born = born.WriteWord(0x1C, unchecked((ushort)(Random(frame, 80, out frame) - 40)));
                born = born.WriteWord(0x20, unchecked((ushort)(2304 + Random(frame, 1536, out frame))));
            }
            else
            {
                born = born.WriteWord(0x16, unchecked((ushort)(-20 - Random(frame, 30, out frame))));
                born = born.WriteWord(0x1C, unchecked((ushort)(2560 + Random(frame, 1280, out frame))));
            }
            frame = Write(frame, child, born);
        }
        return frame;
    }

    private static NativeBattleModulePoolTask AddWord(NativeBattleModulePoolTask task, int offset, int amount) => task.WriteWord(offset, unchecked((ushort)(Signed(task, offset) + amount)));

    private static int Random(NativeBattleEffect210Frame current, int divisor, out NativeBattleEffect210Frame next)
    {
        NativeCrtRandomState random = current.Random.Draw(out ushort value);
        next = current with { Random = random };
        return value % divisor;
    }
}
