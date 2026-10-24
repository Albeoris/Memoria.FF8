using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65CasterPointTests
{
    [Fact]
    public void Wide_point_keeps_full_world_translation_and_publishes_source_w_and_unknown_matrix_padding()
    {
        NativeBattleActionMatrix world = Identity() with { X = 100000, Y = -200000, Z = 300000 };
        NativeBattleActionMatrix bone = Identity() with { X = 7, Y = 8, Z = 9 };
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 16).ToArray());
        NativeBattleEffect65CasterPointStep step = NativeBattleEffect65CasterPoint.Transform(NativeBattleEffectMemory.FromColdProcess(), world, bone, Point(), output, true);
        Assert.Equal(100008, Dword(step.Output, 0));
        Assert.Equal(-199990, Dword(step.Output, 4));
        Assert.Equal(300012, Dword(step.Output, 8));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, step.Output.Read(12, 4));
        Assert.Equal(new int?[] { 100008, -199990, 300012 }, step.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(0x81C00000u, step.Memory.ScriptedColorRegisters.Flags);
        Assert.Null(step.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(step.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal((byte)37, step.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.Equal((byte)0, step.Memory.ScriptedColorRegisters.NormalInput!.Value[7]);
    }

    [Fact]
    public void Word_point_stores_saturated_ir_and_preserves_the_existing_output_suffix_knownness()
    {
        NativeBattleActionMatrix world = Identity() with { X = 65535, Y = -65539, Z = 131069 };
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(6, new byte[] { 0x34, 0x12 });
        NativeBattleEffect65CasterPointStep step = NativeBattleEffect65CasterPoint.Transform(NativeBattleEffectMemory.FromColdProcess(), world, Identity(), Point(), output, false);
        Assert.Equal(short.MaxValue, Word(step.Output, 0));
        Assert.Equal(short.MinValue, Word(step.Output, 2));
        Assert.Equal(short.MaxValue, Word(step.Output, 4));
        Assert.Equal(new byte[] { 0x34, 0x12 }, step.Output.Read(6, 2));
        Assert.All(step.Output.ReadSnapshot(8, 4).Known, known => Assert.False(known));
        Assert.Equal(new int?[] { 65536, -65537, 131072 }, step.Memory.ScriptedColorRegisters!.Mac);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattleEffectArena Point()
    {
        return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)1)).Write(2, BitConverter.GetBytes((short)2)).Write(4, BitConverter.GetBytes((short)3)).Write(6, BitConverter.GetBytes((short)37));
    }

    private static int Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
