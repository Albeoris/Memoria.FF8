using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95WaveTests
{
    [Fact]
    public void Spawner_copies_then_increments_ordinal_and_draws_one_delay_random()
    {
        var fixture = Fixture(0x745250);
        NativeBattleSiren95Frame frame = Put(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x60, 7));
        NativeCrtRandomState random = frame.Random.Draw(out ushort delay);
        var step = NativeBattleSiren95Waves.Advance(frame, fixture.Address, new(fixture.Packets, (_, _) => throw new InvalidOperationException()));
        var child = Assert.Single(step.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x7452F0);
        Assert.Equal((ushort)7, child.ReadWord(0x60));
        Assert.Equal((ushort)8, Read(step.Frame, fixture.Address).ReadWord(0x60));
        Assert.Equal((ushort)((delay & 3) + 4), Read(step.Frame, fixture.Address).ReadWord(0x5C));
        Assert.Equal((byte)1, Read(step.Frame, fixture.Address).ReadByte(0x28));
        Assert.Equal(random, step.Frame.Random);
    }

    [Fact]
    public void Child_initialization_consumes_eight_randoms_uses_camera_control_focal_and_keeps_screen_matrix_independent()
    {
        var fixture = Fixture(0x7452F0);
        NativeBattleSiren95State state = fixture.Frame.Memory.Siren95!.WriteDword(0x258EAA0, 0x258FB88).WriteDword(0x258FCCC, 0x19D2018).Write(0x258E890, BitConverter.GetBytes((ushort)300));
        NativeBattleSiren95Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = state } };
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 8; index++)
        {
            random = random.Draw(out _);
        }
        int draws = 0;
        NativeBattleSiren95Frame Draw(NativeBattleSiren95Frame current, uint address)
        {
            var task = Read(current, address);
            Assert.Equal(0x19D2018u, task.ReadDword(0x4C));
            Assert.Equal((ushort)600, task.ReadWord(0x20));
            Assert.Equal((ushort)4096, task.ReadWord(0x50));
            Assert.Equal((ushort)0xFE00, task.ReadWord(0x52));
            Assert.InRange(task.ReadDword(0x30), 1024u, 5118u);
            Assert.Equal(task.ReadDword(0x30), task.ReadDword(0x34));
            draws++;
            return current;
        }
        string replay = NativeBattleTestSnapshots.Json(frame);
        var step = NativeBattleSiren95Waves.Advance(frame, fixture.Address, new(fixture.Packets, Draw));
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(1, draws);
        NativeBattleSiren95Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(replay)!;
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleSiren95Waves.Advance(restored, fixture.Address, new(fixture.Packets, Draw))));
        var direct = Read(frame, fixture.Address).WriteWord(0x44, 0).WriteWord(0x46, 0).WriteWord(0x48, 0).WriteWord(0x1C, 123).WriteWord(0x1E, unchecked((ushort)-456)).WriteWord(0x20, 789).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
        NativeBattleActionMatrix matrix = NativeBattleSiren95Waves.Matrix745380(direct);
        Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation.ToArray(), matrix.Rotation.ToArray());
        Assert.Equal((123, -456, 789), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void Fade_out_finishes_at_signed_4096_but_still_draws_final_tick()
    {
        var fixture = Fixture(0x7452F0);
        var task = Read(fixture.Frame, fixture.Address).WriteByte(0x29, 3).WriteWord(0x50, 3584).WriteWord(0x54, unchecked((ushort)-3)).WriteWord(0x1C, 2).WriteWord(0x48, 4090).WriteWord(0x5E, 20);
        var frame = Put(fixture.Frame, fixture.Address, task);
        int draws = 0;
        var step = NativeBattleSiren95Waves.Advance(frame, fixture.Address, new(fixture.Packets, (current, address) =>
        {
            Assert.Equal((ushort)4096, Read(current, address).ReadWord(0x50));
            Assert.Equal(unchecked((ushort)-1), Read(current, address).ReadWord(0x1C));
            Assert.Equal((ushort)14, Read(current, address).ReadWord(0x48));
            draws++;
            return current;
        }));
        Assert.True(step.Complete);
        Assert.Equal(1, draws);
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    private static (NativeBattleSiren95Frame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture(uint callback)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var child = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x2585EF0, callback, 0x64, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        return (child.Frame, child.Address, fixture.Inputs.Packets);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
