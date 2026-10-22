using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47ParticleStageTests
{
    [Fact]
    public void Actual176_byte_callback_clears_RGB_once_rises_in_three_steps_and_waits_for_mode3()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Advance(fixture.Frame).Frame;
        Assert.Equal(new NativeBattleStageFadeWrite(0, true), Assert.Single(fixture.Writes));
        Assert.Equal((ushort)1, fixture.Task(frame).ReadWord(0x24));
        for (int update = 1; update <= 3; update++)
        {
            frame = fixture.Advance(frame).Frame;
            Assert.Equal(new NativeBattleStageFadeWrite(checked((short)(update * 512)), false), fixture.Writes[^1]);
        }
        Assert.Equal((byte)2, fixture.Task(frame).ReadByte(0x29));
        int writes = fixture.Writes.Count;
        frame = fixture.Advance(frame with { State = frame.State.WithSharedWord(0x40, 2) }).Frame;
        Assert.Equal((byte)2, fixture.Task(frame).ReadByte(0x29));
        frame = fixture.Advance(frame with { State = frame.State.WithSharedWord(0x40, 3) }).Frame;
        Assert.Equal((byte)3, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal(writes, fixture.Writes.Count);
        Assert.Equal((ushort)0xA55A, fixture.Task(frame).ReadWord(0x20));
    }

    [Fact]
    public void Falling_phase_publishes_six_depths_then_releases_actual_parent_once()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 3).WriteWord(0x1C, 1536));
        for (int update = 1; update <= 6; update++)
        {
            NativeBattleCurse47Step step = fixture.Advance(frame);
            frame = step.Frame;
            Assert.Equal(new NativeBattleStageFadeWrite(checked((short)(1536 - update * 256)), false), fixture.Writes[^1]);
            Assert.Equal(update == 6 ? 2 : 0, step.Result);
        }
        Assert.Equal((byte)0, NativeBattleCurse47Controller.Read(frame, fixture.Parent).ReadByte(0x28));
        Assert.Equal((byte)4, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal((ushort)6, fixture.Task(frame).ReadWord(0x24));
    }

    [Theory]
    [InlineData(1, 0x7FF0, unchecked((short)0x81F0), 1)]
    [InlineData(3, 0x8000, 0x7F00, 3)]
    [InlineData(3, 0xFFFF, 0, 4)]
    public void Depth_operations_wrap_WORD_before_signed_comparison(byte phase, ushort prior, short expected, byte nextPhase)
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, phase).WriteWord(0x1C, prior));
        NativeBattleCurse47Step step = fixture.Advance(frame);
        Assert.Equal(expected, Assert.Single(fixture.Writes).Fade);
        Assert.Equal(nextPhase, fixture.Task(step.Frame).ReadByte(0x29));
    }

    [Fact]
    public void Terminal_child_wait_and_JSON_next_do_not_repeat_stage_store_or_consume_random()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame before = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 3).WriteWord(0x1C, 128).WriteByte(0x28, 1));
        NativeBattleCurse47Frame restored = JsonSerializer.Deserialize<NativeBattleCurse47Frame>(JsonSerializer.Serialize(before))!;
        NativeBattleCurse47Step first = fixture.Advance(before);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(fixture.Advance(restored)));
        Assert.Equal(0, first.Result);
        Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(first.Frame.Random));
        int writes = fixture.Writes.Count;
        NativeBattleCurse47Frame ready = fixture.Put(first.Frame, fixture.Task(first.Frame).WriteByte(0x28, 0));
        Assert.Equal(2, fixture.Advance(ready).Result);
        Assert.Equal(writes, fixture.Writes.Count);
    }

    [Fact]
    public void Invalid_signed_phase_fails_before_external_stage_publication()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame invalid = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 255));
        Assert.Throws<InvalidDataException>(() => fixture.Advance(invalid));
        Assert.Empty(fixture.Writes);
    }

    private sealed class Fixture
    {
        public NativeBattleCurse47Frame Frame { get; }
        public uint Parent { get; }
        public uint Address { get; }
        public List<NativeBattleStageFadeWrite> Writes { get; } = [];

        public Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleCurse47Controller.RootCallback, 100);
            Parent = root.Address;
            NativeBattleEffectHierarchyAppend child = root.Hierarchy.Append8DC540(5, NativeBattleCurse47ParticleStage.Callback, 176, Parent, (_, _, _) => new(0, 3));
            Address = child.Address;
            hierarchy = child.Hierarchy.Write(Address, child.Hierarchy.Read(Address).WriteWord(0x20, 0xA55A));
            NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), null, null) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(71), NativeBattleCurse47State.FromColdProcess(new byte[80].ToImmutableArray()));
        }

        public NativeBattleModulePoolTask Task(NativeBattleCurse47Frame frame) => NativeBattleCurse47Controller.Read(frame, Address);
        public NativeBattleCurse47Frame Put(NativeBattleCurse47Frame frame, NativeBattleModulePoolTask task) => NativeBattleCurse47Controller.Write(frame, Address, task);
        public NativeBattleCurse47Step Advance(NativeBattleCurse47Frame frame) => NativeBattleCurse47ParticleStage.Advance(frame, Address, (current, operation) =>
        {
            Writes.Add(Assert.IsType<NativeBattleCurse47StageDepthWrite>(operation).Write);
            return current;
        });
    }
}
