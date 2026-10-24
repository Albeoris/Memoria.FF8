using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Clouds
{
    public static NativeBattleEffect66Step Advance(NativeBattleEffect66Frame frame, uint callback, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs) => callback switch
    {
        0x622EB0 => AdvanceStreaks(frame, task, resources, inputs),
        0x624DF0 or 0x625110 or 0x625470 or 0x626F80 => AdvanceSpritePool(frame, callback, task, resources, inputs),
        0x626690 => AdvanceScrolling(frame, task, resources, inputs),
        0x627210 => AdvanceGroundStreaks(frame, task, resources, inputs),
        0x627890 or 0x627BD0 => AdvanceArenaSparks(frame, callback, task, resources, inputs),
        _ => throw new NotSupportedException("Effect66 reached an unsupported cloud callback.")
    };

    private static bool Paused(NativeBattleEffect66Frame frame, NativeBattleEffect66Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static NativeBattleEffect66Frame Random(NativeBattleEffect66Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort output);
        value = output;
        return frame with { Random = random };
    }
    private static short S16(NativeBattleEffectArena source, int at) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(at, 2));
    private static uint U32(NativeBattleEffectArena source, int at) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(at, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena source, int at, int value) => source.Write(at, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena source, int at, int value) => source.Write(at, BitConverter.GetBytes(value));
    private static NativeBattleVector Point(NativeBattleEffectArena source, int at) => new(S16(source, at), S16(source, at + 2), S16(source, at + 4));
    private static NativeBattleEffectArena Point(NativeBattleEffectArena target, int at, NativeBattleVector point) => Word(Word(Word(target, at, point.X), at + 2, point.Y), at + 4, point.Z);
    private static short? OptionalWord(NativeBattleEffectArena source, int at)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(at, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static NativeBattleEffectArena Rotation(NativeBattleEffectArena target, int at, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            target = Word(target, at + index * 2, matrix.Rotation[index]);
        }
        return target;
    }
    private static NativeBattleEffectArena Translation(NativeBattleEffectArena target, int at, int x, int y, int z) => Put(Put(Put(target, at + 20, x), at + 24, y), at + 28, z);
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena source, int at)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(at, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static uint Free(NativeBattleEffect66Frame frame, uint start, int count)
    {
        for (int index = 0; index < count; index++)
        {
            uint address = checked(start + (uint)(index * 24));
            if (U32(ReadBytes(frame, address, 4), 0) == 0)
            {
                return address;
            }
        }
        return 0;
    }
    private static NativeBattleEffect66Step Finish(NativeBattleEffect66Frame frame, uint task, int remaining, NativeBattleEffect66Inputs inputs)
    {
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        frame = Write(frame, task, Read(frame, task).WriteWord(12, unchecked((ushort)(Read(frame, task).ReadWord(12) + 1))));
        return new(frame, remaining == 0 ? 2 : 0);
    }

    private static (NativeBattleEffect66Frame Frame, NativeBattleEffectArena Outer) SpritePoint(NativeBattleEffect66Frame frame, NativeBattleEffectArena outer, NativeBattleEffectArena row, int offset, int scale, int radius)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, Point(row, offset), OptionalWord(row, offset + 6));
        frame = frame with { Memory = NativeBattleLightBackTransform.Transform(Publish(frame, registers).Memory, 0) };
        registers = Registers(frame);
        NativeBattleActionMatrix rotation = new([unchecked((short)scale), 0, 0, 0, unchecked((short)scale), 0, 0, 0, unchecked((short)scale)], 0, 0, 0, 4096);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, rotation, true, false, 0) };
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        outer = Put(Put(Put(outer, 0x38, x), 0x3C, y), 0x40, z);
        var unit = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        outer = Put(Put(Put(outer, 0x28, unit.X), 0x2C, unit.Y), 0x30, unit.Z);
        x = unchecked(x + (unchecked(unit.X * radius) >> 12));
        y = unchecked(y + (unchecked(unit.Y * radius) >> 12));
        z = unchecked(z + (unchecked(unit.Z * radius) >> 12));
        outer = Put(Put(Put(outer, 0x38, x), 0x3C, y), 0x40, z);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, rotation with { X = x, Y = y, Z = z }, false, true, null) };
        return (Publish(frame, registers), outer);
    }
}
