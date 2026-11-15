using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedPartyLoadTests
{
    private static readonly BattlePartyModelOwner NativeParty = (BattlePartyModelOwner)OriginalOwner(0);
    private static readonly BattlePartyModelOwner AddedParty = new BattlePartyActorModel(new("added-party"));
    private static readonly BattlePartyModelOwner SecondAddedParty = new BattlePartyActorModel(new("second-added-party"));
    private static readonly BattlePartyModelOwner MissingParty = new BattlePartyActorModel(new("missing-party"));
    private static readonly BattleModelOwner Enemy = OriginalOwner(6);

    [Fact]
    public void Native_cpu_admission_keeps_source_zero_under_hosted_graphics()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown().BindPartyOwner(NativeParty, NativeBattleModelSlot.Decode(0));
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, Aliases(roster), _ => new NativeBattleGraphicsModel(0));
        Assert.True(NativeBattleModelResourceCopies.CanUseResourceStorage(native, NativeParty, graphics));
        NativeBattleModelResourceCopies.RequirePlannedTransition(native, NativeParty, graphics);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceCopies.CanUseResourceStorage(native, AddedParty, graphics));
        Assert.Null(native.Cpu.PresentationBanks);
    }

    [Fact]
    public void Hosted_cpu_keeps_a_retained_native_animation_key_at_an_unbound_row()
    {
        BattleModelRoster roster = Roster();
        var retained = new NativeBattleModelAnimationCopy(2 * 0x3020 + 0x20, [7, 24], 0, 2, 8);
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown();
        NativeBattleModelCpuResources hosted = (native with { AnimationSources = native.AnimationSources.Add(2 * 32 + 8, retained) }).WithPresentationRoster(roster, Aliases(roster));
        hosted.Validate();
        Assert.Equal(new byte[] { 7, 24 }, hosted.ReadAnimationHeader(2 * 32 + 8, 0, 2));
        Assert.Throws<InvalidDataException>(() => hosted.BindAnimation((BattlePartyModelOwner)OriginalOwner(2), NativeBattleModelSlot.Decode(2), retained));
    }

    [Fact]
    public void Body_and_linked_loads_publish_isolated_sections_and_hosted_animation_keys()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources cpu = Resources(roster);
        NativeBattleModelDatSource body = TaggedBody(31);
        NativeBattleModelDatSource otherBody = TaggedBody(42);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        NativeBattlePartyLoadPlan first = Plan(0, AddedParty, body, linked);
        NativeBattlePartyLoadPlan second = Plan(0, SecondAddedParty, otherBody, linked);
        Assert.False(first.RequiresResourceStorage);
        Assert.True(first.UseResourceStorage);

        NativeBattleModelLoadStep step = Load(0, first, body);
        cpu = Publish(cpu, step, 0, null);
        NativeBattleModelLoader retained = NativeBattleTestSnapshots.Independent(step.State);
        retained.Validate();
        Assert.NotNull(retained.PartyPlan);
        cpu = Publish(cpu, Load(0x1000, retained.PartyPlan!, linked), 0, 1);
        cpu = Publish(cpu, Load(0, second, otherBody), 2, null);
        cpu = Publish(cpu, Load(0x1000, second, linked), 2, 3);

        Assert.Equal(new byte[] { 99 }, cpu.Cpu.GetBank(0).ReadGuest(1, 1));
        foreach ((BattlePartyModelOwner owner, int bodyAlias, int linkedAlias, byte tag) in new[] { (AddedParty, 0, 1, (byte)31), (SecondAddedParty, 2, 3, (byte)42) })
        {
            int bank = cpu.Cpu.PartyBankIndex(owner);
            Assert.True(NativeBattlePresentationCpuBanks.IsExtension(bank));
            Assert.Equal(new byte[] { tag }, cpu.Cpu.GetBank(bank).Read(1, 1));
            NativeBattleModelCpuReference tail = cpu.Rows[bodyAlias].Require(8);
            Assert.Equal(bank, tail.Bank);
            Assert.True(tail.ResourceOnly);
            Assert.Equal(first.Body.CopiedLength, tail.Offset);
            Assert.Equal(bank, cpu.Rows[linkedAlias].Require(12).Bank);
            Assert.Null(cpu.Rows[bodyAlias].LinkedPhysicalIndex36);
            Assert.Equal(body.Bytes.AsSpan().Slice(body.Section(6), 8).ToArray(), cpu.ReadAnimationSection(owner, 8, 0, 8));
            Assert.Equal(linked.Bytes.AsSpan().Slice(linked.Section(7), 8).ToArray(), cpu.ReadAnimationSection(owner, 24, 0, 8));
            Assert.Throws<NotSupportedException>(() => cpu.Cpu.GetBank(bank).ReadGuest(1, 1));
        }
        Assert.Empty(cpu.AnimationPointers);
        Assert.Throws<NotSupportedException>(() => cpu.AnimationFrames.ReadGuest(0x20, 1));
        Assert.Throws<InvalidDataException>(() => cpu.AnimationFrames.ReadGuest(7 * 0x3020 + 0x20, 1));
        cpu.Validate();
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.Independent(cpu);
        restored.Validate();
        Assert.Equal(cpu.ReadAnimationSection(SecondAddedParty, 24, 0, 8), restored.ReadAnimationSection(SecondAddedParty, 24, 0, 8));
        Assert.Equal(new byte[] { 42 }, restored.Cpu.GetBank(restored.Cpu.PartyBankIndex(SecondAddedParty)).Read(1, 1));
    }

    [Fact]
    public void Shared_weapon_callback_patches_relative_geometry_inside_the_hosted_bank()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8) with { ModelId = 1 };
        NativeBattleModelDatSource linked = SpecialSource(1, linked: true);
        NativeBattlePartyLoadPlan plan = Plan(1, SecondAddedParty, body, linked);
        NativeBattleModelCpuResources cpu = Publish(Resources(roster), Load(1, plan, body), 0, null);
        NativeBattleModelCpuReference geometry = cpu.Rows[0].Require(16);
        NativeBattleModelLoadStep weapon = Load(0x1001, plan, linked);
        Assert.Equal(0x507E20u, weapon.Bind!.Task.Callback);
        cpu = Publish(cpu, weapon, 0, null);
        Assert.Equal(geometry, cpu.Rows[0].Require(16));
        Assert.Equal(cpu.Cpu.PartyBankIndex(SecondAddedParty), geometry.Bank);
        Assert.Equal(plan.Body.CopiedLength - geometry.Offset + 8, BinaryPrimitives.ReadInt32LittleEndian(cpu.Cpu.GetBank(geometry.Bank).Read(geometry.Offset + 8, 4)));
        Assert.Null(cpu.Rows[0].LinkedPhysicalIndex36);
        Assert.Null(weapon.State.PartyPlan);
        Assert.False(cpu.OwnedAnimationSources.ContainsKey((SecondAddedParty, 24)));
        cpu.Validate();
    }

    [Fact]
    public void Model_seven_retains_the_extra_poll_and_no_linked_task()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelDatSource source = SpecialSource(7, linked: false);
        NativeBattlePartyLoadPlan plan = Plan(7, AddedParty, source);
        Assert.Null(plan.Linked);
        NativeBattleModelLoadStep loaded = Load(7, plan, source);
        Assert.Equal((byte)3, loaded.Bind!.Task.Phase);
        NativeBattleModelCpuResources cpu = Publish(Resources(roster), loaded, 0, null);
        Assert.Equal(cpu.Cpu.PartyBankIndex(AddedParty), cpu.Rows[0].Require(40).Bank);
        Assert.Equal(source.Bytes.AsSpan().Slice(source.Section(9), 4).ToArray(), cpu.ReadAnimationSection(AddedParty, 8, 0, 4));
        NativeBattleModelLoader noLink = NativeBattleModelLoader.Queue507080(0x1007, AddedParty, null, 0, 0, 0);
        Assert.Null(noLink.Task);
        noLink.Validate();
    }

    [Fact]
    public void Queue_and_completion_reject_mismatched_rosters_sources_and_tail_owners()
    {
        BattleModelRoster roster = Roster();
        BattleModelRoster other = new([NativeParty, Enemy, AddedParty, MissingParty]);
        NativeBattleModelDatSource body = TaggedBody(31);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        NativeBattlePartyLoadPlan plan = Plan(0, SecondAddedParty, body, linked);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelLoader.Queue507080(0, AddedParty, NativeBattleModelSlot.Decode(3), 0, 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelLoader.Queue507080(0, Enemy, NativeBattleModelSlot.Decode(6), 0, 0, 0));
        Assert.Equal(0x507120u, NativeBattleModelLoader.Queue507080(20, SecondAddedParty, null, 0, 0, 0).Task!.Callback);
        Assert.Equal(0x507120u, NativeBattleModelLoader.Queue507080(20, Enemy, NativeBattleModelSlot.Decode(6), 0, 0, 0).Task!.Callback);
        Assert.Throws<InvalidDataException>((NativeBattleModelLoader.Queue507080(0, MissingParty, null, 0, 0, 0) with { PartyPlan = plan }).Validate);
        NativeBattleModelDatSource changed = TaggedBody(32);
        Assert.Throws<InvalidDataException>(() => Load(0, plan, changed));

        NativeBattleModelLoadStep prepared = Load(0, plan, body);
        Assert.Throws<InvalidDataException>(() => Publish(Resources(other), prepared, 0, null));
        NativeBattleModelCpuResources cpu = Publish(Resources(roster), prepared, 0, null);
        NativeBattleModelLoadStep weapon = Load(0x1000, plan, linked);
        foreach (int invalidBank in new[] { 3, 4, cpu.Cpu.PartyBankIndex(AddedParty) })
        {
            NativeBattleModelCpuResources wrongTail = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(8, cpu.Rows[0].Require(8) with { Bank = invalidBank })) };
            Assert.Throws<InvalidDataException>(() => Publish(wrongTail, weapon, 0, 1));
        }
        NativeBattleModelAnimationCopy animation = cpu.OwnedAnimationSources[(SecondAddedParty, 8)];
        Assert.Throws<InvalidDataException>((cpu with { OwnedAnimationSources = cpu.OwnedAnimationSources.Add((MissingParty, 8), animation) }).Validate);
        Assert.Throws<InvalidDataException>((cpu with { AnimationPointers = cpu.AnimationPointers.Add(24 * 32 + 8, 0) }).Validate);
    }

    private static NativeBattlePartyLoadPlan Plan(byte model, BattlePartyModelOwner owner, params NativeBattleModelDatSource[] sources)
    {
        return NativeBattlePartyLoadPlan.Create(model, owner, null, 0, 0, NativeBattlePartyCpuLayout.Classic, sources);
    }

    private static NativeBattleModelLoadStep Load(ushort request, NativeBattlePartyLoadPlan plan, NativeBattleModelDatSource source)
    {
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(request, plan.Owner, plan.OriginalSlot, 0, 0, 0) with { PartyPlan = plan };
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
        return result.AnimationCopy is { } animation ? cpu.BindAnimation((BattlePartyModelOwner)loaded.Bind!.Task.Owner, loaded.Bind.Task.OriginalSlot, animation) : cpu;
    }

    private static NativeBattleModelCpuResources Resources(BattleModelRoster roster)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleModelCpuBank native = cpu.Cpu.GetBank(0).WriteGuest(1, [99]);
        return (cpu with { Cpu = cpu.Cpu.WithBank(0, native) }).WithPresentationRoster(roster, Aliases(roster));
    }

    private static BattleModelRoster Roster() => new([NativeParty, Enemy, AddedParty, SecondAddedParty]);

    private static NativeBattleModelAliases Aliases(BattleModelRoster roster) => new(roster, [(NativeBattleModelSlot.Decode(0), NativeParty), (NativeBattleModelSlot.Decode(6), Enemy)]);

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
