using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95TargetParticleTests
{
    [Fact]
    public void Initial_phase_advances_without_target_or_director_reads()
    {
        var fixture = Fixture();
        NativeBattleSiren95Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = fixture.Frame.Memory.Siren95! with { DirectorAddress1533010 = null } } };
        var step = NativeBattleSiren95TargetParticles.Advance743250(frame, fixture.Address, fixture.Packets, _ => throw new InvalidOperationException());
        Assert.Equal((byte)1, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)1, Read(step.Frame, fixture.Address).ReadWord(0x24));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.False(step.Complete);
    }

    [Fact]
    public void Final_emitter_tick_spawns_before_stage7_gate_and_preserves_all_twelve_random_operands_and_corner_padding()
    {
        var fixture = Fixture();
        NativeBattleSiren95Frame frame = Put(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1));
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Random = new(1), Memory = frame.Memory with { Siren95 = frame.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes((short)7)) } };
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 12; index++)
        {
            random = random.Draw(out _);
        }
        NativeBattleGraphicsModel target = new(3, 0)
        {
            GeometryBounds52 = new(new(-100, 50, -200), new(100, -50, 200)),
            Transform = new(new(0, 0, 0), new(-32000, 32000, -1000), 0, 0, 4096)
        };
        NativeBattleGraphicsModel ReadTarget(byte actor)
        {
            Assert.Equal((byte)3, actor);
            return target;
        }
        NativeBattleSiren95Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var step = NativeBattleSiren95TargetParticles.Advance743250(frame, fixture.Address, fixture.Packets, ReadTarget);
        Assert.Equal(random, step.Frame.Random);
        NativeBattleModulePoolTask child = Assert.Single(step.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x7433C0 && (task.Flags & 1) != 0);
        Assert.Equal(unchecked((ushort)-32098), child.ReadWord(0x6C));
        Assert.Equal((ushort)32000, child.ReadWord(0x6E));
        Assert.Equal(unchecked((ushort)-982), child.ReadWord(0x70));
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.Equal((ushort)0, child.ReadWord(0x72 + corner * 8));
            Assert.Equal((ushort)0, child.ReadWord(0x8C + corner * 8));
            Assert.Equal((ushort)0, child.ReadWord(0x8E + corner * 8));
            Assert.Equal((ushort)0, child.ReadWord(0x90 + corner * 8));
        }
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)1, (byte)(Read(step.Frame, fixture.Address).ReadByte(0x26) & 1));
        Assert.False(step.Complete);
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleSiren95TargetParticles.Advance743250(restored, fixture.Address, fixture.Packets, ReadTarget)));
    }

    [Fact]
    public void Target_particle_random_sprite_initialization_has_one_draw_and_ignores_contact_stage_gate()
    {
        var fixture = Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x7433C0, 0xE0, fixture.Address, fixture.Packets);
        uint director = appended.Frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        NativeBattleSiren95Frame frame = appended.Frame with { Random = new(1), Memory = appended.Frame.Memory with { Siren95 = appended.Frame.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes((short)7)) } };
        NativeCrtRandomState random = frame.Random.Draw(out ushort sprite);
        int draws = 0;
        var step = NativeBattleSiren95Contacts.AdvanceParticle(frame, appended.Address, (current, pointer, scale) =>
        {
            NativeBattleModulePoolTask child = Read(current, pointer);
            Assert.Equal((short)4096, scale);
            Assert.Equal(sprite % 3 == 0 ? 0x15331D0u : sprite % 3 == 1 ? 0x1533328u : 0x15334A0u, child.ReadDword(0x4C));
            Assert.Equal((byte)0, (byte)(child.ReadByte(0x26) & 4));
            draws++;
            return current;
        });
        Assert.Equal(4, draws);
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal((ushort)10, Read(step.Frame, appended.Address).ReadWord(0x52));
    }

    private static (NativeBattleSiren95Frame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x743250, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        return (appended.Frame, appended.Address, fixture.Inputs.Packets);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
