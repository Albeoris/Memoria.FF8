using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedResourceAliasTests
{
    private static readonly NativeBattleFileStaging EmptyStaging = NativeBattleFileStaging.Unknown();

    [Fact]
    public void Eleven_body_and_linked_pairs_publish_independent_sections_across_the_native_alias_end()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().WithPresentationRoster(roster);
        Assert.Equal(37, cpu.Rows.Length);
        Assert.Equal(14, roster.EnemyRows.Count());

        int ordinal = 0;
        foreach (int actor in roster.PartyRows)
        {
            int bodyRow = ordinal * 2;
            int linkedRow = bodyRow + 1;
            var (body, linked) = Sources(ordinal);
            NativeBattlePartyLoadPlan plan = Plan(roster, actor, body, linked);
            cpu = Publish(cpu, Load(0, plan, body), bodyRow, null);
            cpu = Publish(cpu, Load(0x1000, plan, linked), bodyRow, linkedRow);
            if (linkedRow == 11)
            {
                cpu = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(cpu));
                cpu.Validate();
            }
            ordinal++;
        }

        Assert.Equal(11, ordinal);
        cpu = cpu with { LastModel142Row = 21 };
        cpu.Validate();
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(cpu));
        restored.Validate();
        Assert.Equal(21, restored.LastModel142Row);
        Assert.Equal(3, restored.Cpu.Banks.Length);
        Assert.Equal(8, restored.Cpu.PresentationBanks!.Count);
        ordinal = 0;
        foreach (int actor in roster.PartyRows)
        {
            int bodyRow = ordinal * 2;
            int linkedRow = bodyRow + 1;
            int expectedBank = actor < 3 ? actor : actor + 2;
            NativeBattleModelCpuResourceRow body = restored.Rows[bodyRow];
            Assert.Equal(expectedBank, body.Require(12).Bank);
            Assert.Equal(expectedBank, restored.Rows[linkedRow].Require(12).Bank);
            Assert.Equal(actor, body.LinkedPhysicalIndex36!.Value);
            Assert.Equal(actor >= 3, body.Require(12).ResourceOnly);
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
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown();
        var (body, linked) = Sources(0);
        NativeBattlePartyLoadPlan plan = Plan(null, 0, body, linked);
        native = Publish(native, Load(0, plan, body), 10, null);
        native.Validate();
        Assert.Equal(11, native.Rows.Length);
        NativeBattleModelCpuReference section = native.Rows[10].Require(12);
        NativeBattleResourcePointer pinned = new NativeBattleResourcePointer(section, null, 64).Pin(native);

        NativeBattleModelCpuResources partial = native with { Cpu = native.Cpu.WithPresentationRoster(roster) };
        partial.Validate();
        NativeBattleModelCpuResources upgraded = partial.WithPresentationRoster(roster);
        Assert.Equal(37, upgraded.Rows.Length);
        Assert.Same(partial.Cpu, upgraded.Cpu);
        Assert.Same(native.Rows[10], upgraded.Rows[10]);
        Assert.Same(upgraded, upgraded.WithPresentationRoster(roster));
        Assert.Equal(new byte[] { 1 }, NativeBattleModelResourceView.Open(upgraded, EmptyStaging, pinned, 64).Read(1, 1));

        BattleModelRoster other = new(roster.Rows.SetItem(0, new("different", true)));
        Assert.Throws<InvalidDataException>(() => partial.WithPresentationRoster(other));
        NativeBattleModelDatSource changed = Sources(1).Body;
        NativeBattlePartyLoadPlan changedPlan = Plan(roster, 0, changed, linked);
        upgraded = Publish(upgraded, Load(0, changedPlan, changed), 10, null);
        Assert.Equal(new byte[] { 2 }, ReadTag(upgraded, 10));
        Assert.Equal(new byte[] { 1 }, NativeBattleModelResourceView.Open(upgraded, EmptyStaging, pinned, 64).Read(1, 1));
    }

    [Fact]
    public void Validation_and_completion_reject_unowned_aliases_and_out_of_extent_references()
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.Unknown();
        var (body, linked) = Sources(0);
        NativeBattlePartyLoadPlan nativePlan = Plan(null, 0, body, linked);
        NativeBattleModelLoadStep nativeBody = Load(0, nativePlan, body);
        NativeBattleModelCpuResources grownNative = native with { Rows = native.Rows.Add(NativeBattleModelCpuResourceRow.Unknown()) };
        Assert.Throws<InvalidDataException>(grownNative.Validate);
        Assert.Throws<InvalidDataException>(() => Publish(grownNative, nativeBody, 0, null));

        NativeBattleModelCpuResources hosted = native.WithPresentationRoster(roster);
        NativeBattlePartyLoadPlan hostedPlan = Plan(roster, 14, body, linked);
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
        Assert.Same(extended, extended.WithPresentationRoster(roster));
        Assert.Equal(new byte[] { 1 }, ReadTag(extended, 37));

        var oversized = hosted with { Rows = hosted.Rows.AddRange(Enumerable.Range(0, 52 - hosted.Rows.Length).Select(_ => NativeBattleModelCpuResourceRow.Unknown())) };
        Assert.Throws<InvalidDataException>(oversized.Validate);
        Assert.Throws<InvalidDataException>(() => Publish(oversized, hostedBody, 0, null));

        hosted = Publish(hosted, hostedBody, 36, null);
        NativeBattleModelCpuResourceRow row = hosted.Rows[36];
        NativeBattleModelCpuReference section = row.Require(12);
        var invalid = hosted with { Rows = hosted.Rows.SetItem(36, row.Set(12, section with { Offset = hosted.Cpu.GetBank(section.Bank).ResourceCapacity + 1 })) };
        Assert.Throws<InvalidDataException>(invalid.Validate);
    }

    private static BattleModelRoster Roster()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        for (int row = 0; row < rows.Length; row++)
        {
            bool party = row < 3 || row is >= 7 and <= 14;
            rows[row] = new(party ? $"party-{row}" : $"enemy-{row}", party);
        }
        return new(rows.ToImmutableArray());
    }

    private static (NativeBattleModelDatSource Body, NativeBattleModelDatSource Linked) Sources(int ordinal)
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        body = body with { Bytes = body.Bytes.SetItem(body.Section(0) + 1, checked((byte)(1 + ordinal))) };
        linked = linked with { Bytes = linked.Bytes.SetItem(linked.Section(0) + 1, checked((byte)(101 + ordinal))) };
        return (body, linked);
    }

    private static NativeBattlePartyLoadPlan Plan(BattleModelRoster? roster, int actor, NativeBattleModelDatSource body, NativeBattleModelDatSource linked)
    {
        return NativeBattlePartyLoadPlan.Create(0, checked((byte)actor), 0, 0, NativeBattlePartyCpuLayout.Classic, [body, linked], roster);
    }

    private static NativeBattleModelLoadStep Load(ushort request, NativeBattlePartyLoadPlan plan, NativeBattleModelDatSource source)
    {
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(request, plan.PhysicalIndex, 0, 0, 0, plan.PresentationRoster) with { PartyPlan = plan };
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
        return completed.AnimationCopy is { } animation ? cpu.BindAnimation(step.Bind!.Task.PhysicalIndex, animation) : cpu;
    }

    private static byte[] ReadTag(NativeBattleModelCpuResources cpu, int row)
    {
        NativeBattleModelCpuReference section = cpu.Rows[row].Require(12);
        return NativeBattleModelResourceView.Open(cpu, EmptyStaging, section, 64).Read(1, 1);
    }
}
