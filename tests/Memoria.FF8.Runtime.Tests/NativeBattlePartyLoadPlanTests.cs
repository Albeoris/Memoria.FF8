using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(0, Party(1), NativeBattleModelSlot.Decode(1), 0, 0, layout, [body, linked]);
        Assert.True(plan.Body.CopiedLength < layout.BankLength);
        Assert.True(plan.RequiresResourceStorage);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, cpu.Cpu.Banks[1].Write(0, [11, 22, 33, 44])) } };
        NativeBattleModelCpuBank pinned = cpu.Cpu.Banks[1];
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(0, OriginalOwner(1), NativeBattleModelSlot.Decode(1), 0, 0, 0) with { PartyPlan = plan };
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
        loader = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(loader));
        loader.Validate();
        loader = NativeBattleModelLoader.Queue507080(0x1000, OriginalOwner(1), NativeBattleModelSlot.Decode(1), 0, 0, 0) with { PartyPlan = loader.PartyPlan };
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
        Assert.Throws<NotSupportedException>(() => NativeBattlePartyLoadPlan.Create(0, Party(1), NativeBattleModelSlot.Decode(1), 0, 0, NativeBattlePartyCpuLayout.Classic, [body]));
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(0, Party(1), NativeBattleModelSlot.Decode(1), 0, 0, NativeBattlePartyCpuLayout.Classic, [body, linked]);
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(0, OriginalOwner(1), NativeBattleModelSlot.Decode(1), 0, 0, 0) with { PartyPlan = plan };
        NativeBattleModelDatSource changed = body with { Bytes = body.Bytes.SetItem(body.Bytes.Length - 1, 42) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        Assert.Throws<InvalidDataException>(() => loader.Poll507070(-1, [changed], staging));
        NativeBattleModelLoader old = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(loader with { PartyPlan = null }));
        Assert.Null(old.PartyPlan);
        Assert.DoesNotContain("PartyPlan", NativeBattleTestSnapshots.Json(old));
        Assert.NotNull(old.Poll507070(-1, [changed], staging).FileRequest);
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [body], staging);
        request = request with { State = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(request.State)) };
        Assert.Null(request.State.Poll507070(-1, [], request.Staging).TextureUpload);
        NativeBattleFileStaging replaced = request.Staging.BindResource(NativeBattleFileStaging.Address, changed.Bytes);
        Assert.Throws<InvalidDataException>(() => request.State.Poll507070(body.Bytes.Length, [], replaced));
    }

    [Fact]
    public void Plan_validates_native_selectors_layout_owner_and_live_promotion_barriers()
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, NativeBattlePartyCpuLayout.Classic.BankLength);
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(0, Party(1), NativeBattleModelSlot.Decode(1), 0, 0, NativeBattlePartyCpuLayout.Classic, [body, linked]);
        NativeBattleModelLoadTask task = NativeBattleModelLoader.Queue507080(0, OriginalOwner(1), NativeBattleModelSlot.Decode(1), 0, 0, 0).Task!;
        Assert.Throws<InvalidDataException>(() => plan.Part(task with { OriginalSlot = NativeBattleModelSlot.Decode(0) }));
        Assert.Throws<InvalidDataException>(() => plan.Part(task with { Selector = 1 }));
        NativeBattleGraphicsState loading = OriginalGraphics([(1, Party(1), new(1))]);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().BindPartyOwner(Party(1), NativeBattleModelSlot.Decode(1));
        NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, Party(1), loading);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, Party(1), loading with { ActorModels = loading.ActorModels.Set(Party(1), new(3)) }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, Party(1), loading with { Effects = loading.Effects with { PublishedCount = 1 } }));
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(0, OriginalOwner(1), NativeBattleModelSlot.Decode(1), 0, 0, 0) with { PartyPlan = plan };
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
        BattlePartyActorModel owner = new(new("party"));
        NativeBattleGraphicsState graphics = OriginalGraphics([(1, owner, new(1) { FileSelector128 = 0, FileSelector129 = 0 })]);
        NativeBattleHostedTestFixture.BindProcess(battle, graphics, GraphicsResources([body]));
        NativeBattleHostedTestFixture.SetProperty(battle.Presentation!, nameof(BattlePresentationProcess.NativeModelCpuResources), NativeBattleModelCpuResources.Unknown());
        MethodInfo queue = typeof(BattlePresentationProcess).GetMethod("QueueNativeModelResource507080", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => queue.Invoke(battle.Presentation!, [graphics, (ushort)0, owner, 0u])).InnerException);
        Assert.Null(battle.Presentation!.NativeModelLoader);
        Assert.Null(battle.Presentation?.NativeFileStaging);
        NativeBattleHostedTestFixture.SetField(battle.Presentation!, "resources", new BattlePresentationResources(battle.ReadDefinition(), GraphicsResources([body, linked])));
        queue.Invoke(battle.Presentation!, [graphics, (ushort)0, owner, 0u]);
        NativeBattleModelLoader planned = battle.Presentation!.NativeModelLoader!;
        Assert.True(planned.PartyPlan!.RequiresResourceStorage);
        Assert.Null(battle.Presentation?.NativeFileStaging);
        NativeBattleGraphicsState active = graphics with { Effects = graphics.Effects with { PublishedCount = 1 } };
        MethodInfo poll = typeof(BattlePresentationProcess).GetMethod("PollNativeModelResource507070", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => poll.Invoke(battle.Presentation!, [active])).InnerException);
        Assert.Same(planned, battle.Presentation!.NativeModelLoader);
        Assert.Null(battle.Presentation?.NativeFileStaging);
        NativeBattleHostedTestFixture.SetProperty(battle.Presentation!, nameof(BattlePresentationProcess.NativeEffectIoResult), -1);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeFileIo))!.SetValue(battle.Presentation!, NativeBattleFileIoState.FromColdProcess());
        object?[] arguments = [graphics];
        Assert.Equal(1, poll.Invoke(battle.Presentation!, arguments));
        Assert.Equal((byte)1, battle.Presentation!.NativeModelLoader!.Task!.Phase);
        Assert.NotNull(battle.Presentation?.NativeFileStaging);
    }

    [Theory]
    [InlineData(1, 0x507E20)]
    [InlineData(9, 0x507E20)]
    [InlineData(7, 0)]
    public void Native_special_callbacks_plan_the_selected_copy_extent_and_reject_conflicting_catalogs(byte model, int linkedCallback)
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8) with { ModelId = model, Selector = 2 };
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8) with { ModelId = model, Selector = 3 };
        NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(model, Party(0), NativeBattleModelSlot.Decode(0), 2, 3, NativeBattlePartyCpuLayout.Classic, [body, linked]);
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
        Assert.Throws<InvalidDataException>(() => NativeBattlePartyLoadPlan.Create(model, Party(0), NativeBattleModelSlot.Decode(0), 2, 3, NativeBattlePartyCpuLayout.Classic, [body, body with { Name = "other.dat" }, linked]));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Production_fitting_load_prefers_logical_when_inactive_and_preserves_guest_for_active_old_owner(bool remastered, bool active)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattleModelDatSource linked = NativeBattleModelResourceCapacityTests.Source(true, 8);
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, cpu.Cpu.Banks[1].Write(0, [11, 22, 33, 44])) } };
        NativeBattleModelCpuBank held = cpu.Cpu.Banks[1];
        BattlePartyActorModel owner = new(new("party"));
        NativeBattleGraphicsState graphics = OriginalGraphics([(1, owner, new(active ? (ushort)3 : (ushort)1) { FileSelector128 = 0, FileSelector129 = 0 })]);
        NativeBattleHostedTestFixture.BindProcess(battle, graphics, GraphicsResources([body, linked]));
        NativeBattleHostedTestFixture.SetProperty(battle.Presentation!, nameof(BattlePresentationProcess.NativeModelCpuResources), cpu);
        MethodInfo queue = typeof(BattlePresentationProcess).GetMethod("QueueNativeModelResource507080", BindingFlags.Instance | BindingFlags.NonPublic)!;
        queue.Invoke(battle.Presentation!, [graphics, (ushort)0, owner, 0u]);
        NativeBattleModelLoader loader = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(battle.Presentation!.NativeModelLoader!));
        Assert.False(loader.PartyPlan!.RequiresResourceStorage);
        Assert.Equal(!active, loader.PartyPlan.PreferResourceStorage);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        (loader, cpu, staging) = Complete(loader, cpu, staging, body, null);
        Assert.Equal(!active, cpu.Cpu.Banks[1].HasResourceStorage);
        Assert.Equal(!active, cpu.Rows[0].Require(16).ResourceOnly);
        if (!active)
        {
            Assert.Equal(new byte[] { 11, 22, 33, 44 }, cpu.Cpu.Banks[1].ReadGuest(0, 4));
        }
        Assert.Equal(new byte[] { 11, 22, 33, 44 }, held.Read(0, 4));
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeModelLoader))!.SetValue(battle.Presentation!, loader);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeModelCpuResources))!.SetValue(battle.Presentation!, cpu);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, graphics.ActorModels.Get(owner) with { TextureResourceIndex = 0 }) };
        queue.Invoke(battle.Presentation!, [graphics, (ushort)0x1000, owner, 0u]);
        (loader, cpu, staging) = Complete(battle.Presentation!.NativeModelLoader!, cpu, staging, linked, 1);
        Assert.Equal(!active, cpu.Rows[1].Require(16).ResourceOnly);
        Assert.Null(loader.PartyPlan);
        NativeBattleModelDatSource oversized = NativeBattleModelResourceCapacityTests.Source(true, layout.BankLength);
        NativeBattleHostedTestFixture.SetField(battle.Presentation!, "resources", new BattlePresentationResources(battle.ReadDefinition(), GraphicsResources([body, oversized])));
        if (active)
        {
            NativeBattleModelLoader prior = battle.Presentation!.NativeModelLoader!;
            Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => queue.Invoke(battle.Presentation!, [graphics, (ushort)0, owner, 0u])).InnerException);
            Assert.Same(prior, battle.Presentation!.NativeModelLoader);
            Assert.Null(battle.Presentation?.NativeFileStaging);
        }
        else
        {
            typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeModelCpuResources))!.SetValue(battle.Presentation!, cpu);
            NativeBattleGraphicsState activeLogical = graphics with { ActorModels = graphics.ActorModels.Set(owner, graphics.ActorModels.Get(owner) with { Flags0 = 3 }), Effects = graphics.Effects with { PublishedCount = 1 } };
            queue.Invoke(battle.Presentation!, [activeLogical, (ushort)0, owner, 0u]);
            Assert.True(battle.Presentation!.NativeModelLoader!.PartyPlan!.UseResourceStorage);
            NativeBattleModelCpuResources beforeReload = cpu;
            (loader, cpu, staging) = Complete(battle.Presentation!.NativeModelLoader, cpu, staging, body, null);
            NativeBattleModelResourceCopies.RequirePlannedTransition(beforeReload, owner, activeLogical);
            Assert.True(cpu.Rows[0].Require(16).ResourceOnly);
            Assert.Equal(new byte[] { 11, 22, 33, 44 }, cpu.Cpu.Banks[1].ReadGuest(0, 4));
        }
        cpu.Validate();
    }

    private static NativeBattleGraphicsDefinition GraphicsResources(ImmutableArray<NativeBattleModelDatSource> sources)
    {
        return NativeBattleHostedTestFixture.PrimitiveExecutionGraphics(new(null!, [new("party", 0, new([0, 0]) { ModelLoadSources = sources })], []));
    }

    private static (NativeBattleModelLoader Loader, NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging) Complete(NativeBattleModelLoader loader, NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelDatSource source, int? linkedRow)
    {
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [source], staging);
        NativeBattleModelLoadStep prepared = request.State.Poll507070(source.Bytes.Length, [], request.Staging);
        NativeBattleModelLoadStep loaded = prepared.State.Poll507070(source.Bytes.Length, [], prepared.Staging);
        NativeBattleModelResourceBind bind = loaded.Bind!;
        BattlePartyModelOwner owner = (BattlePartyModelOwner)bind.Task.Owner;
        cpu = cpu.BindPartyOwner(owner, bind.Task.OriginalSlot);
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(bind, cpu.Cpu, cpu.Rows, 0, linkedRow);
        return (loaded.State, (cpu with { Cpu = completed.Cpu, Rows = completed.Rows }).BindAnimation(owner, bind.Task.OriginalSlot, completed.AnimationCopy!), loaded.Staging);
    }
    private static BattlePartyModelOwner Party(int slot) => (BattlePartyModelOwner)OriginalOwner(slot);

}
