using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect207BoundsStep(NativeBattleEffect207Frame Frame, NativeBattleEffectArena Rect8, int MinimumDepth, bool Included);

public static partial class NativeBattleEffect207Lower
{
    private const uint L207Cursor = 0x2517A24;

    public static NativeBattleEffect207Step Advance(NativeBattleEffect207Frame frame, uint address, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x6A5650)
        {
            throw new NotSupportedException("Effect207 requires its selected particle callback.");
        }
        NativeBattleEffectArena point = TaskView(task, 12, 8);
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null, 4096, -1024) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = L207Word(L207Word(L207Put(Context(frame, temporary, 180), 0, 0x11AC414), 4, task.ReadWord(0x12)), 0x24, 0);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(207, task) with { ResourceGeneration = 0x11AC414 });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect207DrawBatch(drawn.Batch, L207Cursor));
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            int velocityOffset = 0x14 + axis * 2;
            sbyte acceleration = unchecked((sbyte)task.ReadBytes(0x1A + axis, 1)[0]);
            short velocity = unchecked((short)(task.ReadWord(velocityOffset) + acceleration));
            task = task.WriteWord(velocityOffset, unchecked((ushort)velocity));
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + velocity)));
        }
        short next = unchecked((short)(task.ReadWord(0x12) + 1));
        return new(Write(frame, address, task.WriteWord(0x12, unchecked((ushort)next))), next >= 16 ? 2 : 0);
    }

    public static NativeBattleEffectArena Direction6020(int angleA, int angleB, NativeBattleEffectArena retained8)
    {
        int cosine = NativeBattleCameraMath.Cosine(angleA);
        short y = unchecked((short)-NativeBattleCameraMath.Sine(angleA));
        short x = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angleB) * cosine) >> 12));
        short z = unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(angleB) * cosine) >> 12));
        return retained8.Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    }

    public static NativeBattleEffect207BoundsStep Bounds5F10(NativeBattleEffect207Frame frame, uint modelPointer, NativeBattleEffectArena rect8, int minimumDepth, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        long difference = (long)modelPointer - 0x1D972C0;
        if (difference < 0 || difference % 156 != 0 || difference / 156 >= 7)
        {
            throw new InvalidDataException("Effect207 bounds require their current physical actor.");
        }
        NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, (int)(difference / 156));
        NativeBattleVector bottom = new(L207S(model, 0x1C), L207S(model, 0x1E), L207S(model, 0x20));
        NativeBattleVector top = bottom with { Y = unchecked((short)(L207S(model, 0x36) - 512)) };
        short size = unchecked((short)(L207S(model, 0x26) + 256));
        int radius = size;
        bool included = false;
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect207 bounds require current projection controls.");
        for (int index = 1; index >= 0; index--)
        {
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, index == 1 ? top : bottom, null), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            frame = Publish(frame, registers);
            uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect207 bounds require their projected screen coordinate.");
            short depth = unchecked((short)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Effect207 bounds require their current depth.")) >> 2));
            short x = unchecked((short)(unchecked((short)screen) / 8));
            short y = unchecked((short)(unchecked((short)(screen >> 16)) / 8));
            if (index == 1)
            {
                radius = unchecked(size * 54) / depth;
            }
            short left = unchecked((short)(x - radius));
            Include(left, y, depth);
            Include(unchecked((short)(left + radius * 2)), y, depth);
        }
        return new(frame, rect8, minimumDepth, included);

        void Include(short x, short y, short depth)
        {
            if (depth <= 0)
            {
                return;
            }
            included = true;
            rect8 = L207Word(rect8, 0, Math.Min(L207S(rect8, 0), x));
            rect8 = L207Word(rect8, 2, Math.Min(L207S(rect8, 2), y));
            rect8 = L207Word(rect8, 4, Math.Max(L207S(rect8, 4), x));
            rect8 = L207Word(rect8, 6, Math.Max(L207S(rect8, 6), y));
            minimumDepth = Math.Min(minimumDepth, depth);
        }
    }

    private static NativeBattleEffectArena L207Source(NativeBattleEffect207Frame frame, NativeBattleEffect207Resources resources, uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> data) in resources.StaticFronts)
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(address, length));
            }
        }
        if (address >= resources.Programme.Address && (ulong)address + (uint)length <= (ulong)resources.Programme.Address + (uint)resources.Programme.Data.Length)
        {
            return NativeBattleEffectArena.Clear().Write(0, resources.Programme.Data.AsSpan().Slice(checked((int)(address - resources.Programme.Address)), length));
        }
        return ReadBytes(frame, address, length);
    }
    private static NativeBattleEffectArena L207Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena L207Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static short L207S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint L207U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
