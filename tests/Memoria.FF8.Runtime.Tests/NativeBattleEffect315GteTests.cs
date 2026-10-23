using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315GteTests
{
    [Fact]
    public void Alternate_LIGHT_and_Back_transform_do_not_substitute_ordinary_rotation_or_translation()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleGeometryRegisters ordinary = memory.GeometryRegisters!;
        NativeBattlePackedMatrix32 alternate = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 7, Y = 8, Z = 9 }, null);
        memory = NativeBattleEffect315Gte.LoadRotation45DE50(memory, alternate);
        memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(memory, alternate);
        memory = NativeBattleEffect315Gte.LoadVertex64DEE0(memory, Point(10, -20, 30));
        NativeBattleEffectMemory actual = NativeBattleEffect315Gte.TransformAlternate4607E0(memory);
        Assert.Equal(new int?[] { 17, -12, 39 }, actual.ScriptedColorRegisters!.Mac);
        Assert.Equal(ordinary.Rotation20.ToArray(), actual.GeometryRegisters!.Rotation20.ToArray());
        Assert.Equal(ordinary.Translation.ToArray(), actual.GeometryRegisters.Translation.ToArray());
        Assert.False(actual.ScriptedLighting!.LightMatrix!.Known[18]);
        Assert.False(actual.ScriptedLighting.LightMatrix.Known[19]);
        Assert.Equal(new int?[] { 7, 8, 9 }, Enumerable.Range(0, 3).Select(index => actual.ScriptedLighting.BackColor!.ReadLane(index)).ToArray());
    }

    [Fact]
    public void Unknown_alternate_lanes_and_padding_remain_unknown_without_ordinary_fallback()
    {
        NativeBattlePackedMatrix32 alternate = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        alternate = alternate with { Known = alternate.Known.SetItem(0, false).SetItem(24, false) };
        NativeBattleEffectMemory memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(Memory(), alternate), alternate);
        memory = NativeBattleEffect315Gte.LoadVertex64DEE0(memory, Point(10, 20, 30));
        NativeBattleEffectMemory actual = NativeBattleEffect315Gte.TransformAlternate4607E0(memory);
        Assert.Null(actual.ScriptedColorRegisters!.Mac[0]);
        Assert.Null(actual.ScriptedColorRegisters.Mac[1]);
        Assert.Equal(30, actual.ScriptedColorRegisters.Mac[2]);
        Assert.Null(actual.ScriptedColorRegisters.Flags);
        Assert.Null(actual.ScriptedLighting!.BackColor!.ReadLane(1));
        Assert.False(actual.ScriptedLighting.LightMatrix!.Known[18]);
    }

    [Fact]
    public void Diagonal_uses_signed_low_WORD_with_known_zero_padding_and_preserves_translation()
    {
        NativeBattleEffectMemory before = Memory();
        NativeBattleEffectMemory actual = NativeBattleEffect315Gte.SetDiagonal64DE00(before, unchecked((ushort)0x18001));
        short[] rotation = actual.GeometryRegisters!.Rotation20.Take(18).Select(value => value!.Value).Chunk(2).Select(bytes => BinaryPrimitives.ReadInt16LittleEndian(bytes)).ToArray();
        Assert.Equal(new short[] { -32767, 0, 0, 0, -32767, 0, 0, 0, -32767 }, rotation);
        Assert.Equal((byte?)0, actual.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0, actual.GeometryRegisters.Rotation20[19]);
        Assert.Equal(before.GeometryRegisters!.Translation.ToArray(), actual.GeometryRegisters.Translation.ToArray());
        Assert.Equal(JsonSerializer.Serialize(before.ScriptedLighting), JsonSerializer.Serialize(actual.ScriptedLighting));
    }

    [Fact]
    public void MAC_to_translation_keeps_full_DWORDs_and_requires_all_actual_lanes()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { ScriptedColorRegisters = memory.ScriptedColorRegisters! with { Mac = [0x12345678, -200000, 70000] } };
        NativeBattleEffectMemory actual = NativeBattleEffect315Gte.MacToTranslation64DE40(memory);
        Assert.Equal(new int?[] { 0x12345678, -200000, 70000 }, actual.GeometryRegisters!.Translation);
        memory = memory with { ScriptedColorRegisters = memory.ScriptedColorRegisters with { Mac = [0, null, 0] } };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect315Gte.MacToTranslation64DE40(memory));
    }

    [Fact]
    public void Vertex_and_IR_stores_force_W_zero_and_JSON_continuation_retains_both_control_banks()
    {
        NativeBattleEffectMemory memory = NativeBattleEffect315Gte.LoadVertex64DEE0(Memory(), Point(-1, 2, -3));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6, 2)));
        Assert.True(memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.True(memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        memory = memory with { ScriptedColorRegisters = memory.ScriptedColorRegisters with { Ir = [40000, -40000, 0x12345678] } };
        NativeBattleEffectArena point = NativeBattleEffect315Gte.StoreIr64DF10(memory);
        Assert.Equal(new short[] { unchecked((short)40000), unchecked((short)-40000), 0x5678, 0 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(point.Read(index * 2, 2))).ToArray());
        NativeBattlePackedMatrix32 alternate = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 123, Y = -456, Z = 789 }, 0xBEEF);
        memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(memory, alternate), alternate);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleEffectMemory Next(NativeBattleEffectMemory current) => NativeBattleEffect315Gte.TransformAlternate4607E0(NativeBattleEffect315Gte.LoadVertex64DEE0(current, point));
        Assert.Equal(JsonSerializer.Serialize(Next(memory)), JsonSerializer.Serialize(Next(restored)));
    }

    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000, Y = 2000, Z = 3000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedLighting = NativeBattleScriptedLightingMemory.Unknown() };
}
