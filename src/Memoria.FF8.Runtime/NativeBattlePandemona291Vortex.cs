using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291Vortex
{
    public static NativeBattleEffectArena Reset6F1520(NativeBattleEffectArena arena, uint taskAddress)
    {
        int task = Offset(taskAddress);
        for (int index = 0; index < 128; index++)
        {
            arena = Word(arena, task + 0x42C + index * 24 + 6, 0);
        }
        return arena.Write(task + 0x102C, BitConverter.GetBytes(unchecked(taskAddress + 0x42C)));
    }

    public static NativeBattlePandemona291TrailAllocation Spawn6EDC66(NativeBattleEffectArena arena, NativeCrtRandomState random, uint taskAddress, ImmutableArray<byte> trigonometry)
    {
        for (int index = 0; index < 4; index++)
        {
            NativeBattlePandemona291TrailAllocation allocated = NativeBattlePandemona291Trails.Allocate6F1550(arena, random, taskAddress);
            arena = allocated.Arena;
            random = allocated.Random;
            if (allocated.Address is not uint address)
            {
                break;
            }
            random = random.Draw(out ushort selected);
            int angle = selected & 0xFFF;
            int record = Offset(address);
            arena = Word(arena, record + 8, unchecked((ushort)(Trig(trigonometry, angle, false) >> 3)));
            arena = Word(arena, record + 0xA, 0);
            arena = Word(arena, record + 0xC, unchecked((ushort)(Trig(trigonometry, angle, true) >> 3)));
            arena = Word(arena, record + 6, 12);
            arena = Word(arena, record + 0xE, 4096);
        }
        return new(arena, random, null);
    }

    public static NativeBattleEffectArena Advance6F1A30(NativeBattleEffectArena arena, uint taskAddress, NativeBattleActionMatrix matrix, int radialPhase, int radialAmplitude, ImmutableArray<byte> trigonometry, Action<ImmutableArray<byte>, NativeBattleFullMacVertex>? observeInput = null)
    {
        int task = Offset(taskAddress);
        for (int index = 0; index < 128; index++)
        {
            int record = checked(task + 0x42C + index * 24);
            ushort life = ReadWord(arena, record + 6);
            if (life == 0)
            {
                continue;
            }
            life = unchecked((ushort)(life - 1));
            arena = Word(arena, record + 6, life);
            if (life == 0)
            {
                arena = arena.Write(task + 0x102C, BitConverter.GetBytes(unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)record)));
                continue;
            }
            int spinX = unchecked((sbyte)arena.Read(record + 0x14, 1)[0]) << 2;
            int spinZ = unchecked((sbyte)arena.Read(record + 0x15, 1)[0]) << 2;
            arena = Word(arena, record + 0x10, unchecked((ushort)(ReadWord(arena, record + 0x10) + spinX)));
            arena = Word(arena, record + 0x12, unchecked((ushort)(ReadWord(arena, record + 0x12) + spinZ)));
            ImmutableArray<byte> input = arena.Read(record + 8, 8).ToImmutableArray();
            NativeBattleVector velocity = new(BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan().Slice(4)));
            NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(matrix, velocity);
            observeInput?.Invoke(input, transformed);
            short phase = unchecked((short)(velocity.Y + 4096));
            int radial = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((phase & 0xFFE) * 2, 2)) * radialAmplitude) >> 12;
            radial = unchecked((short)radial);
            int angle = unchecked(phase + radialPhase) & 0xFFF;
            int dx = unchecked(Trig(trigonometry, angle, false) * radial) >> 12;
            int dz = unchecked(Trig(trigonometry, angle, true) * radial) >> 12;
            arena = Word(arena, record, unchecked((ushort)(Math.Clamp(transformed.X, short.MinValue, short.MaxValue) + dx)));
            arena = Word(arena, record + 2, unchecked((ushort)Math.Clamp(transformed.Y, short.MinValue, short.MaxValue)));
            arena = Word(arena, record + 4, unchecked((ushort)(Math.Clamp(transformed.Z, short.MinValue, short.MaxValue) + dz)));
            arena = Word(arena, record + 0xA, unchecked((ushort)(ReadWord(arena, record + 0xA) - 341)));
        }
        return arena;
    }

    public static ImmutableArray<NativeBattlePandemona291LayerRequest> Layers(int center, int age)
    {
        int elapsed = unchecked(age - 40);
        int growth = unchecked((int)(unchecked((uint)elapsed << 12) / 60));
        int angle = unchecked(elapsed << 6);
        int firstScale = unchecked((elapsed + 32) << 7);
        int secondScale = unchecked((elapsed + 16) << 8);
        return [new(0x13A0E58, 0x13A4280, center, angle, growth, unchecked(elapsed << 7), growth >> 2, firstScale, 8192, 0, unchecked((byte)(elapsed << 5)), 0), new(0x13A4F88, 0x13A5EF0, center, angle, growth, unchecked((elapsed + 2) << 7), growth >> 1, secondScale, 8192, 1, unchecked((byte)(elapsed << 5)), 0), new(0x13A6508, 0x13A7BF0, center, angle, growth, unchecked((elapsed + 4) << 7), growth >> 1, secondScale, 8192, 1, unchecked((byte)(elapsed * 40)), 0)];
    }

    public static ImmutableArray<NativeBattleVector> Morph6F2D20(ImmutableArray<byte> source, ImmutableArray<byte> target, short ir0)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(4, 4));
        if (count < 0 || count > (source.Length - 8) / 8 || count > 0 && (target.IsDefault || count > (target.Length - 8) / 8))
        {
            throw new InvalidDataException("Native Pandemona morph leaves its actual source or target records.");
        }
        ImmutableArray<NativeBattleVector>.Builder vertices = ImmutableArray.CreateBuilder<NativeBattleVector>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = checked(8 + index * 8);
            vertices.Add(new(Component(offset), Component(offset + 2), Component(offset + 4)));
        }
        return vertices.MoveToImmutable();

        short Component(int offset)
        {
            ushort first = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(offset, 2));
            ushort second = BinaryPrimitives.ReadUInt16LittleEndian(target.AsSpan().Slice(offset, 2));
            return NativeBattlePandemona291Layers.DifferenceGpf45E9D0(first, second, ir0);
        }
    }

    private static int Offset(uint address) => checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    private static ushort ReadWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));

    private static short Trig(ImmutableArray<byte> trigonometry, int angle, bool cosine)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((angle & 0xFFF) * 4 + (cosine ? 2 : 0), 2));
    }
}
