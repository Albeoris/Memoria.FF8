using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa86BodyTests
{
    [Fact]
    public void Body_motion_uses_signed_endpoints_and_truncation_and_preserves_fourth_WORD_until_arrival()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(82, 0xA55A).WriteWord(340, 0).WriteWord(342, 513).WriteWord(344, unchecked((ushort)-3));
        task = Endpoints(task, -100, 20, 90, 105, -21, -91, 0x1234);
        NativeBattleModulePoolTask moved = NativeBattleRinoa86Body.Move784070(task);
        Assert.Equal((ushort)513, moved.ReadWord(340));
        Assert.Equal((short)-75, unchecked((short)moved.ReadWord(76)));
        Assert.Equal((short)12, unchecked((short)moved.ReadWord(78)));
        Assert.Equal((short)68, unchecked((short)moved.ReadWord(80)));
        Assert.Equal((ushort)0xA55A, moved.ReadWord(82));
    }

    [Fact]
    public void Arrival_copies_all_eight_original_endpoint_bytes_before_adding_vertical_offset_and_survives_restore()
    {
        NativeBattleModulePoolTask task = Endpoints(Task().WriteWord(340, 4000).WriteWord(342, 274).WriteWord(344, unchecked((ushort)-70)), 1, 2, 3, -321, 20, 987, 0xF123);
        task = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        NativeBattleModulePoolTask moved = NativeBattleRinoa86Body.Move784070(task);
        Assert.Equal((ushort)4096, moved.ReadWord(340));
        Assert.Equal((short)-321, unchecked((short)moved.ReadWord(76)));
        Assert.Equal((short)-50, unchecked((short)moved.ReadWord(78)));
        Assert.Equal((short)987, unchecked((short)moved.ReadWord(80)));
        Assert.Equal((ushort)0xF123, moved.ReadWord(82));
    }

    [Fact]
    public void Motion_wraps_progress_as_native_WORD_before_its_signed_comparison()
    {
        NativeBattleModulePoolTask task = Endpoints(Task().WriteWord(340, 32760).WriteWord(342, 274).WriteWord(82, 0xCCDD), 0, 0, 0, 100, 100, 100, 0xAAAA);
        NativeBattleModulePoolTask moved = NativeBattleRinoa86Body.Move784070(task);
        Assert.Equal(unchecked((ushort)(32760 + 274)), moved.ReadWord(340));
        Assert.Equal((short)-793, unchecked((short)moved.ReadWord(76)));
        Assert.Equal((ushort)0xCCDD, moved.ReadWord(82));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x77FF90, ImmutableArray.CreateRange(new byte[340]));

    private static NativeBattleModulePoolTask Endpoints(NativeBattleModulePoolTask task, short x0, short y0, short z0, short x1, short y1, short z1, ushort fourth)
    {
        return task.WriteWord(320, unchecked((ushort)x0)).WriteWord(322, unchecked((ushort)y0)).WriteWord(324, unchecked((ushort)z0)).WriteWord(328, unchecked((ushort)x1)).WriteWord(330, unchecked((ushort)y1)).WriteWord(332, unchecked((ushort)z1)).WriteWord(334, fourth);
    }
}
