using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect251Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Lower
{
    public static NativeBattleEffect251Step Advance(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x6F4A30 => ParticleBurst(frame, address, resources, inputs),
            0x6F4DC0 or 0x6F4EC0 => Particle(frame, address, resources, inputs),
            0x6F5B20 => Trail(frame, address, resources, inputs),
            0x6F6530 => ActChild(frame, address, resources, inputs),
            0x6F65F0 => Sprite(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Effect251 requires its selected lower callback.")
        };
    }

    public static NativeBattleEffectArena Put251(NativeBattleEffectArena bytes, int offset, uint value) => bytes.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord251(NativeBattleEffectArena bytes, int offset, int value) => bytes.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static bool Paused251(NativeBattleEffect251Frame frame) => ReadCurrentPause(frame) != 0;

    private static int Random251(ref NativeBattleEffect251Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static NativeBattleEffect251Frame Setup251(NativeBattleEffect251Frame frame, NativeBattleEffectArena point, int scale, int depth, NativeBattleEffect251Inputs inputs)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), w, unchecked((short)scale), unchecked((short)depth)) };
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        return Publish(frame, registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } });
    }

    private static NativeBattleEffect251Frame DrawSprite251(NativeBattleEffect251Frame frame, uint temporary, NativeBattleEffectArena context, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs, bool paletteOverride = false)
    {
        frame = WriteBytes(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, allowFirstPaletteOverride: paletteOverride);
        frame = WriteBytes(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect251DrawBatch(drawn.Batch));
    }

    private static NativeBattleEffect251Step Delay251(NativeBattleEffect251Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        if (!Paused251(frame))
        {
            frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 1))));
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect251Frame Mutate4FA0(NativeBattleEffect251Frame frame, uint address)
    {
        NativeBattleGfAnimationSource source = new(0x1434430, frame.State.Read(0x1434430, 17224).ToImmutableArray());
        int start = checked((int)(address - source.Address));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(start + 4));
        if (count < 2 || start < 0 || start > source.Data.Length - 12 - checked(count * 8))
        {
            throw new InvalidDataException("Effect251 normalization requires its actual positive vertex rows.");
        }
        NativeBattleEffectArena normalized = NormalizeObject4FA0(frame.State.ReadView(address, checked(12 + count * 8)), count);
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectArena word = CopyBytes(normalized, 18 + index * 8, NativeBattleEffectArena.Clear(), 0, 2);
            frame = frame with { State = frame.State.WriteView(checked(address + 18u + (uint)index * 8), word, 2) };
        }
        return frame;
    }

    public static NativeBattleEffectArena NormalizeObject4FA0(NativeBattleEffectArena source, int count)
    {
        if (count < 2)
        {
            throw new InvalidDataException("Effect251 normalization requires at least two current rows.");
        }
        int minimum = Word251(source, 14);
        int maximum = minimum;
        for (int index = 0; index < count - 1; index++)
        {
            int y = Word251(source, 14 + index * 8);
            minimum = Math.Min(minimum, y);
            maximum = Math.Max(maximum, y);
        }
        int range = maximum - minimum;
        if (range == 0)
        {
            throw new InvalidDataException("Effect251 normalization has a zero native denominator.");
        }
        for (int index = 0; index < count; index++)
        {
            int y = Word251(source, 14 + index * 8);
            source = PutWord251(source, 18 + index * 8, unchecked((y - maximum) << 12) / range);
        }
        return source;
    }

    public static NativeBattlePackedMatrix32 Packed251(NativeBattleEffectArena bytes)
    {
        (byte[] data, bool[] known) = bytes.ReadSnapshot(0, 32);
        return new(data.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleGeometryRegisterStep LoadPacked251(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena bytes, int slot, int offset)
    {
        (byte[] data, bool[] known) = bytes.ReadSnapshot(offset, 8);
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(6)) : null;
        registers = NativeBattleGeometry.LoadVector(registers, slot, Vector(bytes, offset), w);
        return slot == 0 ? registers with { Colors = registers.Colors with { NormalInput = data.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } } : registers;
    }

    public static NativeBattleEffect251Frame Fade6020(NativeBattleEffect251Frame frame, int red, int green, int blue, int age, int fadeIn, int hold, int fadeOut, NativeBattleEffect251Inputs inputs)
    {
        int amount = NativeBattleRinoa89FadeDrawing.Factor6FC450(age, fadeIn, hold, fadeOut);
        return inputs.Execute(frame, new NativeBattleEffect251DrawBatch(NativeBattleRinoa89FadeDrawing.Draw7040B0(red, green, blue, amount)));
    }

    private static NativeBattleEffect251Step Particle(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 14) > 0)
        {
            return Delay251(frame, address, task);
        }
        bool rising = task.Callback == 0x6F4EC0;
        int scale = Signed(task, rising ? 28 : 32);
        frame = Setup251(frame, TaskView(task, 16, 8), scale, -(scale >> 3), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord251(PutWord251(Put251(ReadBytes(frame, temporary, 180), 0, rising ? 0x1438AE4u : 0xC79220u), 4, rising ? Signed(task, 12) : 9 - Signed(task, 12)), 36, rising ? 0 : 16);
        if (!rising)
        {
            context = context.Write(32, [4]);
        }
        frame = DrawSprite251(frame, temporary, context, resources, inputs, !rising);
        frame = Release(frame, 180);
        if (Paused251(frame))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (rising)
        {
            short velocity = Signed(task, 22);
            task = task.WriteWord(18, unchecked((ushort)(Signed(task, 18) + velocity))).WriteWord(22, unchecked((ushort)(velocity + (velocity >> 5)))).WriteWord(28, unchecked((ushort)(Signed(task, 28) - (Signed(task, 28) >> 6))));
        }
        else
        {
            task = task.WriteWord(32, unchecked((ushort)(Signed(task, 32) - 64)));
            for (int axis = 0; axis < 3; axis++)
            {
                short velocity = Signed(task, 24 + axis * 2);
                task = task.WriteWord(16 + axis * 2, unchecked((ushort)(Signed(task, 16 + axis * 2) + velocity))).WriteWord(24 + axis * 2, unchecked((ushort)(velocity + (axis == 1 ? velocity >> 4 : -(velocity >> 4)))));
            }
        }
        short age = unchecked((short)(Signed(task, 12) + (rising ? 1 : -1)));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)age))), rising ? age >= 16 ? 2 : 0 : age < 0 ? 2 : 0);
    }

    private static NativeBattleEffect251Step ParticleBurst(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 14) > 0)
        {
            return Delay251(frame, address, task);
        }
        int scale = Signed(task, 28);
        frame = Setup251(frame, TaskView(task, 16, 8), scale, -(scale >> 4), inputs);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord251(PutWord251(Put251(ReadBytes(frame, temporary, 180), 0, 0x143878C), 4, task.ReadWord(12)), 36, 0);
        frame = DrawSprite251(frame, temporary, context, resources, inputs);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)) with { X = Signed(task, 16), Y = Signed(task, 22), Z = Signed(task, 20) };
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, 4096);
        matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int q = unchecked(Math.Max((int)Signed(task, 18), -2000) << 12) / 2000;
        byte color = unchecked((byte)(160 + q * 160 / 4096));
        context = PutWord251(PutWord251(Put251(ReadBytes(frame, temporary, 180), 0, 0x1438938), 4, task.ReadWord(12)), 36, 12).Write(28, [color, color, color]);
        frame = DrawSprite251(frame, temporary, context, resources, inputs);
        frame = Release(frame, 180);
        if (Paused251(frame))
        {
            return new(frame, 0);
        }
        if (Signed(task, 12) == 0)
        {
            for (int index = 0; index < 14; index++)
            {
                bool rising = index < 4;
                (frame, uint child) = Append(frame, 0x2556440, rising ? 0x6F4EC0u : 0x6F4DC0u);
                if (child == 0)
                {
                    continue;
                }
                NativeBattleModulePoolTask birth = CopyTask(Read(frame, child), 16, TaskView(task, 16, 8), 8);
                int ordinal = rising ? index : index - 4;
                int life = rising ? 0 : 5 + Random251(ref frame) % 4;
                birth = birth.WriteWord(12, unchecked((ushort)life)).WriteWord(14, unchecked((ushort)(ordinal + Random251(ref frame) % (rising ? 6 : 4))));
                int span = rising ? 300 : 400;
                birth = birth.WriteWord(16, unchecked((ushort)(Signed(birth, 16) + Random251(ref frame) % span - span / 2))).WriteWord(18, unchecked((ushort)(Signed(birth, 18) - Random251(ref frame) % (rising ? 100 : 200)))).WriteWord(20, unchecked((ushort)(Signed(birth, 20) + Random251(ref frame) % span - span / 2)));
                if (rising)
                {
                    birth = birth.WriteWord(22, unchecked((ushort)(-20 - Random251(ref frame) % 30))).WriteWord(28, unchecked((ushort)(2560 + Random251(ref frame) % 1280)));
                }
                else
                {
                    birth = birth.WriteWord(24, unchecked((ushort)(Random251(ref frame) % 80 - 40))).WriteWord(26, unchecked((ushort)(-20 - Random251(ref frame) % 50))).WriteWord(28, unchecked((ushort)(Random251(ref frame) % 80 - 40))).WriteWord(32, unchecked((ushort)(2304 + Random251(ref frame) % 1536)));
                }
                frame = Write(frame, child, birth);
            }
        }
        short age = unchecked((short)(Signed(Read(frame, address), 12) + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)age))), age >= 16 ? 2 : 0);
    }

    private static NativeBattleEffect251Step Sprite(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Setup251(frame, TaskView(task, 12, 8), 4096, -1024, inputs);
        frame = frame with { State = frame.State with { CallbackStackWord58 = 0xB4 } };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord251(PutWord251(Put251(ReadBytes(frame, temporary, 180), 0, 0x142D744), 4, task.ReadWord(18)), 36, 0);
        frame = DrawSprite251(frame, temporary, context, resources, inputs);
        frame = Release(frame, 180);
        if (Paused251(frame))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short phase = unchecked((short)(Signed(task, 18) + 1));
        task = task.WriteWord(18, unchecked((ushort)phase));
        if (phase >= 13)
        {
            return new(Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Signed(task, 12 + axis * 2) + unchecked((sbyte)task.ReadBytes(20 + axis, 1)[0]))));
        }
        return new(Write(frame, address, task), 0);
    }
}
