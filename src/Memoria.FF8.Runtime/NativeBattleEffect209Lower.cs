using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect209Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Lower
{
    private const uint CommonCursor = 0x1D8E054;

    public static NativeBattleEffect209Step Advance(NativeBattleEffect209Frame frame, uint taskAddress, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        uint callback = Read(frame, taskAddress).Callback;
        if (callback == 0x637A40)
        {
            return AdvanceScreen(frame, taskAddress, resources, inputs);
        }
        if (callback is not (0x638490 or 0x6385E0 or 0x638770 or 0x638900 or 0x638A80))
        {
            throw new NotSupportedException("Effect209 requires its selected lower callback.");
        }
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        frame = frame with { PresentationIdentity = frame.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(209, task) : null };
        int age = Signed(task, 12);
        bool scrolling = callback == 0x638770;
        int y = Signed(task, 0x12);
        if (scrolling)
        {
            NativeBattleEffect209PointStep point = inputs.Point502170(frame, checked((int)frame.State.ReadDword(CasterGlobal)), 0xF1, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = point.Frame;
            y = Vector(point.Output8).Y;
        }
        int angle = callback is 0x638490 or 0x638770 ? 0 : Signed(task, 0x18);
        int sx = Signed(task, 0x1C);
        int sy = callback == 0x6385E0 || callback == 0x638900 ? Signed(task, 0x20) : sx;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, unchecked((short)angle), 0)), sx, sy, sx) with { X = Signed(task, 0x10), Y = y, Z = Signed(task, 0x14) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int size = callback == 0x638490 ? 96 : scrolling ? 144 : 88;
        (frame, uint temporary) = Allocate(frame, size);
        NativeBattleEffectArena context = Put(Context(frame, temporary, size), 8, 0);
        uint source;
        int lifetime;
        if (callback == 0x638490)
        {
            source = 0xE288F0;
            lifetime = 110;
            context = Put(Put(Put(context, 0, source), 0x1C, age >= 102 ? 0xF3u : 0x33u), 0x58, 0xE1000240);
            context = Put(context, 0x5C, 0xE1000220);
            if (age >= 102)
            {
                context = Put(context, 12, unchecked((uint)((age - 102) << 9)));
            }
        }
        else if (scrolling)
        {
            source = 0xE26340;
            lifetime = 110;
            context = Put(Put(Put(Put(Put(Put(context, 0, source), 12, unchecked((uint)(age >= 102 ? age * 320 - 31104 : 1536))), 0x10, 0), 0x14, unchecked((uint)(age * 8))), 0x20, 0xF3), 0x24, resources.ScrollColors[age % 18]);
            context = Word(Word(Word(Word(context, 0x18, 0), 0x1A, 0), 0x1C, 64), 0x1E, 128);
        }
        else
        {
            source = callback == 0x6385E0 ? BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(MeshDirectoryAddress + (uint)(Signed(task, 14) * 4), 4)) : callback == 0x638900 ? 0xE277B8u : 0xE290E0u;
            lifetime = callback == 0x6385E0 ? task.ReadWord(0x16) == 0 ? 110 : 55 : callback == 0x638900 ? 24 : 20;
            context = Put(Put(context, 0, source), 0x1C, 0x33);
            int? cue = callback == 0x6385E0 ? age >= lifetime - 8 ? (age - (lifetime - 8)) << 9 : null : callback == 0x638900 ? age < 2 ? (2 - age) << 11 : age >= 8 ? (age - 8) << 8 : null : age >= 4 ? (age - 4) << 8 : null;
            if (cue is int fade)
            {
                context = Put(Put(context, 0x1C, 0xF3), 12, unchecked((uint)fade));
            }
        }
        NativeBattlePilotVisualIdentity? identity = frame.PresentationIdentity is { } sourceIdentity ? sourceIdentity with { ResourceGeneration = source } : null;
        frame = PublishContext(frame, temporary, context, size);
        if (callback == 0x638490)
        {
            NativeBattleEffect209ModeMesh96Step drawn = NativeBattleEffect209ModeMesh96.DrawPresentation(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, pilotIdentity: identity);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context96, size);
            frame = inputs.Execute(frame, new NativeBattleEffect209DrawBatch(drawn.Batch, CommonCursor));
        }
        else if (scrolling)
        {
            NativeBattleWindowedMesh144Step drawn = NativeBattleEffect209Windowed144.DrawPresentation(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, pilotIdentity: identity);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context144, size);
            frame = inputs.Execute(frame, new NativeBattleEffect209DrawBatch(drawn.Batch, CommonCursor));
        }
        else
        {
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, pilotIdentity: identity);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, size);
            frame = inputs.Execute(frame, new NativeBattleEffect209DrawBatch(drawn.Batch, CommonCursor));
        }
        frame = Release(frame, size);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, taskAddress);
        if (callback != 0x638490 && !scrolling)
        {
            int velocity = Signed(task, 0x1A);
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + velocity)));
            if (callback == 0x638A80)
            {
                task = task.WriteWord(0x1A, unchecked((ushort)(velocity - (velocity >> 4))));
            }
        }
        if (!scrolling)
        {
            if (callback is 0x638490 or 0x6385E0)
            {
                if (age < 16)
                {
                    task = AdvanceScale(task, 0x1C, 0x1E, 8);
                }
                if (callback == 0x6385E0 && age < 32)
                {
                    task = AdvanceScale(task, 0x20, 0x22, 8);
                }
            }
            else
            {
                task = AdvanceScale(task, 0x1C, 0x1E, 16);
                if (callback == 0x638900)
                {
                    task = AdvanceScale(task, 0x20, 0x22, 16);
                }
            }
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, taskAddress, task.WriteWord(12, unchecked((ushort)next))), next >= lifetime ? 2 : 0);
    }

    private static NativeBattleModulePoolTask AdvanceScale(NativeBattleModulePoolTask task, int scaleOffset, int velocityOffset, int divisor)
    {
        int velocity = Signed(task, velocityOffset);
        return task.WriteWord(scaleOffset, unchecked((ushort)(task.ReadWord(scaleOffset) + velocity))).WriteWord(velocityOffset, unchecked((ushort)(velocity - velocity / divisor)));
    }
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short? OptionalWord(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
