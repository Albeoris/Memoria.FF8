using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227BonePointTests
{
    [Fact]
    public void Current_world_composes_local_bone_before_packed_point_and_retains_output_W()
    {
        NativeBattleActionMatrix world = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        NativeBattleActionMatrix bone = NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 };
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(6, 2);
        NativeBattleEffect227BonePointStep result = NativeBattleEffect227BonePoint.Transform693990(Memory(), world, bone, Point(), output);
        Assert.Equal(new short[] { 133, 222, 289 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(axis * 2, 2))));
        Assert.DoesNotContain(true, result.Output8.ReadSnapshot(6, 2).Known);
        Assert.Equal((byte)50, result.Memory.ScriptedColorRegisters!.NormalInput!.Value[6]);
        Assert.Equal((byte)0, result.Memory.ScriptedColorRegisters.NormalInput.Value[7]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
    }

    [Fact]
    public void Full_MAC_saturates_IR_without_changing_output_padding_and_survives_JSON_next_call()
    {
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000, Y = -40000 };
        NativeBattleActionMatrix bone = NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 };
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xA55A));
        NativeBattleEffect227BonePointStep result = NativeBattleEffect227BonePoint.Transform693990(Memory(), world, bone, Point(), output);
        Assert.Equal(new int?[] { 40011, -39978, 33 }, result.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(0, 2)));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(2, 2)));
        Assert.Equal((ushort)0xA55A, BinaryPrimitives.ReadUInt16LittleEndian(result.Output8.Read(6, 2)));
        Assert.NotEqual(0u, result.Memory.ScriptedColorRegisters.Flags);
        NativeBattleEffect227BonePointStep restored = JsonSerializer.Deserialize<NativeBattleEffect227BonePointStep>(JsonSerializer.Serialize(result))!;
        NativeBattleEffect227BonePointStep directNext = NativeBattleEffect227BonePoint.Transform693990(result.Memory, world, bone, Point(), result.Output8);
        NativeBattleEffect227BonePointStep restoredNext = NativeBattleEffect227BonePoint.Transform693990(restored.Memory, world, bone, Point(), restored.Output8);
        Assert.Equal(JsonSerializer.Serialize(directNext), JsonSerializer.Serialize(restoredNext));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };

    private static NativeBattleEffectArena Point()
    {
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear();
        foreach ((int offset, short value) in new (int, short)[] { (0, 1), (2, 2), (4, 3), (6, 50) })
        {
            point = point.Write(offset, BitConverter.GetBytes(value));
        }
        return point;
    }
}
