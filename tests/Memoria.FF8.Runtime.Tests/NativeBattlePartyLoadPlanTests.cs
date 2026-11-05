using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePartyLoadPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_pair_selects_logical_storage_before_small_body_and_retains_guest_shadow_and_pins(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, layout.BankLength - 200);
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(0, 1, 0, 0, layout, [body, linked]);
        Assert.True(plan.Body.CopiedLength < layout.BankLength);
        Assert.True(plan.RequiresResourceStorage);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, cpu.Cpu.Banks[1].Write(0, [11, 22, 33, 44])) } };
        NativeBattleModelCpuBank pinned = cpu.Cpu.Banks[1];
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(0, 1, 0, 0, 0) with { PartyPlan = plan };
        (loader, cpu, staging) = Complete(loader, cpu, staging, body, null);
        Assert.Null(loader.Task);
        Assert.NotNull(loader.PartyPlan);
        Assert.True(cpu.Cpu.Banks[1].HasResourceStorage);
        Assert.True(cpu.Rows[0].Require(12).ResourceOnly);
        Assert.Equal(new byte[] { 11, 22, 33, 44 }, pinned.Read(0, 4));
        Assert.Equal(new byte[] { 11, 22, 33, 44 }, cpu.Cpu.Banks[1].ReadGuest(0, 4));
        string json = NativeBattleTestSnapshots.Json(loader);
        Assert.Contains("Sha256", json);
        Assert.DoesNotContain("Bytes", json);
        loader = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelLoader>(json);
        loader.Validate();
        loader = NativeBattleModelLoader.Queue507080(0x1000, 1, 0, 0, 0) with { PartyPlan = loader.PartyPlan };
        (loader, cpu, staging) = Complete(loader, cpu, staging, linked, 1);
        Assert.Null(loader.PartyPlan);
        Assert.True(cpu.Rows[1].Require(16).ResourceOnly);
        Assert.True(cpu.Cpu.Banks[1].ResourceCapacity > layout.BankLength);
        NativeBattleModelCpuBank changed = cpu.Cpu.Banks[1].Write(0, [99]);
        Assert.Equal(new byte[] { 11 }, changed.ReadGuest(0, 1));
        Assert.Equal(new byte[] { 11 }, pinned.Read(0, 1));
        Assert.NotEqual(new byte[] { 99 }, cpu.Cpu.Banks[1].Read(0, 1));
        cpu.Validate();
    }

    [Fact]
    public void Plan_rejects_missing_or_replaced_selected_source_before_staging_and_old_absent_plan_keeps_old_loader()
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        Assert.Throws<NotSupportedException>(() => NativeBattlePartyLoadPlan.Create(0, 1, 0, 0, NativeBattlePartyCpuLayout.Classic, [body]));
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(0, 1, 0, 0, NativeBattlePartyCpuLayout.Classic, [body, linked]);
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(0, 1, 0, 0, 0) with { PartyPlan = plan };
        NativeBattleModelDatSource changed = body with { Bytes = body.Bytes.SetItem(body.Bytes.Length - 1, 42) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        Assert.Throws<InvalidDataException>(() => loader.Poll507070(-1, [changed], staging));
        NativeBattleModelLoader old = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelLoader>(NativeBattleTestSnapshots.Json(loader with { PartyPlan = null }));
        Assert.Null(old.PartyPlan);
        Assert.DoesNotContain("PartyPlan", NativeBattleTestSnapshots.Json(old));
        Assert.NotNull(old.Poll507070(-1, [changed], staging).FileRequest);
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [body], staging);
        request = request with { State = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelLoader>(NativeBattleTestSnapshots.Json(request.State)) };
        Assert.Null(request.State.Poll507070(-1, [], request.Staging).TextureUpload);
        NativeBattleFileStaging replaced = request.Staging.BindResource(NativeBattleFileStaging.Address, changed.Bytes);
        Assert.Throws<InvalidDataException>(() => request.State.Poll507070(body.Bytes.Length, [], replaced));
    }

    [Fact]
    public void Plan_validates_native_selectors_layout_owner_and_live_promotion_barriers()
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, NativeBattlePartyCpuLayout.Classic.BankLength);
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(0, 1, 0, 0, NativeBattlePartyCpuLayout.Classic, [body, linked]);
        NativeBattleModelLoadTask task = NativeBattleModelLoader.Queue507080(0, 1, 0, 0, 0).Task!;
        Assert.Throws<InvalidDataException>(() => plan.Part(task with { PhysicalIndex = 0 }));
        Assert.Throws<InvalidDataException>(() => plan.Part(task with { Selector = 1 }));
        NativeBattleGraphicsState loading = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(1, new(1, 1)), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, 1, loading);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, 1, loading with { Models = loading.Models.SetItem(1, new(1, 3)) }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, 1, loading with { Effects = loading.Effects with { PublishedCount = 1 } }));
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(0, 1, 0, 0, 0) with { PartyPlan = plan };
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [body], NativeBattleFileStaging.Unknown());
        NativeBattleModelLoadStep prepared = request.State.Poll507070(body.Bytes.Length, [], request.Staging);
        NativeBattleModelLoadStep loaded = prepared.State.Poll507070(body.Bytes.Length, [], prepared.Staging);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceCopies.Complete(loaded.Bind!, NativeBattleModelCpuBanks.Unknown(NativeBattlePartyCpuLayout.Remastered), cpu.Rows, 0, null));
    }

    [Fact]
    public void Production_queue_plans_before_publication_and_poll_rechecks_late_active_owners()
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, NativeBattlePartyCpuLayout.Classic.BankLength);
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        BattleDefinition definition = (BattleDefinition)typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        void Sources(ImmutableArray<NativeBattleModelDatSource> sources)
        {
            typeof(BattleDefinition).GetProperty(nameof(BattleDefinition.NativeGraphics))!.SetValue(definition, new NativeBattleGraphicsDefinition(null!, [new("party", 0, new([0, 0]) { ModelLoadSources = sources })], []));
        }
        Sources([body]);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(battle, NativeBattleModelCpuResources.Unknown());
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(1, new(1, 1) { FileSelector128 = 0, FileSelector129 = 0 }), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
        MethodInfo queue = typeof(BattleSession).GetMethod("QueueNativeModelResource507080", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => queue.Invoke(battle, [graphics, (ushort)0, (byte)1, 0u])).InnerException);
        Assert.Null(battle.NativeModelLoader);
        Assert.Null(battle.NativeFileStaging);
        Sources([body, linked]);
        queue.Invoke(battle, [graphics, (ushort)0, (byte)1, 0u]);
        NativeBattleModelLoader planned = battle.NativeModelLoader!;
        Assert.True(planned.PartyPlan!.RequiresResourceStorage);
        Assert.Null(battle.NativeFileStaging);
        NativeBattleGraphicsState active = graphics with { Effects = graphics.Effects with { PublishedCount = 1 } };
        MethodInfo poll = typeof(BattleSession).GetMethod("PollNativeModelResource507070", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => poll.Invoke(battle, [active])).InnerException);
        Assert.Same(planned, battle.NativeModelLoader);
        Assert.Null(battle.NativeFileStaging);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectIoResult))!.SetValue(battle, -1);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileIo))!.SetValue(battle, NativeBattleFileIoState.FromColdProcess());
        object?[] arguments = [graphics];
        Assert.Equal(1, poll.Invoke(battle, arguments));
        Assert.Equal((byte)1, battle.NativeModelLoader!.Task!.Phase);
        Assert.NotNull(battle.NativeFileStaging);
    }

    [Theory]
    [InlineData(1, 0x507E20)]
    [InlineData(9, 0x507E20)]
    [InlineData(7, 0)]
    public void Native_special_callbacks_plan_the_selected_copy_extent_and_reject_conflicting_catalogs(byte model, int linkedCallback)
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8) with { ModelId = model, Selector = 2 };
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8) with { ModelId = model, Selector = 3 };
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(model, 0, 2, 3, NativeBattlePartyCpuLayout.Classic, [body, linked]);
        Assert.Equal(model == 7 ? 0x5079B0u : 0x5077B0u, plan.Body.Callback);
        Assert.Equal(body.Section(model == 7 ? 7 : 5) - body.Section(0), plan.Body.CopiedLength);
        if (model == 7)
        {
            Assert.Null(plan.Linked);
        }
        else
        {
            Assert.Equal((uint)linkedCallback, plan.Linked!.Callback);
            Assert.Equal(linked.Section(3) - linked.Section(0), plan.Linked.CopiedLength);
        }
        Assert.Throws<InvalidDataException>(() => NativeBattlePartyLoadPlan.Create(model, 0, 2, 3, NativeBattlePartyCpuLayout.Classic, [body, body with { Name = "other.dat" }, linked]));
    }

    private static (NativeBattleModelLoader Loader, NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging) Complete(NativeBattleModelLoader loader, NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelDatSource source, int? linkedRow)
    {
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [source], staging);
        NativeBattleModelLoadStep prepared = request.State.Poll507070(source.Bytes.Length, [], request.Staging);
        NativeBattleModelLoadStep loaded = prepared.State.Poll507070(source.Bytes.Length, [], prepared.Staging);
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(loaded.Bind!, cpu.Cpu, cpu.Rows, 0, linkedRow);
        return (loaded.State, (cpu with { Cpu = completed.Cpu, Rows = completed.Rows }).BindAnimation(1, completed.AnimationCopy!), loaded.Staging);
    }
}
