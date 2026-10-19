using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicAnchorTests
{
    [Fact]
    public void Selected_basis_transforms_cached_position_before_global_point_projection_and_returns_cue_instead_of_depth()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9C, 0xFFFF);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797204, 7);
        short[] rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096];
        for (int index = 0; index < rotation.Length; index++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, 0x27979E8 + (uint)(index * 2), unchecked((ushort)rotation[index]));
        }
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27979E8 + 20, 10);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27979E8 + 24, 20);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27979E8 + 28, 3000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 200);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 1000);
        frame = frame with { Core = frame.Core.Write(0x27971E4, new byte[6]).MarkUnknown(0x27979E8 + 18, 2) };
        NativeBattleWaterSlotBank bank = new(0x2000, 0x400, 4, 0x2000);
        NativeBattleCinematicSpriteProjection result = NativeBattleCinematicAnchor.Project(frame, bank, new(null, 216, 160, 108), 0, 8192);
        Assert.Equal((ushort)149, result.Frame.ReadTemporaryWord(0xD0));
        Assert.Equal((ushort)114, result.Frame.ReadTemporaryWord(0xD2));
        Assert.Equal(2, result.Cue);
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xFC));
        Assert.Equal(frame.ReadTemporaryDword(0xF0), result.Frame.ReadTemporaryDword(0xF0));
        Assert.Same(frame.Core, result.Frame.Core);
        Assert.Same(frame.Arena, result.Frame.Arena);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicAnchor.Project(restored, bank, new(null, 216, 160, 108), 0, 8192)));
    }
}
