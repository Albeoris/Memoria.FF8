using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedPartyLoadTests
{
    [Fact]
    public void Native_cpu_admission_keeps_source_zero_under_hosted_graphics()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown();
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        Assert.True(NativeBattleModelResourceCopies.CanUseResourceStorage(native, 0, graphics));
        NativeBattleModelResourceCopies.RequirePlannedTransition(native, 0, graphics);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceCopies.CanUseResourceStorage(native, 7, graphics));
        Assert.Null(native.Cpu.PresentationBanks);
    }

    [Fact]
    public void Hosted_cpu_keeps_a_retained_native_animation_key_at_an_unbound_row()
    {
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown().BindAnimation(2, new(2 * 0x3020 + 0x20, [7, 24], 8));
        NativeBattleModelCpuResources hosted = native with { Cpu = native.Cpu.WithPresentationRoster(Roster()) };
        hosted.Validate();
        Assert.Equal(new byte[] { 7, 24 }, hosted.ReadAnimationHeader(2 * 32 + 8, 0, 2));
        Assert.Throws<InvalidDataException>(() => hosted.BindAnimation(2, new(2 * 0x3020 + 0x20, [0], 8)));
    }

    [Fact]
    public void Body_and_linked_loads_publish_isolated_sections_and_hosted_animation_keys()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources cpu = Resources(roster);
        NativeBattleModelDatSource body = TaggedBody(31);
        NativeBattleModelDatSource otherBody = TaggedBody(42);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        NativeBattlePartyLoadPlan first = Plan(0, 7, roster, body, linked);
        NativeBattlePartyLoadPlan second = Plan(0, 24, roster, otherBody, linked);
        Assert.False(first.RequiresResourceStorage);
        Assert.True(first.UseResourceStorage);

        NativeBattleModelLoadStep step = Load(0, first, body);
        cpu = Publish(cpu, step, 0, null);
        NativeBattleModelLoader retained = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelLoader>(NativeBattleTestSnapshots.Json(step.State));
        retained.Validate();
        Assert.NotNull(retained.PartyPlan);
        cpu = Publish(cpu, Load(0x1000, retained.PartyPlan!, linked), 0, 1);
        cpu = Publish(cpu, Load(0, second, otherBody), 2, null);
        cpu = Publish(cpu, Load(0x1000, second, linked), 2, 3);

        Assert.Equal(new byte[] { 99 }, cpu.Cpu.GetBank(0).ReadGuest(1, 1));
        foreach ((int row, byte bank, int bodyAlias, int linkedAlias, byte tag) in new[] { (7, (byte)9, 0, 1, (byte)31), (24, (byte)26, 2, 3, (byte)42) })
        {
            Assert.Equal(new byte[] { tag }, cpu.Cpu.GetBank(bank).Read(1, 1));
            NativeBattleModelCpuReference tail = cpu.Rows[bodyAlias].Require(8);
            Assert.Equal(bank, tail.Bank);
            Assert.True(tail.ResourceOnly);
            Assert.Equal(first.Body.CopiedLength, tail.Offset);
            Assert.Equal(bank, cpu.Rows[linkedAlias].Require(12).Bank);
            Assert.Equal((byte)row, cpu.Rows[bodyAlias].LinkedPhysicalIndex36);
            Assert.Equal(body.Bytes.AsSpan().Slice(body.Section(6), 8).ToArray(), cpu.ReadAnimationHeader(row * 32 + 8, 0, 8));
            Assert.Equal(linked.Bytes.AsSpan().Slice(linked.Section(7), 8).ToArray(), cpu.ReadAnimationHeader(row * 32 + 24, 0, 8));
            Assert.Throws<NotSupportedException>(() => cpu.Cpu.GetBank(bank).ReadGuest(1, 1));
        }
        Assert.Empty(cpu.AnimationPointers);
        Assert.Throws<NotSupportedException>(() => cpu.AnimationFrames.ReadGuest(0x20, 1));
        Assert.Throws<InvalidDataException>(() => cpu.AnimationFrames.ReadGuest(7 * 0x3020 + 0x20, 1));
        cpu.Validate();
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(cpu));
        restored.Validate();
        Assert.Equal(cpu.ReadAnimationHeader(24 * 32 + 24, 0, 8), restored.ReadAnimationHeader(24 * 32 + 24, 0, 8));
        Assert.Equal(new byte[] { 42 }, restored.Cpu.GetBank(26).Read(1, 1));
    }

    [Fact]
    public void Shared_weapon_callback_patches_relative_geometry_inside_the_hosted_bank()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8) with { ModelId = 1 };
        NativeBattleModelDatSource linked = SpecialSource(1, linked: true);
        NativeBattlePartyLoadPlan plan = Plan(1, 24, roster, body, linked);
        NativeBattleModelCpuResources cpu = Publish(Resources(roster), Load(1, plan, body), 0, null);
        NativeBattleModelCpuReference geometry = cpu.Rows[0].Require(16);
        NativeBattleModelLoadStep weapon = Load(0x1001, plan, linked);
        Assert.Equal(0x507E20u, weapon.Bind!.Task.Callback);
        cpu = Publish(cpu, weapon, 0, null);
        Assert.Equal(geometry, cpu.Rows[0].Require(16));
        Assert.Equal((byte)26, geometry.Bank);
        Assert.Equal(plan.Body.CopiedLength - geometry.Offset + 8, BinaryPrimitives.ReadInt32LittleEndian(cpu.Cpu.GetBank(26).Read(geometry.Offset + 8, 4)));
        Assert.Equal((byte)24, cpu.Rows[0].LinkedPhysicalIndex36);
        Assert.Null(weapon.State.PartyPlan);
        Assert.False(cpu.AnimationSources.ContainsKey(24 * 32 + 24));
        cpu.Validate();
    }

    [Fact]
    public void Model_seven_retains_the_extra_poll_and_no_linked_task()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelDatSource source = SpecialSource(7, linked: false);
        NativeBattlePartyLoadPlan plan = Plan(7, 7, roster, source);
        Assert.Null(plan.Linked);
        NativeBattleModelLoadStep loaded = Load(7, plan, source);
        Assert.Equal((byte)3, loaded.Bind!.Task.Phase);
        NativeBattleModelCpuResources cpu = Publish(Resources(roster), loaded, 0, null);
        Assert.Equal((byte)9, cpu.Rows[0].Require(40).Bank);
        Assert.Equal(source.Bytes.AsSpan().Slice(source.Section(9), 4).ToArray(), cpu.ReadAnimationHeader(7 * 32 + 8, 0, 4));
        NativeBattleModelLoader noLink = NativeBattleModelLoader.Queue507080(0x1007, 7, 0, 0, 0, roster);
        Assert.Null(noLink.Task);
        noLink.Validate();
    }

    [Fact]
    public void Queue_and_completion_reject_mismatched_rosters_sources_and_tail_owners()
    {
        BattleModelRoster roster = Roster();
        BattleModelRoster other = new(roster.Rows.SetItem(24, new("different", true)));
        NativeBattleModelDatSource body = TaggedBody(31);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        NativeBattlePartyLoadPlan plan = Plan(0, 24, roster, body, linked);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelLoader.Queue507080(0, 8, 0, 0, 0, roster));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelLoader.Queue507080(0, OriginalOwner(6), 0, 0, 0, roster));
        Assert.Equal(0x507120u, NativeBattleModelLoader.Queue507080(20, 24, 0, 0, 0, roster).Task!.Callback);
        Assert.Equal(0x507120u, NativeBattleModelLoader.Queue507080(20, OriginalOwner(6), 0, 0, 0, roster).Task!.Callback);
        Assert.Throws<InvalidDataException>((NativeBattleModelLoader.Queue507080(0, 24, 0, 0, 0, other) with { PartyPlan = plan }).Validate);
        NativeBattleModelDatSource changed = TaggedBody(32);
        Assert.Throws<InvalidDataException>(() => Load(0, plan, changed));

        NativeBattleModelLoadStep prepared = Load(0, plan, body);
        Assert.Throws<InvalidDataException>(() => Publish(Resources(other), prepared, 0, null));
        NativeBattleModelCpuResources cpu = Publish(Resources(roster), prepared, 0, null);
        NativeBattleModelLoadStep weapon = Load(0x1000, plan, linked);
        foreach (byte invalidBank in new byte[] { 3, 4, 9 })
        {
            NativeBattleModelCpuResources wrongTail = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(8, cpu.Rows[0].Require(8) with { Bank = invalidBank })) };
            Assert.Throws<InvalidDataException>(() => Publish(wrongTail, weapon, 0, 1));
        }
        NativeBattleModelAnimationCopy animation = cpu.AnimationSources[24 * 32 + 8];
        Assert.Throws<InvalidDataException>((cpu with { AnimationSources = cpu.AnimationSources.Add(6 * 32 + 8, animation) }).Validate);
        Assert.Throws<InvalidDataException>((cpu with { AnimationPointers = cpu.AnimationPointers.Add(24 * 32 + 8, 0) }).Validate);
    }

    private static NativeBattlePartyLoadPlan Plan(byte model, byte row, BattleModelRoster roster, params NativeBattleModelDatSource[] sources)
    {
        return NativeBattlePartyLoadPlan.Create(model, row, 0, 0, NativeBattlePartyCpuLayout.Classic, sources, roster);
    }

    private static NativeBattleModelLoadStep Load(ushort request, NativeBattlePartyLoadPlan plan, NativeBattleModelDatSource source)
    {
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(request, plan.PhysicalIndex, 0, 0, 0, plan.PresentationRoster) with { PartyPlan = plan };
        NativeBattleModelLoadStep requested = loader.Poll507070(-1, [source], NativeBattleFileStaging.Unknown());
        Assert.Equal((byte)1, requested.State.Task!.Phase);
        Assert.Same(requested.State, requested.State.Poll507070(-1, [], requested.Staging).State);
        NativeBattleModelLoadStep prepared = requested.State.Poll507070(source.Bytes.Length, [], requested.Staging);
        Assert.Equal((byte)2, prepared.State.Task!.Phase);
        Assert.NotNull(prepared.TextureUpload);
        NativeBattleModelLoadStep complete = prepared.State.Poll507070(source.Bytes.Length, [], prepared.Staging);
        if (plan.ModelId == 7)
        {
            Assert.Equal((byte)3, complete.State.Task!.Phase);
            complete = complete.State.Poll507070(source.Bytes.Length, [], complete.Staging);
        }
        Assert.Equal(0, complete.Remaining);
        return complete;
    }

    private static NativeBattleModelCpuResources Publish(NativeBattleModelCpuResources cpu, NativeBattleModelLoadStep loaded, int body, int? linked)
    {
        NativeBattleModelResourceCompletion result = NativeBattleModelResourceCopies.Complete(loaded.Bind!, cpu.Cpu, cpu.Rows, body, linked);
        cpu = cpu with { Cpu = result.Cpu, Rows = result.Rows };
        return result.AnimationCopy is { } animation ? cpu.BindAnimation(loaded.Bind!.Task.PhysicalIndex, animation) : cpu;
    }

    private static NativeBattleModelCpuResources Resources(BattleModelRoster roster)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleModelCpuBank native = cpu.Cpu.GetBank(0).WriteGuest(1, [99]);
        return cpu with { Cpu = cpu.Cpu.WithBank(0, native).WithPresentationRoster(roster) };
    }

    private static BattleModelRoster Roster()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new BattlePartyActorModel(new("native"));
        rows[6] = new BattleEnemyActorModel(new("enemy"));
        rows[7] = new BattlePartyActorModel(new("party-seven"));
        rows[24] = new BattlePartyActorModel(new("party-twenty-four"));
        return new(rows.ToImmutableArray());
    }

    private static NativeBattleModelDatSource TaggedBody(byte tag)
    {
        NativeBattleModelDatSource source = NativeBattleModelResourceCapacityTests.Source(false, 8);
        return source with { Bytes = source.Bytes.SetItem(source.Section(0) + 1, tag) };
    }

    private static NativeBattleModelDatSource SpecialSource(byte model, bool linked)
    {
        byte[] empty = new byte[4];
        byte[] pack = [0, 0, 0, 0, 8, 0, 0, 0];
        byte[][] sections = linked ? [empty, empty, empty, empty, pack] : [empty, empty, empty, empty, empty, empty, empty, empty, pack, [1, 2, 3, 4]];
        int cursor = 4 + (sections.Length + 1) * 4;
        byte[] dat = new byte[cursor + sections.Sum(section => section.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(dat, sections.Length);
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(dat.AsSpan(4 + index * 4), cursor);
            sections[index].CopyTo(dat, cursor);
            cursor += sections[index].Length;
        }
        BinaryPrimitives.WriteInt32LittleEndian(dat.AsSpan(4 + sections.Length * 4), cursor);
        return new(model, linked, 0, 310, "special.dat", dat.ToImmutableArray());
    }
}
