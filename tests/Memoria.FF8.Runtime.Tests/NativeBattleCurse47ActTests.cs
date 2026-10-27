using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47ActTests
{
    [Theory]
    [InlineData(NativeBattleCurse47Act.FirstCallback, 56)]
    [InlineData(NativeBattleCurse47Act.SecondCallback, 55)]
    public void First_ACTs_select_first_enabled_original_model_and_draw_during_initialization(uint callback, byte modelId)
    {
        Fixture fixture = new(callback);
        fixture.Identities[0] = new(0, modelId);
        fixture.Identities[2] = new(2, modelId);
        fixture.Identities[3] = new(2, modelId);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Frame).Frame;
        Assert.Equal(new[] { 0, 1, 2 }, fixture.ReadIdentities);
        Assert.Equal(new[] { 2 }, fixture.ReadAnchors);
        Assert.Equal((ushort)11, fixture.Task(result).ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-22), fixture.Task(result).ReadWord(0x1E));
        Assert.Equal((ushort)33, fixture.Task(result).ReadWord(0x20));
        Assert.Equal((ushort)0xCAFE, fixture.Task(result).ReadWord(0x22));
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x29));
        Assert.Equal((ushort)1, fixture.Task(result).ReadWord(0x24));
        Assert.Equal((ushort)0xFF00, fixture.Task(result).ReadWord(0x88));
        Assert.Equal(4096u, fixture.Task(result).ReadDword(0x50));
        Assert.Equal(4096u, fixture.Task(result).ReadDword(0x54));
        Assert.Equal(4096u, fixture.Task(result).ReadDword(0x58));
        Assert.Equal(1, fixture.Prepared);
        Assert.Equal(1, fixture.Emitted);
        Assert.Equal((ushort)7, result.State.SharedWord(0x10));
        Assert.Equal((ushort)9, result.State.SharedWord(0x12));
        Assert.Equal((ushort)10, result.State.SharedWord(0x14));
    }

    [Fact]
    public void Actor_search_skips_unknown_disabled_model_identity_but_requires_it_for_enabled_rows()
    {
        Fixture fixture = new(NativeBattleCurse47Act.FirstCallback);
        fixture.Identities[0] = new(0, null);
        fixture.Identities[1] = new(2, 56);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Frame).Frame;
        Assert.Equal(new[] { 0, 1 }, fixture.ReadIdentities);
        Assert.Equal(new[] { 1 }, fixture.ReadAnchors);
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x29));
        Fixture unknown = new(NativeBattleCurse47Act.FirstCallback);
        unknown.Identities[0] = new(2, null);
        Assert.Throws<NotSupportedException>(() => unknown.Step(unknown.Frame));
        Assert.Empty(unknown.ReadAnchors);
        Assert.Equal(0, unknown.Prepared);
        Assert.Equal(new[] { 0 }, unknown.ReadIdentities);
    }

    [Theory]
    [InlineData(0xFFFF, false)]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(0x7FFF, true)]
    public void Midpoint_ACT_wait_uses_signed_WORD_age_and_still_increments_age_when_waiting(ushort age, bool initialized)
    {
        Fixture fixture = new(NativeBattleCurse47Act.MidpointCallback);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x24, age))).Frame;
        Assert.Equal(initialized ? 1 : 0, fixture.Prepared);
        Assert.Equal(initialized ? (byte)1 : (byte)0, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(unchecked((ushort)(age + 1)), fixture.Task(result).ReadWord(0x24));
        Assert.Equal(initialized ? (ushort)0xFF80 : (ushort)0, fixture.Task(result).ReadWord(0x88));
    }

    [Fact]
    public void Midpoint_reads_55_then_56_and_truncates_signed_odd_sums_toward_zero_without_writing_W()
    {
        Fixture fixture = new(NativeBattleCurse47Act.MidpointCallback);
        fixture.Identities[1] = new(2, 55);
        fixture.Identities[4] = new(2, 56);
        fixture.Anchor = actor => actor == 1 ? new(-6, 32767, -32768) : new(1, 32766, -32767);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x24, 15))).Frame;
        Assert.Equal(new[] { 1, 4 }, fixture.ReadAnchors);
        Assert.Equal(unchecked((ushort)-2), fixture.Task(result).ReadWord(0x1C));
        Assert.Equal((ushort)32766, fixture.Task(result).ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)-32767), fixture.Task(result).ReadWord(0x20));
        Assert.Equal((ushort)0xCAFE, fixture.Task(result).ReadWord(0x22));
    }

    [Fact]
    public void Missing_second_model_keeps_original_task_position_but_retains_the_first_actual_anchor_side_effect()
    {
        Fixture fixture = new(NativeBattleCurse47Act.MidpointCallback);
        fixture.Identities[1] = new(2, 55);
        NativeBattleModulePoolTask task = fixture.Task(fixture.Frame).WriteWord(0x24, 15).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Put(fixture.Frame, task)).Frame;
        Assert.Equal(new[] { 1 }, fixture.ReadAnchors);
        Assert.Equal((ushort)100, fixture.Task(result).ReadWord(0x1C));
        Assert.Equal((ushort)200, fixture.Task(result).ReadWord(0x1E));
        Assert.Equal((ushort)300, fixture.Task(result).ReadWord(0x20));
        Assert.Equal((ushort)7, result.State.SharedWord(0x10));
        Assert.Equal(1, fixture.Prepared);
    }

    [Fact]
    public void Actual_workspace_completes_only_on_animation_zero_and_waits_for_children_after_terminal_flag()
    {
        Fixture fixture = new(NativeBattleCurse47Act.FirstCallback, 2);
        NativeBattleCurse47Frame frame = fixture.Step(fixture.Frame).Frame;
        frame = fixture.Put(frame, fixture.Task(frame).WriteByte(0x28, 1));
        NativeBattleCurse47Step result = fixture.Step(frame);
        Assert.Equal(0, result.Result);
        Assert.Equal((byte)2, fixture.Task(result.Frame).ReadByte(0x29));
        Assert.Equal((byte)1, (byte)(fixture.Task(result.Frame).ReadByte(0x26) & 1));
        int prepared = fixture.Prepared;
        NativeBattleCurse47Frame cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCurse47Frame>(SnapshotJson(result.Frame))!);
        Assert.Equal(SnapshotJson(fixture.Step(result.Frame)), SnapshotJson(fixture.Step(cold)));
        Assert.Equal(prepared, fixture.Prepared);
        NativeBattleCurse47Step completed = fixture.Step(fixture.Put(result.Frame, fixture.Task(result.Frame).WriteByte(0x28, 0)));
        Assert.Equal(2, completed.Result);
        Assert.Equal((byte)0, NativeBattleCurse47Controller.Read(completed.Frame, fixture.Parent).ReadByte(0x28));
    }

    [Fact]
    public void Pause_still_prepares_and_emits_the_actual_current_workspace_without_advancing_its_frame()
    {
        Fixture fixture = new(NativeBattleCurse47Act.FirstCallback);
        NativeBattleCurse47Frame frame = fixture.Step(fixture.Frame).Frame;
        uint before = fixture.Task(frame).ReadDword(0x98);
        NativeBattleCurse47Frame result = fixture.Step(frame with { State = frame.State.WriteDword(0x264D82C, 1) }).Frame;
        Assert.Equal(before, fixture.Task(result).ReadDword(0x98));
        Assert.Equal(2, fixture.Prepared);
        Assert.Equal(2, fixture.Emitted);
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x29));
    }

    [Fact]
    public void Node_callback_task_writes_are_used_as_the_single_workspace_owner_and_missing_services_fail()
    {
        Fixture fixture = new(NativeBattleCurse47Act.FirstCallback);
        fixture.OnEmit = (current, address) => NativeBattleCurse47Controller.Write(current, address, NativeBattleCurse47Controller.Read(current, address).WriteWord(0x86, 0xA55A));
        NativeBattleCurse47Frame result = fixture.Step(fixture.Frame).Frame;
        Assert.Equal((ushort)0xA55A, fixture.Task(result).ReadWord(0x86));
        Assert.Equal(fixture.Source.Address, fixture.Task(result).ReadDword(0x94));
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47Act.Advance(fixture.Frame, fixture.Address, fixture.Inputs with { PrepareDraw = (_, _) => throw new NotSupportedException("Actual draw preparation required.") }));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteDword(0x78, 501))));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 0xFF))));
    }

    [Fact]
    public void Observed_mode_one_births_three_distinct_ACTs_before_the_particle_under_the_actual_director()
    {
        var source = BirthFrame();
        NativeBattleCurse47Frame before = NativeBattleStorageSnapshots.Capture(source.Frame);
        NativeBattleCurse47Frame frame = NativeBattleCurse47Act.ObserveMode(source.Frame, source.Director, 1, source.Packets);
        Assert.Equal(new uint[] { NativeBattleCurse47Act.FirstCallback, NativeBattleCurse47Act.SecondCallback, NativeBattleCurse47Act.MidpointCallback }, frame.Effect.Pools[4].Slots.Select(task => task.Callback));
        Assert.Equal(new uint[] { 0x17BB134, 0x17BDF6C, 0x17C0DA4 }, frame.Effect.Pools[4].Slots.Select(task => task.ReadDword(0x74)));
        Assert.Equal(new uint[] { 340, 340, 500 }, frame.Effect.Pools[4].Slots.Select(task => task.ReadDword(0x78)));
        Assert.Equal(new ushort[] { 0, 1, 2 }, frame.Effect.Pools[4].Slots.Select(task => task.ReadWord(0x80)));
        Assert.All(frame.Effect.Pools[4].Slots, task => Assert.Equal((ushort)0, task.ReadWord(0x82)));
        Assert.Equal(0x831870u, frame.Effect.Pools[5].Slots[0].Callback);
        Assert.Equal((byte)4, NativeBattleCurse47Controller.Read(frame, source.Director).ReadByte(0x28));
        Assert.Equal((byte)0, NativeBattleCurse47Controller.Read(before, source.Director).ReadByte(0x28));
    }

    [Fact]
    public void Observed_mode_two_births_only_the_original_generic_source_with_zero_context_operands()
    {
        var source = BirthFrame();
        NativeBattleCurse47Frame frame = NativeBattleCurse47Act.ObserveMode(source.Frame, source.Director, 2, source.Packets);
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[3].Slots);
        Assert.Equal(0x82DF60u, generic.Callback);
        Assert.Equal(0x17B97A4u, generic.ReadDword(0x30));
        Assert.Equal((ushort)0, generic.ReadWord(0x298));
        Assert.Equal((ushort)0, generic.ReadWord(0x29C));
        Assert.Equal((ushort)0, generic.ReadWord(0x29E));
        Assert.Equal((byte)1, NativeBattleCurse47Controller.Read(frame, source.Director).ReadByte(0x28));
        Assert.Same(source.Frame, NativeBattleCurse47Act.ObserveMode(source.Frame, source.Director, 3, source.Packets));
    }

    private static (NativeBattleCurse47Frame Frame, uint Director, NativeBattleActionPacketsState Packets) BirthFrame()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 32, 15, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(2, NativeBattleCurse47Director.Callback, 72);
        hierarchy = parent.Hierarchy.Write(parent.Address, parent.Hierarchy.Read(parent.Address).WriteDword(0x0C, 0x1D99A78));
        NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), new(0, 1, 47, 0, 0, 0), null) { Pools = hierarchy.Pools };
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleCurse47IoTests.Initialized()), parent.Address, packets);
    }

    private sealed class Fixture
    {
        public NativeBattleCurse47Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public NativeBattleGfAnimationSource Source { get; }
        public NativeBattleCurse47ActorIdentity[] Identities { get; } = Enumerable.Range(0, 7).Select(_ => new NativeBattleCurse47ActorIdentity(0, 0)).ToArray();
        public List<int> ReadIdentities { get; } = [];
        public List<int> ReadAnchors { get; } = [];
        public int Prepared { get; private set; }
        public int Emitted { get; private set; }
        public Func<int, NativeBattleVector> Anchor { get; set; } = _ => new(11, -22, 33);
        public Func<NativeBattleCurse47Frame, uint, NativeBattleCurse47Frame> OnEmit { get; set; } = (frame, _) => frame;
        public NativeBattleCurse47ActInputs Inputs => new((_, pointer) => Source with { Address = pointer }, (frame, actor) =>
        {
            ReadIdentities.Add(actor);
            return Identities[actor];
        }, (frame, actor, selector, distance) =>
        {
            Assert.Equal((short)15, selector);
            Assert.Equal(0, distance);
            ReadAnchors.Add(actor);
            return new(frame with { State = frame.State.WithSharedWord(0x10, 7) }, Anchor(actor));
        }, (frame, _) =>
        {
            Prepared++;
            return new(frame with { State = frame.State.WithSharedWord(0x12, 9) }, NativeBattleEffectArena.Clear());
        }, (frame, address, context, node) =>
        {
            Emitted++;
            Assert.Equal(0, node.GeometryIndex);
            return OnEmit(frame with { State = frame.State.WithSharedWord(0x14, 10) }, address);
        });
        public Fixture(uint callback, int duration = 40)
        {
            byte[] bytes = new byte[64];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 0x20);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x20), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x24), duration);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x28), 0x10);
            Source = new(0x17BB134, bytes.ToImmutableArray());
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(2, NativeBattleCurse47Director.Callback, 72);
            Parent = parent.Address;
            NativeBattleEffectHierarchyAppend child = parent.Hierarchy.Append8DC540(4, callback, 648, Parent, (_, _, _) => new(0, 3));
            Address = child.Address;
            hierarchy = child.Hierarchy.Write(Address, child.Hierarchy.Read(Address).WriteDword(0x74, Source.Address).WriteDword(0x78, callback == NativeBattleCurse47Act.MidpointCallback ? 500u : 340u).WriteWord(0x22, 0xCAFE));
            NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), null, null) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleCurse47IoTests.Initialized());
        }
        public NativeBattleModulePoolTask Task(NativeBattleCurse47Frame frame) => NativeBattleCurse47Controller.Read(frame, Address);
        public NativeBattleCurse47Frame Put(NativeBattleCurse47Frame frame, NativeBattleModulePoolTask task) => NativeBattleCurse47Controller.Write(frame, Address, task);
        public NativeBattleCurse47Step Step(NativeBattleCurse47Frame frame) => NativeBattleCurse47Act.Advance(frame, Address, Inputs);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
