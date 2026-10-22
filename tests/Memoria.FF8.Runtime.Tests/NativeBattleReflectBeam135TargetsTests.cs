using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReflectBeam135TargetsTests
{
    [Theory]
    [InlineData(0, 2, 0, 25)]
    [InlineData(1, 0, 2, 26)]
    public void Target_remaining_zero_retires_before_old25_contact_and_nonzero_contacts_all_matching_rows(int remaining, int result, int contacts, int next)
    {
        NativeBattleReflectBeam135ControllerTests.Fixture f = new() { Remaining = remaining };
        (NativeBattleReflectBeam135Frame frame, uint address) = Target(f, 25);
        var actual = NativeBattleReflectBeam135Controller.AdvanceTarget(frame, address, f.Resources, f.Inputs);
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)next, NativeBattleReflectBeam135Controller.Read(actual.Frame, address).ReadWord(12));
        Assert.Equal(contacts, f.Operations.OfType<NativeBattleReflectBeam135Contact>().Count());
        if (contacts != 0)
        {
            Assert.Equal(new int[] { 0, 2 }, f.Operations.OfType<NativeBattleReflectBeam135Contact>().Select(value => value.Index).ToArray());
        }
        var context = Assert.Single(f.Acts);
        Assert.Equal((TargetActAddress, 0x1C, 0x310), (context.Source, context.Offset, context.Size));
        Assert.Throws<NotSupportedException>(() => context.Context.Read(6, 2));
        Assert.Equal(1u, actual.Frame.State.ReadDword(CursorGlobal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Target_early_append_precedes_three_CRT_draws_and_full_bank_consumes_no_random(bool full)
    {
        NativeBattleReflectBeam135ControllerTests.Fixture f = new();
        (NativeBattleReflectBeam135Frame frame, uint address) = Target(f, 0);
        if (full)
        {
            for (int index = 0; index < ParticleCount; index++)
            {
                (frame, _) = NativeBattleReflectBeam135Controller.Append(frame, 4, ParticleACallback);
            }
        }
        NativeCrtRandomState expected = frame.Random;
        if (!full)
        {
            for (int index = 0; index < 3; index++)
            {
                expected = expected.Draw(out _);
            }
        }
        var actual = NativeBattleReflectBeam135Controller.AdvanceTarget(frame, address, f.Resources, f.Inputs);
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Empty(f.Acts);
        Assert.Equal((ushort)1, NativeBattleReflectBeam135Controller.Read(actual.Frame, address).ReadWord(12));
        if (!full)
        {
            NativeBattleModulePoolTask child = Assert.Single(actual.Frame.Effect.Pools[4].Slots, task => (task.Flags & 1) != 0);
            Assert.Equal(ParticleBCallback, child.Callback);
            Assert.Equal((ushort)0, child.ReadWord(18));
        }
    }

    [Fact]
    public void Target_sixteen_creates_one_large_and_four_moving_particles_without_requiring_source_W()
    {
        NativeBattleReflectBeam135ControllerTests.Fixture f = new();
        (NativeBattleReflectBeam135Frame frame, uint address) = Target(f, 16);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 15; index++)
        {
            expected = expected.Draw(out _);
        }
        var actual = NativeBattleReflectBeam135Controller.AdvanceTarget(frame, address, f.Resources, f.Inputs);
        Assert.Equal(expected, actual.Frame.Random);
        NativeBattleModulePoolTask[] particles = actual.Frame.Effect.Pools[4].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
        Assert.Equal(5, particles.Length);
        Assert.Equal(ParticleACallback, particles[0].Callback);
        Assert.All(particles.Skip(1), child =>
        {
            Assert.Equal(ParticleCCallback, child.Callback);
            Assert.Equal((ushort)10, child.ReadWord(12));
            Assert.Equal((ushort)20, child.ReadWord(14));
            Assert.Equal((ushort)30, child.ReadWord(16));
            Assert.Equal((ushort)0, child.ReadWord(18));
        });
        Assert.Throws<NotSupportedException>(() => NativeBattleReflectBeam135Controller.Read(actual.Frame, address).ReadWord(26));
    }

    [Theory]
    [InlineData(ParticleACallback, 15, SpriteCAddress)]
    [InlineData(ParticleBCallback, 6, SpriteAAddress)]
    [InlineData(ParticleCCallback, 7, SpriteBAddress)]
    public void Particle_draws_actual180_before_motion_and_retires_from_signed_phase_without_feedback(uint callback, int phase, uint source)
    {
        NativeBattleReflectBeam135ControllerTests.Fixture f = new();
        (NativeBattleReflectBeam135Frame frame, uint address) = NativeBattleReflectBeam135Controller.Append(f.Frame, 4, callback);
        NativeBattleModulePoolTask task = NativeBattleReflectBeam135Controller.Read(frame, address).WriteWord(12, 10).WriteWord(14, 20).WriteWord(16, 30).WriteWord(18, (ushort)phase).WriteWord(20, unchecked((ushort)-16)).WriteWord(22, 8).WriteWord(24, 0).WriteWord(28, 1).WriteWord(30, unchecked((ushort)-1)).WriteWord(32, 0);
        frame = NativeBattleReflectBeam135Controller.Write(frame, address, task);
        var actual = NativeBattleReflectBeam135Controller.AdvanceParticle(frame, address, f.Resources, f.Inputs);
        Assert.Equal(2, actual.Result);
        NativeBattleReflectBeam135DrawSprite180 draw = Assert.Single(f.Operations.OfType<NativeBattleReflectBeam135DrawSprite180>());
        Assert.Equal(source, draw.SourceAddress);
        Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Staging!.Read(draw.TemporaryAddress + 4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Staging.Read(draw.TemporaryAddress + 36, 2)));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Staging.Read(draw.TemporaryAddress + 40, 2));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        task = NativeBattleReflectBeam135Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)(phase + 1), task.ReadWord(18));
        Assert.Equal(frame.Random, actual.Frame.Random);
        if (callback == ParticleCCallback)
        {
            Assert.Equal(unchecked((ushort)-15), task.ReadWord(20));
            Assert.Equal(unchecked((ushort)-5), task.ReadWord(12));
            Assert.Equal((ushort)27, task.ReadWord(14));
        }
        else
        {
            Assert.Equal((ushort)10, task.ReadWord(12));
            Assert.Equal((ushort)20, task.ReadWord(14));
        }
    }

    private static (NativeBattleReflectBeam135Frame Frame, uint Address) Target(NativeBattleReflectBeam135ControllerTests.Fixture f, ushort age)
    {
        (NativeBattleReflectBeam135Frame frame, uint address) = NativeBattleReflectBeam135Controller.Append(f.Frame, 3, TargetCallback);
        NativeBattleModulePoolTask task = NativeBattleReflectBeam135Controller.Read(frame, address).WriteBytes(12, new byte[TargetStride - 12]).WriteWord(12, age).WriteDword(16, NativeBattleReflectBeam135ControllerTests.Fixture.ModelAddress(3)).WriteWord(20, 10).WriteWord(22, 20).WriteWord(24, 30).MarkPayloadUnknown(26, 2);
        return (NativeBattleReflectBeam135Controller.Write(frame, address, task), address);
    }
}
