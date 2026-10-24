using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect285BonePointTests
{
    [Fact]
    public void Current_world_and_local_bone_compose_before_transforming_the_original_selector_point()
    {
        NativeBattleActionMatrix world = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 1000, 2000, 3000, 4096);
        NativeBattleActionMatrix bone = Identity(10, 20, 30);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectArena local = Point(100, 200, 300, 10);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(6, new byte[] { 0xEF, 0xBE, 0x91, 0x92 });
        NativeBattleEffect285BonePointResult result = NativeBattleEffect285BonePoint.Transform(memory, world, bone, local, retained);
        Assert.Equal(new short[] { 1330, 2220, 2890 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(axis * 2, 2))));
        Assert.Equal(new byte[] { 0xEF, 0xBE, 0x91, 0x92 }, result.Output8.Read(6, 4));
        Assert.Equal(new int?[] { 1030, 2020, 2990 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(local.Read(0, 8), result.Memory.ScriptedColorRegisters!.NormalInput!.Value.ToArray());
        Assert.Same(memory.ScriptedLighting, result.Memory.ScriptedLighting);
    }

    [Fact]
    public void Output_uses_saturated_IR_words_while_full_MAC_and_unknown_output_padding_survive()
    {
        NativeBattleEffect285BonePointResult result = NativeBattleEffect285BonePoint.Transform(Memory(), Identity(40000, -40000, 70000), Identity(0, 0, 0), Point(10, -20, 30, 10), NativeBattleEffectArena.Clear().MarkUnknown(6, 2));
        Assert.Equal(new int?[] { 40010, -40020, 70030 }, result.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new short[] { short.MaxValue, short.MinValue, short.MaxValue }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(axis * 2, 2))));
        Assert.Throws<NotSupportedException>(() => result.Output8.Read(6, 2));
    }

    [Fact]
    public void Restored_arithmetic_owners_produce_the_same_next_point_without_allocating_temporary_memory()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleEffect285BonePointResult Next(NativeBattleEffectMemory current) => NativeBattleEffect285BonePoint.Transform(current, Identity(100, 200, 300), Identity(4, 5, 6), Point(1, 2, 3, 10), NativeBattleEffectArena.Clear().MarkUnknown(6, 2));
        Assert.Equal(JsonSerializer.Serialize(Next(memory)), JsonSerializer.Serialize(Next(restored)));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect285BonePoint.Transform(memory, Identity(0, 0, 0), Identity(0, 0, 0), Point(1, 2, 3, 10).MarkUnknown(6, 2), NativeBattleEffectArena.Clear()));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattleActionMatrix Identity(int x, int y, int z) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, y, z, 4096);
    private static NativeBattleEffectArena Point(short x, short y, short z, short selector) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(6, BitConverter.GetBytes(selector));
}
