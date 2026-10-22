using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHypnotize133ContextsTests
{
    private const uint Morph = NativeBattleEffectNodeMemory.ArenaAddress + 0xE04;
    private static readonly NativeBattleCameraProjection Projection = new(null, 100, 160, 108);

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void Screen_uses_raw_projected_WORDs_and_separate_focal_shadow_with_unknown_context_padding(int inheritedH)
    {
        NativeBattlePackedMatrix32 camera = Camera(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1, Y = 2, Z = 3 });
        NativeBattleHypnotize133ContextMemoryStep result = NativeBattleHypnotize133Contexts.Screen(Memory(), camera, new(400, -100, 2000), null, 333, Morph, Projection with { FocalLength = (ushort)inheritedH });
        uint screen = result.Memory.GeometryRegisters!.Sxy[2]!.Value;
        Assert.Equal((unchecked((short)screen) - 160) * 16, I32(result.Context40, 20));
        Assert.Equal((unchecked((short)(screen >> 16)) - 120) * 16, I32(result.Context40, 24));
        Assert.Equal(5328, I32(result.Context40, 28));
        Assert.Equal(0, I32(result.Context40, 32));
        Assert.Equal(Morph, unchecked((uint)I32(result.Context40, 36)));
        Assert.Throws<NotSupportedException>(() => result.Context40.Read(18, 2));
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(new int?[] { 1, 2, 3 }, result.Memory.GeometryRegisters.Translation);
        Assert.Null(result.Memory.ScriptedColorRegisters.Material);
    }

    [Fact]
    public void Anchored_uses_full_MAC32_and_CPU_camera_T_without_changing_inherited_GTE_T()
    {
        NativeBattleActionMatrix view = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattlePackedMatrix32 camera = Camera(view);
        byte[] bytes = camera.Bytes.ToArray();
        bool[] known = camera.Known.ToArray();
        bytes[19] = 0xAA;
        known[19] = true;
        camera = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleHypnotize133ContextMemoryStep result = NativeBattleHypnotize133Contexts.Anchored(Memory(), camera, new(10000, 2, 3), null, Morph);
        Assert.Equal(129998, I32(result.Context40, 20));
        Assert.Equal(-1, I32(result.Context40, 24));
        Assert.Equal(2003, I32(result.Context40, 28));
        Assert.Equal(-128, I32(result.Context40, 32));
        Assert.Equal(new int?[] { 111, 222, 333 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0xAA, result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Throws<NotSupportedException>(() => result.Context40.Read(18, 2));
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(79998, result.Memory.ScriptedColorRegisters.Mac[0]);
        Assert.Equal(32767, result.Memory.ScriptedColorRegisters.Ir[0]);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(-2048)]
    public void Fading_publishes_three_GPF_FIFO_rows_then_native_camera_transform_with_factor_in_packed_W(int factor)
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { ScriptedColorRegisters = memory.ScriptedColorRegisters! with { Material = 0x44112233, Rgb = [1u, 2u, 3u] } };
        NativeBattleHypnotize133ContextMemoryStep result = NativeBattleHypnotize133Contexts.Fading(memory, Camera(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 }), new(100, 200, 300), (short)factor, Morph, NativeBattleGteColorInterpolation.CodeConstants45F270);
        short[] expected = [(short)factor, 0, 0, 0, (short)factor, 0, 0, 0, (short)factor];
        Assert.Equal(expected, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Context40.Read(index * 2, 2))));
        Assert.Equal(new[] { 110, 220, 330 }, Enumerable.Range(0, 3).Select(axis => I32(result.Context40, 20 + axis * 4)));
        Assert.Equal(unchecked((uint)factor), result.Memory.ScriptedColorRegisters!.Ir0);
        uint channel = factor > 0 ? 255u : 0u;
        Assert.Equal(new uint?[] { 0x44000000u | channel, 0x44000000u | channel << 8, 0x44000000u | channel << 16 }, result.Memory.ScriptedColorRegisters.Rgb);
        Assert.Equal(0x44112233u, result.Memory.ScriptedColorRegisters.Material);
        Assert.Equal((short)factor, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan(6, 2)));
        Assert.Equal(new int?[] { 111, 222, 333 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Throws<NotSupportedException>(() => result.Context40.Read(18, 2));
    }

    [Fact]
    public void Unknown_material_code_does_not_block_Fading_geometry_and_remains_unknown_after_JSON()
    {
        NativeBattleHypnotize133ContextMemoryStep first = NativeBattleHypnotize133Contexts.Fading(Memory(), Camera(NativeBattleCuragaMatrices.Identity8DD770()), new(1, 2, 3), 2048, Morph, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.All(first.Memory.ScriptedColorRegisters!.Rgb, value => Assert.Null(value));
        Assert.Equal(1, I32(first.Context40, 20));
        NativeBattleHypnotize133ContextMemoryStep restored = JsonSerializer.Deserialize<NativeBattleHypnotize133ContextMemoryStep>(JsonSerializer.Serialize(first))!;
        NativeBattleHypnotize133ContextMemoryStep Next(NativeBattleHypnotize133ContextMemoryStep value) => NativeBattleHypnotize133Contexts.Fading(value.Memory, Camera(NativeBattleCuragaMatrices.Identity8DD770()), new(1, 2, 3), 2048, Morph, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void BonePoint_uses_required_current_matrices_and_IR_WORD_result_without_rebuilding_pose()
    {
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770() with { X = 50000 };
        NativeBattleActionMatrix bone = NativeBattleCuragaMatrices.Identity8DD770() with { X = 2 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = null, ScriptedColorRegisters = null };
        NativeBattleHypnotize133BonePointStep result = NativeBattleHypnotize133Contexts.BonePoint(memory, world, bone, new(10000, 2, 3, 7));
        Assert.Equal(new NativeBattleVector(32767, 2, 3), result.Position);
        Assert.Equal(60002, result.Memory.ScriptedColorRegisters!.Mac[0]);
        Assert.Equal((short)7, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan(6, 2)));
        Assert.All(result.Memory.GeometryRegisters!.Sxy, value => Assert.Null(value));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.ScriptedColorRegisters.Material);
    }

    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattlePackedMatrix32 Camera(NativeBattleActionMatrix matrix) => NativeBattlePackedMatrix32.FromMatrix(matrix, null);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Translation = [111, 222, 333] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
}
