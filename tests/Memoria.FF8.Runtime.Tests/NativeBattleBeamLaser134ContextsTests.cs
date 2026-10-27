using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBeamLaser134ContextsTests
{
    private const uint Morph = NativeBattleEffectNodeMemory.ArenaAddress + 0xC60;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotatePoint_stores_IR_WORD_XYZ_before_wrapped_anchor_add_and_preserves_source_W(bool knownW)
    {
        NativeBattlePackedMatrix32 source = RotationOnly(new([8192, 0, 0, 0, 8192, 0, 0, 0, 8192], 900000, -800000, 700000, 4096));
        short? w = knownW ? (short)0x1234 : null;
        NativeBattleBeamLaser134PointMemoryStep result = NativeBattleBeamLaser134Contexts.RotatePoint(Memory(), source, new(20000, -20000, 3), w, new(10, -10, 0), Camera(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 }), Morph);
        Assert.Equal(new NativeBattleVector(-32759, 32758, 6), result.UpdatedLocal);
        Assert.Equal(new[] { -32659, 32958, 306 }, Enumerable.Range(0, 3).Select(axis => I32(result.Context40, 20 + axis * 4)));
        Assert.Equal(w, result.LocalW);
        Assert.Equal(knownW, result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(knownW, result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        if (knownW)
        {
            Assert.Equal((short)0x1234, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan(6, 2)));
        }
        Assert.Equal(new int?[] { 111, 222, 333 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Equal(new uint?[] { 1, 2, 3 }, result.Memory.ScriptedColorRegisters.Rgb);
        Assert.All(result.Memory.GeometryRegisters.Sxy, value => Assert.Null(value));
        Assert.Throws<NotSupportedException>(() => result.Context40.Read(18, 2));
    }

    [Fact]
    public void Final_camera_transform_uses_MAC32_for_context_translation_and_keeps_partial_R_padding()
    {
        NativeBattlePackedMatrix32 camera = Camera(new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096));
        byte[] bytes = camera.Bytes.ToArray();
        bool[] known = camera.Known.ToArray();
        bytes[19] = 0xAA;
        known[19] = true;
        camera = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleBeamLaser134PointMemoryStep result = NativeBattleBeamLaser134Contexts.RotatePoint(Memory(), RotationOnly(NativeBattleCuragaMatrices.Identity8DD770()), new(10000, 2, 3), null, default, camera, Morph);
        Assert.Equal(129998, I32(result.Context40, 20));
        Assert.Equal(79998, result.Memory.ScriptedColorRegisters!.Mac[0]);
        Assert.Equal(32767, result.Memory.ScriptedColorRegisters.Ir[0]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal((byte?)0xAA, result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(0, I32(result.Context40, 32));
        Assert.Equal(Morph, unchecked((uint)I32(result.Context40, 36)));
        Assert.Throws<NotSupportedException>(() => result.Context40.Read(18, 2));
    }

    [Fact]
    public void Source_T_is_unconsumed_but_actual_camera_T_is_required_at_its_CPU_add()
    {
        NativeBattlePackedMatrix32 rotationOnly = RotationOnly(NativeBattleCuragaMatrices.Identity8DD770());
        Assert.Throws<NotSupportedException>(() => NativeBattleBeamLaser134Contexts.RotatePoint(Memory(), rotationOnly, new(1, 2, 3), null, default, rotationOnly, Morph));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Child_uses_CPU_camera_composition_and_preserves_unknown_output_stack_padding(bool rotate)
    {
        NativeBattleActionMatrix camera = rotate ? new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096) : NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 };
        NativeBattlePackedMatrix32 saved = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 7, Y = 8, Z = 9 }, 0x1234);
        NativeBattleEffectArena result = NativeBattleBeamLaser134Contexts.Child(NativeBattlePackedMatrix32.FromMatrix(camera, 0x5678), saved, Morph);
        Assert.Equal(rotate ? new[] { 109, 208, 293 } : new[] { 107, 208, 309 }, Enumerable.Range(0, 3).Select(axis => I32(result, 20 + axis * 4)));
        Assert.Throws<NotSupportedException>(() => result.Read(18, 2));
        Assert.Equal(0, I32(result, 32));
        Assert.Equal(Morph, unchecked((uint)I32(result, 36)));
    }

    [Fact]
    public void JSON_next_preserves_updated_local_W_and_retained_GTE_state()
    {
        NativeBattlePackedMatrix32 matrix = Camera(NativeBattleCuragaMatrices.Identity8DD770());
        NativeBattleBeamLaser134PointMemoryStep first = NativeBattleBeamLaser134Contexts.RotatePoint(Memory(), matrix, new(10, 20, 30), null, new(1, 2, 3), matrix, Morph);
        NativeBattleBeamLaser134PointMemoryStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBeamLaser134PointMemoryStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleBeamLaser134PointMemoryStep Next(NativeBattleBeamLaser134PointMemoryStep value) => NativeBattleBeamLaser134Contexts.RotatePoint(value.Memory, matrix, value.UpdatedLocal, value.LocalW, new(1, 2, 3), matrix, Morph);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattlePackedMatrix32 Camera(NativeBattleActionMatrix matrix) => NativeBattlePackedMatrix32.FromMatrix(matrix, null);
    private static NativeBattlePackedMatrix32 RotationOnly(NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = Camera(matrix);
        bool[] known = packed.Known.ToArray();
        Array.Fill(known, false, 20, 12);
        return packed with { Known = known.ToImmutableArray() };
    }
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Translation = [111, 222, 333] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [1, 2, 3] } };
}
