using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattlePetrifyStare127Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePetrifyStare127ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_selects_bank_from_old_parity_and_wraps_its_signed_cycle_before_next_callback(uint oldParity, int expectedOffset, uint nextParity)
    {
        Fixture fixture = new(10, 9);
        NativeBattlePetrifyStare127Frame frame = fixture.WithRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteDword(16, oldParity));
        fixture.Observe = (current, operation) =>
        {
            Assert.Equal(ArenaAddress + (uint)expectedOffset, current.State.ReadDword(CursorGlobal));
            Assert.Equal(0u, current.State.ReadDword(StageDepthGlobal));
            return current;
        };
        NativeBattlePetrifyStare127Frame result = fixture.Step(frame);
        Assert.Equal(nextParity, fixture.Root(result).ReadDword(16));
        Assert.Equal((ushort)0, fixture.Root(result).ReadWord(12));
        Assert.Equal((ushort)11, fixture.Target(result).ReadWord(12));
        Assert.IsType<NativeBattlePetrifyStare127StageClear>(fixture.Operations[^1]);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Contact_occurs_at_old_age42_after_both_ACTs_and_preserves_frame_returned_state()
    {
        Fixture fixture = new(42);
        fixture.Observe = (frame, operation) => operation is NativeBattlePetrifyStare127AdvanceAct ? frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(CursorGlobal) + 4) } : frame;
        NativeBattlePetrifyStare127Frame result = fixture.Step(fixture.Frame);
        Assert.Equal(new[] { typeof(NativeBattlePetrifyStare127AdvanceAct), typeof(NativeBattlePetrifyStare127AdvanceAct), typeof(NativeBattlePetrifyStare127Contact), typeof(NativeBattlePetrifyStare127StageClear) }, fixture.Operations.Select(operation => operation.GetType()));
        Assert.Equal(0, Assert.Single(fixture.Operations.OfType<NativeBattlePetrifyStare127Contact>()).Index);
        Assert.Equal((ushort)43, fixture.Target(result).ReadWord(12));
        Assert.Equal(ArenaAddress + SecondPacketOffset + 8, result.State.ReadDword(CursorGlobal));
    }

    [Fact]
    public void Last_age48_is_drawn_and_age49_draws_before_child_and_root_release()
    {
        Fixture fixture = new(48);
        NativeBattlePetrifyStare127Frame last = fixture.Step(fixture.Frame);
        Assert.Equal((ushort)49, fixture.Target(last).ReadWord(12));
        Assert.Equal(RootDescriptor, last.Effect.ReturnedDescriptor);
        fixture.Operations.Clear();
        NativeBattlePetrifyStare127Frame released = fixture.Step(last);
        Assert.Equal(2, fixture.Operations.OfType<NativeBattlePetrifyStare127AdvanceAct>().Count());
        Assert.Equal((ushort)49, fixture.Target(released).ReadWord(12));
        Assert.Equal(0u, released.Effect.ReturnedDescriptor);
        Assert.All(released.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
    }

    [Theory]
    [InlineData(9, 1)]
    [InlineData(10, 0)]
    public void First_anchor_refresh_stops_at_age10_and_preserves_padding_and_following_WORD(ushort age, int expectedReads)
    {
        Fixture fixture = new(age);
        NativeBattlePetrifyStare127Frame frame = NativeBattlePetrifyStare127Controller.Write(fixture.Frame, ArenaAddress, fixture.Target(fixture.Frame).WriteWord(0x16, 0xABCD));
        NativeBattlePetrifyStare127Frame result = fixture.Step(frame);
        Assert.Equal(expectedReads, fixture.BoneReads);
        Assert.Equal((ushort)0xABCD, fixture.Target(result).ReadWord(0x16));
        NativeBattlePetrifyStare127AdvanceAct act = Assert.Single(fixture.Operations.OfType<NativeBattlePetrifyStare127AdvanceAct>());
        Assert.Throws<NotSupportedException>(() => act.Context16.Read(6, 2));
        Assert.Equal(Working(result) + MorphOffset, BitConverter.ToUInt32(act.Context16.Read(12, 4)));
    }

    [Fact]
    public void Age14_caches_the_complete_recipient_anchor_and_passes_separate_signed_depths()
    {
        Fixture fixture = new(14);
        NativeBattlePetrifyStare127Frame result = fixture.Step(fixture.Frame);
        Assert.Equal(new[] { 3 }, fixture.AnchorActors);
        Assert.Equal(new ushort[] { 700, 800, 900, 0xFEDC }, Enumerable.Range(0, 4).Select(lane => fixture.Target(result).ReadWord(0x18 + lane * 2)));
        NativeBattlePetrifyStare127AdvanceAct[] acts = fixture.Operations.OfType<NativeBattlePetrifyStare127AdvanceAct>().ToArray();
        Assert.Equal(new byte[] { 0, 1 }, acts.Select(act => act.Act));
        Assert.Equal(-80, BitConverter.ToInt32(acts[0].Context16.Read(8, 4)));
        Assert.Equal(-96, BitConverter.ToInt32(acts[1].Context16.Read(8, 4)));
        Assert.Equal(unchecked((short)0xFEDC), BitConverter.ToInt16(acts[1].Context16.Read(6, 2)));
    }

    [Fact]
    public void Full_target_bank_skips_append_and_does_not_advance_the_header_ordinal()
    {
        Fixture fixture = new(40, 1);
        NativeBattlePetrifyStare127Frame result = fixture.Step(fixture.Frame);
        Assert.Equal((byte)0, fixture.Root(result).ReadByte(14));
        Assert.Equal((ushort)41, fixture.Target(result).ReadWord(12));
        Assert.Equal((ushort)2, fixture.Root(result).ReadWord(12));
        Assert.Single(result.Effect.Pools[1].Slots);
    }

    [Fact]
    public void Reached_target_initialization_clears_the_full_4B0_payload_and_runs_age_zero_in_the_same_scan()
    {
        Fixture fixture = new(0, 1);
        NativeBattlePetrifyStare127Frame frame = fixture.WithRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteByte(15, 0));
        frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0]] } };
        NativeBattlePetrifyStare127Frame result = fixture.Step(frame);
        NativeBattleModulePoolTask target = fixture.Target(result);
        Assert.Equal((ushort)1, target.ReadWord(12));
        Assert.Equal((byte)1, fixture.Root(result).ReadByte(14));
        Assert.Equal((ushort)0, target.ReadWord(0x16));
        Assert.Equal(0u, target.ReadDword(0x18));
        Assert.Equal(0u, target.ReadDword(0x1C));
        Assert.Equal(0x12377BCu, target.ReadDword(0x24));
        Assert.Equal(0x123BC84u, target.ReadDword(0x1EC));
        Assert.Equal(new byte[16], target.ReadBytes(0x4AC, 16));
        Assert.Equal(new[] { typeof(NativeBattlePetrifyStare127AdvanceAct), typeof(NativeBattlePetrifyStare127Sound), typeof(NativeBattlePetrifyStare127StageClear) }, fixture.Operations.Select(operation => operation.GetType()));
    }

    [Fact]
    public void Snapshot_next_keeps_current_pools_state_and_native_operation_order()
    {
        Fixture first = new(42);
        string json = NativeBattleTestSnapshots.Json(first.Frame);
        NativeBattlePetrifyStare127Frame expected = first.Step(first.Frame);
        Fixture next = new(42);
        NativeBattlePetrifyStare127Frame actual = next.Step(NativeBattleTestSnapshots.RestoreJson<NativeBattlePetrifyStare127Frame>(json)!);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Operations.ToImmutableArray()), NativeBattleTestSnapshots.Json(next.Operations.ToImmutableArray()));
    }

    private static uint Working(NativeBattlePetrifyStare127Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private sealed class Fixture
    {
        public NativeBattlePetrifyStare127Frame Frame { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public List<NativeBattlePetrifyStare127Operation> Operations { get; } = [];
        public List<int> AnchorActors { get; } = [];
        public int BoneReads { get; private set; }
        public Func<NativeBattlePetrifyStare127Frame, NativeBattlePetrifyStare127Operation, NativeBattlePetrifyStare127Frame> Observe { get; set; } = (frame, _) => frame;
        private NativeBattlePetrifyStare127Resources Resources { get; } = new(Source(FirstActAddress, 0x44C8, 16), Source(SecondActAddress, 0x5828, 36), new("FF8_EN:12414AC", new byte[236].ToImmutableArray()), 0x61AC6);
        public Fixture(ushort age, ushort cycle = 2)
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 8, 11, 0, 29, 127, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            Packets = packets;
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int rootSlot) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteBytes(12, new byte[8]).WriteWord(12, cycle).WriteByte(15, 1)) });
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, ArenaAddress, TargetStride, 1);
            (targets, int targetSlot) = targets.Append(TargetCallback);
            targets = NativeBattleTaskFixture.EditPool(targets, data => data with { Slots = targets.Slots.SetItem(targetSlot, targets.Slots[targetSlot].WriteBytes(12, new byte[TargetStride - 12]).WriteWord(12, age).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteDword(0x20, 0x1D972C0 + 3 * 156)) });
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
            NativeBattleOriginalEffectState effect = new(127, 126, new(127, Entry, Initializer), new(0, 0, 8, 0, 0, 0), null) { Pools = [root, targets], ReturnedDescriptor = RootDescriptor };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
            Frame = new(memory, effect, new(123), NativeBattlePetrifyStare127State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D972C0).WriteDword(StageDepthGlobal, 1234));
        }
        public NativeBattleModulePoolTask Root(NativeBattlePetrifyStare127Frame frame) => frame.Effect.Pools[0].Slots[0];
        public NativeBattleModulePoolTask Target(NativeBattlePetrifyStare127Frame frame) => frame.Effect.Pools[1].Slots[0];
        public NativeBattlePetrifyStare127Frame WithRoot(NativeBattlePetrifyStare127Frame frame, NativeBattleModulePoolTask task) => NativeBattlePetrifyStare127Controller.Write(frame, RootBank, task);
        public NativeBattlePetrifyStare127Frame Step(NativeBattlePetrifyStare127Frame frame) => NativeBattlePetrifyStare127Controller.Advance(frame, Resources, new(Packets, actor => new(actor, 1) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, actor == 0 ? (short)512 : (short)1024) }, (current, _) =>
        {
            BoneReads++;
            return new(current, new(100, 200, 300));
        }, (current, actor) =>
        {
            AnchorActors.Add(actor);
            return new(current, new(700, 800, 900, unchecked((short)0xFEDC)));
        }, (current, operation) =>
        {
            Operations.Add(operation);
            return Observe(current, operation);
        }));
        private static NativeBattleGfAnimationSource Source(uint address, int length, int duration)
        {
            byte[] data = new byte[length];
            BitConverter.GetBytes(8).CopyTo(data, 4);
            BitConverter.GetBytes(duration).CopyTo(data, 12);
            return new(address, data.ToImmutableArray());
        }
    }
}
