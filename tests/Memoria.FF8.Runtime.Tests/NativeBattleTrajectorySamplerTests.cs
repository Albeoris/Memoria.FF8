using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTrajectorySamplerTests
{
    [Theory]
    [InlineData(8, 4, 550)]
    [InlineData(9, 5, 650)]
    [InlineData(10, 6, 750)]
    public void Final_multi_recipient_segment_preserves_unknown_padding_and_JSON_continuation(int pointCount, int segment, int expected)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [0xAA010101u, 0xAA020202u, 0xAA030303u], Material = 0xAA000000 } };
        NativeBattleEffectArena controls = NativeBattleEffectArena.Clear().MarkUnknown(0, 88);
        for (int index = segment; index < pointCount; index++)
        {
            controls = controls.Write(index * 8, BitConverter.GetBytes((short)(index * 100))).Write(index * 8 + 2, BitConverter.GetBytes((short)(index * 200))).Write(index * 8 + 4, BitConverter.GetBytes((short)(index * -100)));
        }
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(6, 2).Write(8, new byte[] { 0x81, 0x82 });
        NativeBattleTrajectoryStep result = NativeBattleTrajectorySampler.Sample(memory, controls, pointCount, segment, 2048, retained, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(expected, BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(0, 2)));
        Assert.Equal(expected * 2, BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(2, 2)));
        Assert.Equal(-expected, BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(4, 2)));
        Assert.Throws<NotSupportedException>(() => result.Output8.Read(6, 2));
        Assert.Equal(new byte[] { 0x81, 0x82 }, result.Output8.Read(8, 2));
        NativeBattleEffectMemory restoredMemory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleEffectArena restoredControls = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(controls))!;
        NativeBattleTrajectoryStep restored = NativeBattleTrajectorySampler.Sample(restoredMemory, restoredControls, pointCount, segment, 2048, retained, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => NativeBattleTrajectorySampler.Sample(memory, controls, pointCount, segment + 1, 2048, retained, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }
}
