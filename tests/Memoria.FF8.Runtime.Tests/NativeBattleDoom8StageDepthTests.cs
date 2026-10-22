using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8StageDepthTests
{
    [Fact]
    public void Rising_depth_clears_RGB_once_and_clamps_on_the_tenth_update_with_actual_parent_release()
    {
        Fixture fixture = new(0x8CC1E0);
        NativeBattleDoom8Frame frame = fixture.Frame;
        NativeBattleDoom8Step initialized = fixture.Advance(frame);
        frame = initialized.Frame;
        Assert.Equal(new NativeBattleStageFadeWrite(0, true), Assert.Single(fixture.Writes));
        Assert.Equal((byte)1, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal((ushort)1, fixture.Task(frame).ReadWord(0x24));
        for (int update = 1; update <= 10; update++)
        {
            NativeBattleDoom8Step next = fixture.Advance(frame);
            frame = next.Frame;
            Assert.Equal((short)Math.Min(update * 160, 1536), fixture.Writes[^1].Fade);
            Assert.False(fixture.Writes[^1].ClearRgb);
            Assert.Equal(update == 10 ? 2 : 0, next.Result);
        }
        Assert.Equal((ushort)11, fixture.Task(frame).ReadWord(0x24));
        Assert.Equal((byte)0, NativeBattleDoom8Controller.Read(frame, fixture.Parent).ReadByte(0x28));
        Assert.Equal((ushort)0xA5A5, fixture.Task(frame).ReadWord(0x20));
    }

    [Fact]
    public void Falling_depth_waits_for_signed_age_above_45_then_uses_six_updates_without_clearing_RGB()
    {
        Fixture fixture = new(0x8CC2D0);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, fixture.Task(fixture.Frame).WriteWord(0x24, 45));
        frame = fixture.Advance(frame).Frame;
        Assert.Equal((byte)0, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal((ushort)46, fixture.Task(frame).ReadWord(0x24));
        Assert.Empty(fixture.Writes);
        frame = fixture.Advance(frame).Frame;
        Assert.Equal((byte)1, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal((ushort)1536, fixture.Task(frame).ReadWord(0x1C));
        Assert.Empty(fixture.Writes);
        for (int update = 1; update <= 6; update++)
        {
            NativeBattleDoom8Step next = fixture.Advance(frame);
            frame = next.Frame;
            Assert.Equal((short)(1536 - update * 256), fixture.Writes[^1].Fade);
            Assert.False(fixture.Writes[^1].ClearRgb);
            Assert.Equal(update == 6 ? 2 : 0, next.Result);
        }
        Assert.Equal((ushort)53, fixture.Task(frame).ReadWord(0x24));
    }

    [Fact]
    public void Wrapped_depth_and_age_remain_signed_WORD_consumers()
    {
        Fixture fixture = new(0x8CC1E0);
        NativeBattleModulePoolTask task = fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x1C, 0x7FF0);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, task);
        NativeBattleDoom8Step step = fixture.Advance(frame);
        Assert.Equal(unchecked((short)0x8090), Assert.Single(fixture.Writes).Fade);
        Assert.Equal((byte)1, fixture.Task(step.Frame).ReadByte(0x29));
        Assert.Equal(0, step.Result);
        Fixture falling = new(0x8CC2D0);
        frame = NativeBattleDoom8Controller.Write(falling.Frame, falling.Address, falling.Task(falling.Frame).WriteWord(0x24, 0xFFFF));
        frame = falling.Advance(frame).Frame;
        Assert.Equal((ushort)0, falling.Task(frame).ReadWord(0x24));
        Assert.Equal((byte)0, falling.Task(frame).ReadByte(0x29));
        Assert.Empty(falling.Writes);
    }

    [Fact]
    public void Restored_terminal_step_preserves_child_wait_and_next_callback_without_repeating_stage_writes()
    {
        Fixture fixture = new(0x8CC1E0);
        NativeBattleModulePoolTask task = fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x1C, 1440).WriteByte(0x28, 1);
        NativeBattleDoom8Frame before = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, task);
        NativeBattleDoom8Frame restored = JsonSerializer.Deserialize<NativeBattleDoom8Frame>(JsonSerializer.Serialize(before))!;
        NativeBattleDoom8Step first = fixture.Advance(before);
        NativeBattleDoom8Step second = fixture.Advance(restored);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(0, first.Result);
        Assert.Equal((byte)2, fixture.Task(first.Frame).ReadByte(0x29));
        int writes = fixture.Writes.Count;
        NativeBattleDoom8Frame ready = NativeBattleDoom8Controller.Write(first.Frame, fixture.Address, fixture.Task(first.Frame).WriteByte(0x28, 0));
        Assert.Equal(2, fixture.Advance(ready).Result);
        Assert.Equal(writes, fixture.Writes.Count);
    }

    [Fact]
    public void Invalid_phase_or_callback_fails_before_a_stage_write()
    {
        Fixture fixture = new(0x8CC1E0);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, fixture.Task(fixture.Frame).WriteByte(0x29, 255));
        Assert.Throws<InvalidDataException>(() => fixture.Advance(frame));
        frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, fixture.Task(fixture.Frame) with { Callback = 0x8CC3A0 });
        Assert.Throws<InvalidDataException>(() => fixture.Advance(frame));
        Assert.Empty(fixture.Writes);
    }

    private sealed class Fixture
    {
        public NativeBattleDoom8Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public List<NativeBattleStageFadeWrite> Writes { get; } = [];

        public Fixture(uint callback)
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8C4B30, 100);
            Parent = root.Address;
            NativeBattleEffectHierarchyAppend child = root.Hierarchy.Append8DC540(5, callback, 176, Parent, (_, _, _) => new(0, 4));
            Address = child.Address;
            hierarchy = child.Hierarchy.Write(Address, child.Hierarchy.Read(Address).WriteWord(0x20, 0xA5A5));
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()));
        }

        public NativeBattleModulePoolTask Task(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, Address);

        public NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame)
        {
            return NativeBattleDoom8StageDepth.Advance(frame, Address, (current, operation) =>
            {
                Writes.Add(Assert.IsType<NativeBattleDoom8StageDepthWrite>(operation).Write);
                return current;
            });
        }
    }
}
