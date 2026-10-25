using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect233Shadow71Tests
{
    private const uint Actor = 0x1D972C0 + 156 * 3;

    [Fact]
    public void Clear_restores_default_shadow_without_reading_render_owners_or_drawing()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8071);
        frame = frame with { Core = frame.Core.MarkUnknown(0x27973B8, 4).MarkUnknown(0x27973BC, 4).MarkUnknown(0x27973EC, 4) };
        ushort flags = 0xFFFF;
        NativeBattleCinematicShadow71Services services = new(address =>
        {
            Assert.Equal(Actor, address);
            return flags;
        }, (address, value) =>
        {
            Assert.Equal(Actor, address);
            flags = value;
        }, null!);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicShadow71.Execute(frame, services);
        Assert.Equal((ushort)0xFFDF, flags);
        Assert.Equal(0x80001002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x27973BC, 4));
    }

    [Fact]
    public void Explicit_shadow_sets_flag_before_current_owner_pass_and_publishes_returned_cursor_after_restore()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x71);
        frame = frame with { Core = frame.Core.MarkUnknown(0x27973BC, 4) };
        ushort flags = 0xAB05;
        int calls = 0;
        NativeBattleCinematicShadow71Services services = new(_ => flags, (_, value) => flags = value, (current, actor, ordering, mode, output) =>
        {
            Assert.Equal((ushort)0xAB25, flags);
            Assert.Equal(Actor, actor);
            Assert.Equal(0x1040u, ordering);
            Assert.Equal((byte)4, mode);
            Assert.Equal(0x200u, output);
            calls++;
            current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x80001010);
            return new(current, 0x2F0);
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicShadow71.Execute(frame, services);
        Assert.Equal(1, calls);
        Assert.Equal(0x2F0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0x80001012u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        flags = 0xAB05;
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame restoredResult = NativeBattleCinematicShadow71.Execute(restored, services);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restoredResult));
        Assert.Equal(2, calls);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [3]) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, 0x2797068);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797068 + 0x60 + 4 * 3, Actor);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, checked(frame.TemporaryFrame.Address + 0x100));
        frame = frame.WriteTemporaryDword(0x14C, 0x1040);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, 0x2797690);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x200);
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
    }
}
