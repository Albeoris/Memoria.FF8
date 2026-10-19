using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Fact]
    public void Constant_add_06_uses_selected_fixed_point_lanes_and_preserves_random()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x7006, 0xFFFF, 0x7FFF, 2);
        frame = frame with { Arena = frame.Arena.Write(Particle + 0x14, BitConverter.GetBytes(128u)).Write(Particle + 0x18, BitConverter.GetBytes(0xFFF00000u)).Write(Particle + 0x1C, BitConverter.GetBytes(0xFFFFFF00u)).MarkUnknown(Particle + 0x10, 4) };
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(0xFFFFFF80u, Dword(result, 0x14));
        Assert.Equal(0x006FFF00u, Dword(result, 0x18));
        Assert.Equal(0x100u, Dword(result, 0x1C));
        Assert.Throws<NotSupportedException>(() => Dword(result, 0x10));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(0x1008u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(One(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }

    [Fact]
    public void Constant_add_06_ignores_group_bits_and_sentinel_does_not_read_old_lane()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF306, 0x7654, 0, 1, 0x8000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle + 0x10, 4).MarkUnknown(Particle + 0x20, 0x10) };
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Throws<NotSupportedException>(() => Dword(result, 0x10));
        Assert.Equal(0x100u, Dword(result, 0x18));
        Assert.Equal(0xFF800000u, Dword(result, 0x1C));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x20, 2));
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Constant_zero_add_still_requires_the_selected_old_value()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8006, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle + 0x10, 4) };
        Assert.Throws<NotSupportedException>(() => One(frame));
    }
}
