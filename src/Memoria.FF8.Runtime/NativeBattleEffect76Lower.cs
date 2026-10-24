using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect76Lower
{
    public const uint FirstRecords = 0x24C32A0;
    public const uint SecondRecords = 0x24C3F20;
    public const uint Point = 0x24C3270;
    public const uint PrivateCursor = 0x24C66A4;
    public const uint CommonCursor = 0x1D8E054;

    public static NativeBattleEffect76Step Advance(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs)
    {
        uint callback = Read(frame, address).Callback;
        return callback switch
        {
            0x6201A0 => NativeBattleEffect76Particles.Advance(frame, address, 0, resources, inputs),
            0x620530 => NativeBattleEffect76Particles.Advance(frame, address, 1, resources, inputs),
            0x620820 => NativeBattleEffect76Particles.Advance(frame, address, 2, resources, inputs),
            0x621250 => NativeBattleEffect76Particles.Advance(frame, address, 3, resources, inputs),
            0x6215E0 => NativeBattleEffect76Particles.Advance(frame, address, 4, resources, inputs),
            0x620BD0 or 0x620DD0 or 0x620F60 or 0x621110 => NativeBattleEffect76Bursts.Advance(frame, address, resources, inputs),
            0x622560 => Cleanup(frame, address, inputs),
            _ => throw new NotSupportedException($"Effect76 lower callback {callback:X8} is unsupported.")
        };
    }

    public static NativeBattleEffect76FactoryStep Invoke(NativeBattleEffect76Frame frame, uint function, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs) => NativeBattleEffect76Bursts.Factory(frame, function, inputs);

    private static NativeBattleEffect76Step Cleanup(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76CommonTextureLoad508630(checked(Dword(frame, 0x24C66A0) + 0x18000), checked(address + 14)));
        }
        task = Read(frame, address);
        ushort completion = task.ReadWord(14);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, completion != 0 ? 2 : 0);
    }

    internal static short Short(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
    internal static uint Dword(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(row.Read(offset, 4));
    internal static uint Dword(NativeBattleEffect76Frame frame, uint address) => Dword(ReadBytes(frame, address, 4), 0);
    internal static NativeBattleEffectArena Word(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    internal static NativeBattleEffectArena Long(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(value));
    internal static NativeBattleEffectArena Marker(NativeBattleEffectArena row, int value) => Long(row, 0, value);
    internal static bool Paused(NativeBattleEffect76Frame frame, NativeBattleEffect76Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    internal static int Roll(ref NativeBattleEffect76Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    internal static NativeBattleEffectArena PutVector(NativeBattleEffectArena row, int offset, int x, int y, int z) => Word(Word(Word(row, offset, x), offset + 2, y), offset + 4, z);
    internal static NativeBattleVector Vector(NativeBattleEffectArena row, int offset) => new(Short(row, offset), Short(row, offset + 2), Short(row, offset + 4));
    internal static NativeBattleEffect76Step Tick(NativeBattleEffect76Frame frame, uint address, int result)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), result);
    }

    internal static NativeBattleEffectArena WriteRotation(NativeBattleEffectArena output, int offset, NativeBattleActionMatrix matrix, bool paddingUnknown)
    {
        for (int component = 0; component < 9; component++)
        {
            output = Word(output, offset + component * 2, matrix.Rotation[component]);
        }
        return paddingUnknown ? output.MarkUnknown(offset + 18, 2) : output;
    }

    internal static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena output, int offset, NativeBattleActionMatrix matrix) => Long(Long(Long(WriteRotation(output, offset, matrix, true), offset + 20, matrix.X), offset + 24, matrix.Y), offset + 28, matrix.Z);

    internal static NativeBattleEffect76Frame Load(NativeBattleEffect76Frame frame, NativeBattleEffectArena matrix, bool translation = true)
    {
        (byte[] bytes, bool[] known) = matrix.ReadSnapshot(0, 32);
        return LoadMatrix(frame, new(bytes.ToImmutableArray(), known.ToImmutableArray()), true, translation);
    }
}
