using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ParticleTests
{
    [Fact]
    public void Dust_parent_emits_at_even_age40_before_completion_with_24_CRT_draws_and_retained_packed_W()
    {
        Fixture fixture = new(0x8CD4B0);
        NativeBattleDoom8Frame frame = fixture.Write(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x24, 40).WriteWord(0x1C, 100).WriteWord(0x20, 200).WriteWord(0x22, 0xBEEF));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 24; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleDoom8Step result = fixture.Advance(frame);
        Assert.Equal(expected, result.Frame.Random);
        Assert.Equal(0, result.Result);
        Assert.Equal((byte)1, fixture.Task(result.Frame).ReadByte(0x28));
        Assert.Equal((byte)2, fixture.Task(result.Frame).ReadByte(0x29));
        NativeBattleModulePoolTask child = Assert.Single(result.Frame.Effect.Pools[5].Slots, task => task.Callback == 0x8CD670);
        for (int vector = 0; vector < 4; vector++)
        {
            Assert.Equal((ushort)0xBEEF, child.ReadWord(0x72 + vector * 8));
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.InRange(unchecked((short)child.ReadWord(0x8C + vector * 8 + axis * 2)), (short)-512, (short)511);
            }
        }
    }

    [Fact]
    public void Dust_motion_uses_new_signed_damped_velocity_and_publishes_each_of_four_draws_sequentially()
    {
        Fixture fixture = new(0x8CD670);
        NativeBattleModulePoolTask task = fixture.Task(fixture.Frame);
        for (int vector = 0; vector < 4; vector++)
        {
            task = task.WriteWord(0x6C + vector * 8, (ushort)(100 * vector)).WriteWord(0x8C + vector * 8, unchecked((ushort)-17));
            task = task.WriteWord(0x8E + vector * 8, unchecked((ushort)-15)).WriteWord(0x90 + vector * 8, 17).MarkPayloadUnknown(0x72 + vector * 8, 2);
        }
        List<short> positions = [];
        fixture.Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattleDoom8DrawDust>(operation);
            positions.Add(unchecked((short)fixture.Task(frame).ReadWord(0x1C)));
            Assert.Equal((uint)(positions.Count - 1), frame.State.ReadDword(0x275F9D4));
            return frame with { State = frame.State.WriteDword(0x275F9D4, (uint)positions.Count) };
        };
        NativeBattleDoom8Step result = fixture.Advance(fixture.Write(fixture.Frame, task));
        Assert.Equal(new short[] { -1, 99, 199, 299 }, positions);
        Assert.Equal(4u, result.Frame.State.ReadDword(0x275F9D4));
        Assert.Equal((ushort)299, fixture.Task(result.Frame).ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-16), fixture.Task(result.Frame).ReadWord(0x8C));
        Assert.Equal(unchecked((ushort)-15), fixture.Task(result.Frame).ReadWord(0x8E));
        Assert.Equal((ushort)16, fixture.Task(result.Frame).ReadWord(0x90));
        Assert.Throws<NotSupportedException>(() => fixture.Task(result.Frame).ReadWord(0x22));
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Dust_frame17_clamps_and_hides_before_four_attempts_but_still_moves_and_finishes()
    {
        Fixture fixture = new(0x8CD670);
        NativeBattleDoom8Frame frame = fixture.Write(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x50, 16).WriteWord(0x52, 16).WriteWord(0x8C, 256));
        int draws = 0;
        fixture.Execute = (current, _) =>
        {
            draws++;
            Assert.Equal((byte)5, fixture.Task(current).ReadByte(0x26));
            Assert.Equal((ushort)16, fixture.Task(current).ReadWord(0x50));
            return current;
        };
        NativeBattleDoom8Step result = fixture.Advance(frame);
        Assert.Equal(4, draws);
        Assert.Equal((ushort)15, fixture.Task(result.Frame).ReadWord(0x6C));
        Assert.Equal(2, result.Result);
    }

    [Fact]
    public void Smoke_init_uses_four_CRT_draws_and_advances_Y_angle_before_first_draw()
    {
        Fixture fixture = new(0x8CDFB0);
        NativeCrtRandomState expected = fixture.Frame.Random.Draw(out ushort scale);
        expected = expected.Draw(out ushort angle);
        expected = expected.Draw(out ushort x);
        expected = expected.Draw(out ushort z);
        fixture.Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattleDoom8DrawSmoke>(operation);
            NativeBattleModulePoolTask task = fixture.Task(frame);
            Assert.Equal((uint)(scale % 1023 + 3072), task.ReadDword(0x30));
            Assert.Equal(task.ReadDword(0x30), task.ReadDword(0x34));
            Assert.Equal(task.ReadDword(0x30), task.ReadDword(0x38));
            Assert.Equal((ushort)(((angle & 4095) + 128) & 4095), task.ReadWord(0x46));
            Assert.Equal(unchecked((ushort)(x % 768 - 384)), task.ReadWord(0x44));
            Assert.Equal(unchecked((ushort)(z % 768 - 384)), task.ReadWord(0x48));
            return frame;
        };
        Assert.Equal(expected, fixture.Advance(fixture.Frame).Frame.Random);
    }

    [Fact]
    public void Smoke_fades_four_frames_waits_one_and_fades_four_including_terminal_draw()
    {
        Fixture fixture = new(0x8CDFB0);
        List<ushort> fades = [];
        fixture.Execute = (frame, _) =>
        {
            fades.Add(fixture.Task(frame).ReadWord(0x50));
            return frame;
        };
        NativeBattleDoom8Frame current = fixture.Frame;
        NativeCrtRandomState afterBirth = current.Random;
        for (int index = 0; index < 10; index++)
        {
            NativeBattleDoom8Step result = fixture.Advance(current);
            current = result.Frame;
            if (index == 0)
            {
                afterBirth = current.Random;
            }
            Assert.Equal(index == 9 ? 2 : 0, result.Result);
        }
        Assert.Equal(new ushort[] { 4096, 3072, 2048, 1024, 0, 0, 1024, 2048, 3072, 4096 }, fades);
        Assert.Equal(afterBirth, current.Random);
        Assert.Equal((byte)1, fixture.Task(current).ReadByte(0x26));
        Assert.Equal((ushort)10, fixture.Task(current).ReadWord(0x24));
    }

    [Theory]
    [InlineData(0x8CD4B0u, 0)]
    [InlineData(0x8CDED0u, 256)]
    public void Parent_vertical_policy_differs_and_odd_age_never_consumes_CRT(uint callback, ushort expectedY)
    {
        Fixture fixture = new(callback);
        NativeBattleDoom8Frame frame = fixture.Write(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x24, 3));
        NativeBattleDoom8Step result = fixture.Advance(frame);
        Assert.Equal(expectedY, fixture.Task(result.Frame).ReadWord(0x1E));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal((byte)0, fixture.Task(result.Frame).ReadByte(0x28));
    }

    private sealed class Fixture
    {
        public NativeBattleDoom8Frame Frame { get; }
        public uint Address { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute { get; set; } = (frame, _) => frame;
        public Fixture(uint callback)
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            Packets = prior.Inputs.Packets;
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleDoom8Controller.RootCallback, 100);
            hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), prior.Frame.Effect.Parameters!, Packets));
            int pool = callback is 0x8CD4B0 or 0x8CD670 ? 5 : 6;
            NativeBattleEffectHierarchyAppend particle = hierarchy.Append8DC540(pool, callback, NativeBattleDoom8State.Pools[pool].Stride, root.Address, (_, _, _) => new(0, 3));
            Address = particle.Address;
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), prior.Frame.Effect.Parameters, NativeBattleDoom8State.Pools[0].Descriptor) { Pools = particle.Hierarchy.Pools };
            Frame = new(prior.Frame.Memory, effect, new(123), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()));
        }
        public NativeBattleModulePoolTask Task(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, Address);
        public NativeBattleDoom8Frame Write(NativeBattleDoom8Frame frame, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, Address, task);
        public NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame) => NativeBattleDoom8Particles.Advance(frame, Address, Packets, Execute);
    }
}
