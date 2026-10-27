using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ScreenTests
{
    [Theory]
    [InlineData(0, 45, 1)]
    [InlineData(1, 65, 1)]
    [InlineData(2, 85, 1)]
    [InlineData(2, 105, 2)]
    [InlineData(3, 105, 1)]
    public void Boot_delays_copy_current_director_Z_and_preserve_native_case2_fallthrough(short selector, ushort clock, byte phase)
    {
        var fixture = Fixture(selector);
        NativeBattleSiren95Frame frame = Put(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x24, clock));
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95!.Write(director + 4, BitConverter.GetBytes((ushort)4567)) } };
        var step = NativeBattleSiren95Screens.Advance745AD0(frame, fixture.Address, new((_, _, _) => throw new InvalidOperationException()));
        Assert.Equal(phase, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)128, Read(step.Frame, fixture.Address).ReadWord(0x1C));
        Assert.Equal((ushort)0xFB00, Read(step.Frame, fixture.Address).ReadWord(0x1E));
        Assert.Equal((ushort)4567, Read(step.Frame, fixture.Address).ReadWord(0x20));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Row_initializer_clears_exact_D4_preserves_grid_padding_and_uses_two_random_draws()
    {
        var fixture = Fixture(2);
        const uint row = 0x2585C90;
        NativeBattleSiren95State state = fixture.Frame.Memory.Siren95!.Write(row - 1, [0xAB]).Write(row, Enumerable.Repeat((byte)255, 0xD4).ToArray()).Write(row + 0xD4, [0xCD]).Write(0x258EE3E, [0x12, 0x34]);
        NativeBattleSiren95Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = state } };
        NativeCrtRandomState random = frame.Random.Draw(out ushort first).Draw(out ushort second);
        NativeBattleSiren95Frame result = NativeBattleSiren95Screens.Initialize745BF0(frame, row, 2);
        state = result.Memory.Siren95!;
        Assert.Equal(random, result.Random);
        Assert.Equal(new byte[] { 0xAB }, state.Read(row - 1, 1));
        Assert.Equal(new byte[] { 0xCD }, state.Read(row + 0xD4, 1));
        Assert.Equal(0x0C000000u, state.ReadDword(row));
        Assert.Equal(0x3E808080u, state.ReadDword(row + 4));
        Assert.Equal(0x00808080u, state.ReadDword(row + 0x10));
        Assert.Equal(0x3E548000u, state.ReadDword(row + 0x60));
        Assert.Equal(0x0000FF7Fu, state.ReadDword(row + 0xBC));
        Assert.Equal((ushort)(first & 4095), state.ReadWord(row + 0xCE));
        Assert.Equal((ushort)(second & 4095), state.ReadWord(row + 0xD0));
        Assert.Equal((ushort)3968, state.ReadWord(row + 0x3E));
        Assert.Equal(new byte[] { 0x12, 0x34 }, state.Read(0x258EE3E, 2));
        Assert.Equal((short)288, BinaryPrimitives.ReadInt16LittleEndian(state.Read(0x258EE38, 2)));
        Assert.Equal((short)-10240, BinaryPrimitives.ReadInt16LittleEndian(state.Read(0x258EF7C, 2)));
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleSiren95Screens.Initialize745BF0(restored, row, 2)));
    }

    [Fact]
    public void Zero_red_fade_returns_before_director_read_and_underflow_clears_RGB_without_touching_code()
    {
        var fixture = Fixture(0);
        const uint row = 0x2585AE8;
        var frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = fixture.Frame.Memory.Siren95! with { DirectorAddress1533010 = null } } };
        Assert.True(NativeBattleSiren95Screens.Fade746870(frame, row).Complete);
        NativeBattleSiren95State state = fixture.Frame.Memory.Siren95!.Write(row + 0x48, [1, 2, 3, 0x3E]).Write(row + 0x4C, [1, 2, 3, 0x3E]);
        frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = state } };
        var result = NativeBattleSiren95Screens.Fade746870(frame, row);
        Assert.False(result.Complete);
        Assert.Equal(new byte[] { 0, 0, 0, 0x3E }, result.Frame.Memory.Siren95!.Read(row + 0x48, 4));
        Assert.Equal(new byte[] { 0, 0, 0, 0x3E }, result.Frame.Memory.Siren95!.Read(row + 0x4C, 4));
        Assert.True(NativeBattleSiren95Screens.Fade746870(result.Frame, row).Complete);
    }

    private static (NativeBattleSiren95Frame Frame, uint Address) Fixture(short selector)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x745AD0, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        return (Put(appended.Frame, appended.Address, Read(appended.Frame, appended.Address).WriteWord(0xDE, unchecked((ushort)selector))), appended.Address);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
