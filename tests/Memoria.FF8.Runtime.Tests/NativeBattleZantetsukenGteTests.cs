using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleZantetsukenGteTests
{
    [Fact]
    public void Plane_installation_changes_only_two_light_DWORDs_and_red_back_control()
    {
        byte[] bytes = Enumerable.Range(1, 20).Select(value => (byte)value).ToArray();
        bool[] known = Enumerable.Range(0, 20).Select(value => value % 2 == 0).ToArray();
        NativeBattleScriptedLightingMatrix light = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleScriptedLightingMemory lighting = new(light, light, new(10, 20, 30) { Known = [true, false, true] });
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedLighting = lighting };
        NativeBattleEffectMemory result = NativeBattleZantetsukenGte.LoadPlane(memory, Plane(1, -2, 3, -4));
        Assert.Equal(Plane(1, -2, 3, -4), result.ScriptedLighting!.LightMatrix!.Bytes.Take(8));
        Assert.All(result.ScriptedLighting.LightMatrix.Known.Take(8), value => Assert.True(value));
        Assert.Equal(bytes.Skip(8), result.ScriptedLighting.LightMatrix.Bytes.Skip(8));
        Assert.Equal(known.Skip(8), result.ScriptedLighting.LightMatrix.Known.Skip(8));
        Assert.Same(light, result.ScriptedLighting.ColorMatrix);
        Assert.Equal(-4, result.ScriptedLighting.BackColor!.ReadLane(0));
        Assert.Null(result.ScriptedLighting.BackColor.ReadLane(1));
        Assert.Equal(30, result.ScriptedLighting.BackColor.ReadLane(2));
        Assert.Same(memory.GeometryRegisters, result.GeometryRegisters);
        Assert.Same(memory.ScriptedColorRegisters, result.ScriptedColorRegisters);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Partial_plane_controls_publish_selected_distance_and_keep_other_outputs_unknown(int slot)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedLighting = null };
        NativeBattleGeometryRegisterStep input = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x12345678, Ir0 = 99, Rgb = [11, 22, 33] });
        input = NativeBattleGeometry.LoadVector(input, slot, new(10, 100, 50), null);
        memory = memory with { GeometryRegisters = input.Geometry, ScriptedColorRegisters = input.Colors };
        memory = NativeBattleZantetsukenGte.LoadPlane(memory, Plane(0, -4096, 0, 7));
        NativeBattleEffectMemory result = NativeBattleZantetsukenGte.EvaluatePlane(memory, slot);
        Assert.Equal(new int?[] { -93, null, null }, result.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { -93, null, null }, result.ScriptedColorRegisters.Ir);
        Assert.Null(result.ScriptedColorRegisters.Flags);
        Assert.Equal(0x12345678u, result.ScriptedColorRegisters.Material);
        Assert.Equal(99u, result.ScriptedColorRegisters.Ir0);
        Assert.Equal(new uint?[] { 11, 22, 33 }, result.ScriptedColorRegisters.Rgb);
        Assert.Same(input.Geometry, result.GeometryRegisters);
        Assert.Same(memory.ScriptedLighting, result.ScriptedLighting);
        Assert.Null(result.ScriptedLighting!.BackColor!.ReadLane(1));
        Assert.Null(result.ScriptedLighting.BackColor.ReadLane(2));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleZantetsukenGte.EvaluatePlane(restored, slot)));
    }

    [Fact]
    public void Fully_known_remaining_light_rows_produce_all_MAC_IR_and_saturation_flags()
    {
        byte[] identity = new byte[20];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(identity.AsSpan(index * 8), 4096);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedLighting = new(new(identity.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray()), null, new(50, 100000, -100000)) };
        NativeBattleGeometryRegisterStep input = NativeBattleGeometry.LoadVector(new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), 2, new(4096, -32768, 32767), 123);
        memory = memory with { GeometryRegisters = input.Geometry, ScriptedColorRegisters = input.Colors };
        memory = NativeBattleZantetsukenGte.LoadPlane(memory, Plane(0, -4096, 0, 2));
        NativeBattleEffectMemory result = NativeBattleZantetsukenGte.EvaluatePlane(memory, 2);
        Assert.Equal(new int?[] { 32770, 67234, -67233 }, result.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 32767, 32767, -32768 }, result.ScriptedColorRegisters.Ir);
        Assert.Equal(0x81C00000u, result.ScriptedColorRegisters.Flags);
        Assert.Equal(input.Geometry, result.GeometryRegisters);
    }

    [Theory]
    [InlineData(2048, 0)]
    [InlineData(6144, 2)]
    [InlineData(-2048, 0)]
    [InlineData(-6144, -2)]
    public void Plane_dot_uses_native_biased_rounding_before_adding_raw_back(short x, int expected)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), 0, new(x, 0, 0), null);
        memory = memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
        memory = NativeBattleZantetsukenGte.LoadPlane(memory, Plane(1, 0, 0, 3));
        Assert.Equal(expected + 3, NativeBattleZantetsukenGte.EvaluatePlane(memory, 0).ScriptedColorRegisters!.Mac[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleZantetsukenGte.EvaluatePlane(memory, 3));
    }

    private static byte[] Plane(short x, short y, short z, short w)
    {
        byte[] bytes = new byte[8];
        short[] values = [x, y, z, w];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        }
        return bytes;
    }
}
