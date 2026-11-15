using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedResourceAliasTests
{
    private static readonly NativeBattleFileStaging EmptyStaging = NativeBattleFileStaging.Unknown();

    private static readonly BattlePartyModelOwner First = (BattlePartyModelOwner)OriginalOwner(0);
    private static readonly ImmutableArray<BattlePartyModelOwner> Parties = [First, (BattlePartyModelOwner)OriginalOwner(1), (BattlePartyModelOwner)OriginalOwner(2),
        .. Enumerable.Range(0, 8).Select(index => new BattlePartyActorModel(new($"party-{index}")))];
    private static readonly BattleModelRoster Roster = new(Parties.Cast<BattleModelOwner>()
        .Concat(Enumerable.Range(3, 4).Select(OriginalOwner))
        .Concat(Enumerable.Range(0, 10).Select(index => new BattleEnemyActorModel(new($"enemy-{index}")))));
    private static readonly NativeBattleModelAliases Aliases = new(Roster, Enumerable.Range(0, 7).Select(slot => (NativeBattleModelSlot.Decode((byte)slot), OriginalOwner(slot))));

    [Fact]
    public void Eleven_body_and_linked_pairs_publish_independent_sections_across_the_native_alias_end()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().WithPresentationRoster(Roster, Aliases);
        Assert.Equal(37, cpu.Rows.Length);
        Assert.Equal(14, Roster.Enemies.Count());

        int ordinal = 0;
        foreach (BattlePartyModelOwner owner in Roster.Party)
        {
            int bodyRow = ordinal * 2;
            int linkedRow = bodyRow + 1;
            var (body, linked) = Sources(ordinal);
            NativeBattlePartyLoadPlan plan = Plan(owner, body, linked);
            cpu = Publish(cpu, Load(0, plan, body), bodyRow, null);
            cpu = Publish(cpu, Load(0x1000, plan, linked), bodyRow, linkedRow);
            if (linkedRow == 11)
            {
                cpu = NativeBattleTestSnapshots.Independent(cpu);
                cpu.Validate();
            }
            ordinal++;
        }

        Assert.Equal(11, ordinal);
        cpu = cpu with { LastModel142Row = 21 };
        cpu.Validate();
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.Independent(cpu);
        restored.Validate();
        Assert.Equal(21, restored.LastModel142Row);
        Assert.Equal(3, restored.Cpu.Banks.Length);
        Assert.Equal(8, restored.Cpu.PresentationBanks!.Count);
        ordinal = 0;
        foreach (BattlePartyModelOwner owner in Roster.Party)
        {
            int bodyRow = ordinal * 2;
            int linkedRow = bodyRow + 1;
            int expectedBank = restored.Cpu.PartyBankIndex(owner);
            bool original = Aliases.TryEncodeExact(owner, out byte operand);
            NativeBattleModelCpuResourceRow body = restored.Rows[bodyRow];
            Assert.Equal(expectedBank, body.Require(12).Bank);
            Assert.Equal(expectedBank, restored.Rows[linkedRow].Require(12).Bank);
            Assert.Equal(original ? operand : (byte?)null, body.LinkedPhysicalIndex36);
            Assert.Equal(!original, body.Require(12).ResourceOnly);
            Assert.Equal(new byte[] { (byte)(1 + ordinal) }, ReadTag(restored, bodyRow));
            Assert.Equal(new byte[] { (byte)(101 + ordinal) }, ReadTag(restored, linkedRow));
            ordinal++;
        }
        Assert.All(restored.Rows.Skip(22), row => Assert.Empty(row.Fields));
        var invalidated = restored.InvalidateResourceExtents();
        Assert.Equal(37, invalidated.Rows.Length);
        Assert.All(invalidated.Rows, row => Assert.Empty(row.Lengths));
        Assert.Equal(restored.Rows[21].Require(12), invalidated.Rows[21].Require(12));
    }

    [Fact]
    public void Partial_hosted_rows_upgrade_without_rebinding_existing_rows_or_pinned_sections()
    {
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown().BindPartyOwner(First, NativeBattleModelSlot.Decode(0));
        var (body, linked) = Sources(0);
        NativeBattlePartyLoadPlan plan = Plan(First, body, linked);
        native = Publish(native, Load(0, plan, body), 10, null);
        native.Validate();
        Assert.Equal(11, native.Rows.Length);
        NativeBattleModelCpuReference section = native.Rows[10].Require(12);
        NativeBattleResourcePointer pinned = new NativeBattleResourcePointer(section, null, 64).Pin(native);

        NativeBattleModelCpuResources partial = native with { Cpu = native.Cpu.WithPresentationRoster(Roster, Aliases) };
        partial.Validate();
        NativeBattleModelCpuResources upgraded = partial.WithPresentationRoster(Roster, Aliases);
        Assert.Equal(37, upgraded.Rows.Length);
        Assert.Same(partial.Cpu.PartyBankOwners, upgraded.Cpu.PartyBankOwners);
        Assert.Same(native.Rows[10], upgraded.Rows[10]);
        Assert.True(upgraded.Rows == upgraded.WithPresentationRoster(Roster, Aliases).Rows);
        Assert.Equal(new byte[] { 1 }, NativeBattleModelResourceView.Open(upgraded, EmptyStaging, pinned, 64).Read(1, 1));

        BattleModelRoster other = new(Roster.Owners.Select(owner => owner == First ? new BattlePartyActorModel(new("replacement")) : owner));
        Assert.Throws<InvalidDataException>(() => partial.WithPresentationRoster(other, new(other, [])));
        NativeBattleModelDatSource changed = Sources(1).Body;
        NativeBattlePartyLoadPlan changedPlan = Plan(First, changed, linked);
        upgraded = Publish(upgraded, Load(0, changedPlan, changed), 10, null);
        Assert.Equal(new byte[] { 2 }, ReadTag(upgraded, 10));
        Assert.Equal(new byte[] { 1 }, NativeBattleModelResourceView.Open(upgraded, EmptyStaging, pinned, 64).Read(1, 1));
    }

    [Fact]
    public void Validation_and_completion_reject_unowned_aliases_and_out_of_extent_references()
    {
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown().BindPartyOwner(First, NativeBattleModelSlot.Decode(0));
        var (body, linked) = Sources(0);
        NativeBattlePartyLoadPlan nativePlan = Plan(First, body, linked);
        NativeBattleModelLoadStep nativeBody = Load(0, nativePlan, body);
        NativeBattleModelCpuResources incompleteNative = native with { Rows = native.Rows.RemoveAt(native.Rows.Length - 1) };
        Assert.Throws<InvalidDataException>(incompleteNative.Validate);
        Assert.Throws<InvalidDataException>(() => Publish(incompleteNative, nativeBody, 0, null));

        NativeBattleModelCpuResources hosted = native.WithPresentationRoster(Roster, Aliases);
        NativeBattlePartyLoadPlan hostedPlan = Plan(Parties[^1], body, linked);
        NativeBattleModelLoadStep hostedBody = Load(0, hostedPlan, body);
        NativeBattleModelLoadStep hostedLink = Load(0x1000, hostedPlan, linked);
        Assert.Throws<InvalidDataException>(() => Publish(hosted, hostedBody, 37, null));
        Assert.Throws<InvalidDataException>(() => Publish(hosted, hostedLink, 0, -1));
        Assert.Throws<InvalidDataException>(() => Publish(hosted, hostedLink, 0, 37));
        var partial = hosted with { Rows = hosted.Rows.Take(12).ToImmutableArray(), LastModel142Row = 12 };
        Assert.Throws<InvalidDataException>(partial.Validate);
        Assert.Throws<InvalidDataException>(() => Publish(partial, hostedBody, 12, null));
        var extended = hosted with { Rows = hosted.Rows.Add(NativeBattleModelCpuResourceRow.Unknown()) };
        extended = Publish(extended, hostedBody, 37, null);
        extended.Validate();
        Assert.True(extended.Rows == extended.WithPresentationRoster(Roster, Aliases).Rows);
        Assert.Equal(new byte[] { 1 }, ReadTag(extended, 37));

        var oversized = hosted with { Rows = hosted.Rows.AddRange(Enumerable.Range(0, 52 - hosted.Rows.Length).Select(_ => NativeBattleModelCpuResourceRow.Unknown())) };
        NativeBattleModelCpuResources final = Publish(oversized, hostedBody, 51, null);
        final.Validate();
        Assert.Equal(new byte[] { 1 }, ReadTag(final, 51));
        Assert.Equal(52, final.Rows.Length);
        Assert.Same(hosted.Rows[0], final.Rows[0]);

        hosted = Publish(hosted, hostedBody, 36, null);
        NativeBattleModelCpuResourceRow row = hosted.Rows[36];
        NativeBattleModelCpuReference section = row.Require(12);
        var invalid = hosted with { Rows = hosted.Rows.SetItem(36, row.Set(12, section with { Offset = hosted.Cpu.GetBank(section.Bank).ResourceCapacity + 1 })) };
        Assert.Throws<InvalidDataException>(invalid.Validate);
    }

    private static (NativeBattleModelDatSource Body, NativeBattleModelDatSource Linked) Sources(int ordinal)
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        body = body with { Bytes = body.Bytes.SetItem(body.Section(0) + 1, checked((byte)(1 + ordinal))) };
        linked = linked with { Bytes = linked.Bytes.SetItem(linked.Section(0) + 1, checked((byte)(101 + ordinal))) };
        return (body, linked);
    }

    private static NativeBattlePartyLoadPlan Plan(BattlePartyModelOwner owner, NativeBattleModelDatSource body, NativeBattleModelDatSource linked)
    {
        NativeBattleModelSlot? slot = Aliases.TryEncodeExact(owner, out byte operand) ? NativeBattleModelSlot.Decode(operand) : null;
        return NativeBattlePartyLoadPlan.Create(0, owner, slot, 0, 0, NativeBattlePartyCpuLayout.Classic, [body, linked]);
    }

    private static NativeBattleModelLoadStep Load(ushort request, NativeBattlePartyLoadPlan plan, NativeBattleModelDatSource source)
    {
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(request, plan.Owner, plan.OriginalSlot, 0, 0, 0) with { PartyPlan = plan };
        NativeBattleModelLoadStep queued = loader.Poll507070(-1, [source], NativeBattleFileStaging.Unknown());
        NativeBattleModelLoadStep prepared = queued.State.Poll507070(source.Bytes.Length, [], queued.Staging);
        Assert.NotNull(prepared.TextureUpload);
        NativeBattleModelLoadStep complete = prepared.State.Poll507070(source.Bytes.Length, [], prepared.Staging);
        Assert.Equal(0, complete.Remaining);
        return complete;
    }

    private static NativeBattleModelCpuResources Publish(NativeBattleModelCpuResources cpu, NativeBattleModelLoadStep step, int bodyRow, int? linkedRow)
    {
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(step.Bind!, cpu.Cpu, cpu.Rows, bodyRow, linkedRow);
        cpu = cpu with { Cpu = completed.Cpu, Rows = completed.Rows };
        return completed.AnimationCopy is { } animation ? cpu.BindAnimation((BattlePartyModelOwner)step.Bind!.Task.Owner, step.Bind.Task.OriginalSlot, animation) : cpu;
    }

    private static byte[] ReadTag(NativeBattleModelCpuResources cpu, int row)
    {
        NativeBattleModelCpuReference section = cpu.Rows[row].Require(12);
        return NativeBattleModelResourceView.Open(cpu, EmptyStaging, section, 64).Read(1, 1);
    }
}
