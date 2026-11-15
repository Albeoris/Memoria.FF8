using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedPreparedPresentationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly BattleModelOwner Ally = new BattlePartyActorModel(new("ally-3"));
    private static readonly BattleModelOwner Enemy = new BattleEnemyActorModel(new("enemy-4"));

    [Fact]
    public void Child_publishes_current_actor_metadata_with_its_own_palettes_and_graphics_clock()
    {
        var (owner, child, graphics) = Fixture();
        BattleModelRoster roster = graphics.ActorModels.Roster;
        Set(owner, nameof(BattleSession.Frame), 17L);
        BattleCombatant alias = owner.Combatants.Single(actor => actor.Id == "enemy-4");
        alias.SetNativeDisplayName(0);
        Set(child, nameof(BattleSession.NativeGraphics), graphics);
        NativeBattleModelLifecycleServices services = owner.CaptureModelLifecycleServices(roster);
        var before = owner.Combatants.Select(actor => (actor.Id, actor.Name, actor.CurrentHp, actor.Atb)).ToArray();

        child.RefreshHostedPreparedPresentation(owner, services);

        NativeBattlePreparedPresentation first = Assert.IsType<NativeBattlePreparedPresentation>(child.PreparedPresentation);
        Assert.Equal(17, first.Frame);
        Assert.Equal(11, first.Callback);
        Assert.Equal(22, first.StageResourceId);
        Assert.Same(child.NativeEffectMemory!.Textures, first.Textures);
        Assert.Empty(child.Combatants);
        Assert.Null(owner.PreparedPresentation);
        NativeBattlePreparedActor ally = Assert.Single(first.Actors, actor => actor.Id == "ally-3");
        Assert.Equal(("Fourth ally", true, 1, 2, (int?)5, (int?)null), (ally.Name, ally.IsPlayer, ally.ModelId, ally.Costume, ally.WeaponId, ally.SceneSlot));
        Assert.Same(graphics.ActorModels.Get(Ally).BodyTextureSlots, ally.BodyTextures);
        Assert.Same(graphics.ActorModels.Get(Ally).LinkedTextureSlots, ally.LinkedTextures);
        NativeBattlePreparedActor enemy = Assert.Single(first.Actors, actor => actor.Id == alias.Id);
        Assert.Equal(("Known alias", false, 61), (enemy.Name, enemy.IsPlayer, enemy.ModelId));
        Assert.Same(graphics.ActorModels.Get(Enemy).BodyTextureSlots, enemy.BodyTextures);
        Assert.Equal(2, first.Actors.Length);
        Assert.Equal(before, owner.Combatants.Select(actor => (actor.Id, actor.Name, actor.CurrentHp, actor.Atb)).ToArray());

        alias.SetNativeDisplayName(null);
        Set(child, nameof(BattleSession.NativeGraphics), graphics with { Callback = 12 });
        child.RefreshHostedPreparedPresentation(owner, owner.CaptureModelLifecycleServices(roster));

        NativeBattlePreparedPresentation second = Assert.IsType<NativeBattlePreparedPresentation>(child.PreparedPresentation);
        Assert.Equal(first.Frame, second.Frame);
        Assert.Equal(first.Callback + 1, second.Callback);
        Assert.Equal(first.Version + 1, second.Version);
        Assert.Same(first.Primitives, second.Primitives);
        Assert.Equal("Known alias", enemy.Name);
        Assert.Equal("Extra enemy", Assert.Single(second.Actors, actor => actor.Id == alias.Id).Name);
    }

    [Fact]
    public void Arrival_opacity_capture_matches_completed_frame_and_retained_scene_replay()
    {
        var (owner, child, graphics) = Fixture();
        EncounterArrivalState arrival = new(new("ally-3", 0, 0, 3, 0, OpacityFrom: 0.25f), new());
        Field(owner, "encounterArrivals", ImmutableArray.Create(arrival));
        Field(owner, "encounterAllocation", new BattleEncounterAllocationState("batch", "enemy-0", ["enemy-4"], ["target"], BattleEncounterAllocationPhase.Loading, 0));
        graphics = graphics with { Flags = 4 };
        Set(child, nameof(BattleSession.NativeGraphics), graphics);
        NativeBattleModelLifecycleServices services = owner.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        MethodInfo retain = typeof(BattleSession).GetMethod("RetainNativeSceneReplaySource", PrivateInstance)!;
        using IDisposable visible = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        TargetInvocationException missing = Assert.Throws<TargetInvocationException>(() => retain.Invoke(child, [graphics]));
        Assert.IsType<InvalidOperationException>(missing.InnerException);

        Field(child, "hostedFrameServices", services);
        NativeBattleGraphicsState retained = (NativeBattleGraphicsState)retain.Invoke(child, [graphics])!;
        Field(child, "hostedFrameServices", null);
        Set(child, nameof(BattleSession.NativeGraphics), retained);
        child.RefreshHostedPreparedPresentation(owner, services);

        NativeBattlePreparedPrimitiveFrame frame = child.PreparedPresentation!.Primitives;
        NativeBattleSceneReplaySource replay = Assert.Single(frame.SceneReplaySources!).Value;
        Assert.Same(frame.ActorOpacities, replay.Frame.ActorOpacities);
        Assert.Equal(0.25f, frame.ActorOpacity("ally-3"));
        Assert.Equal(0, frame.ActorOpacity("enemy-4"));
        Assert.Equal(1, frame.ActorOpacity("enemy-0"));
    }

    [Fact]
    public void Prepared_scan_uses_canonical_enemy_identity_and_bound_text_for_an_extended_viewer()
    {
        var (owner, child, graphics) = Fixture();
        BattleCombatant enemy = owner.Combatants.Single(actor => actor.Id == "enemy-4");
        enemy.SetNativeDisplayName(0);
        NativeBattleScanProcess scan = NativeBattleScanProcess.FromColdProcess();
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x575540), null, null) { ScanSelectedModel = Enemy };
        (NativeBattleEffectMemory memory, uint extraTextAddress) = NativeBattlePresentationTaskStorage.ReserveByteRange(child.NativeEffectMemory!, effect, graphics.ActorModels.Roster, 24);
        scan = scan with
        {
            Text = scan.Text with { Initialized = true },
            Results = scan.Results with { ExtraTextBindings = [new(extraTextAddress, Enemy, 24)] }
        };
        Set(child, nameof(BattleSession.NativeEffectMemory), memory with { Scan = scan });
        graphics = graphics with { OriginalEffect = effect, UiServices = NativeBattleUiServices.Initialize() };
        Set(child, nameof(BattleSession.NativeGraphics), graphics);

        child.RefreshHostedPreparedPresentation(owner, owner.CaptureModelLifecycleServices(graphics.ActorModels.Roster));

        NativeBattlePreparedScan prepared = Assert.IsType<NativeBattlePreparedScan>(child.PreparedPresentation!.Scan);
        Assert.Equal(new NativeBattlePreparedScanActor(61, "Known alias", false, true), prepared.Actor);
        Assert.Equal((byte)65, prepared.BoundEnemyExtra[extraTextAddress].ReadByte(0));
        Assert.Equal(new[] { 3, 4, 5, 6 }, prepared.EnemyExtra.Keys.Order());
        Assert.Empty(child.Combatants);
        enemy.SetNativeDisplayName(null);
        Assert.Equal("Known alias", prepared.Actor!.Name);
        Assert.Equal((byte)65, prepared.BoundEnemyExtra[extraTextAddress].ReadByte(0));
    }

    [Fact]
    public void Missing_frame_opacities_or_a_changed_logical_roster_preserves_prior_publication()
    {
        var (owner, child, graphics) = Fixture();
        BattleModelRoster roster = graphics.ActorModels.Roster;
        NativeBattleModelLifecycleServices services = owner.CaptureModelLifecycleServices(roster);
        Set(child, nameof(BattleSession.NativeGraphics), graphics);
        child.RefreshHostedPreparedPresentation(owner, services);
        NativeBattlePreparedPresentation? previous = child.PreparedPresentation;

        Assert.Throws<InvalidOperationException>(() => child.RefreshHostedPreparedPresentation(owner, new(services.Facts)));
        Assert.Same(previous, child.PreparedPresentation);
        owner.Combatants.Single(actor => actor.Id == "enemy-4").BindRosterSlot(null, bindNative: false);
        Assert.Throws<InvalidDataException>(() => child.RefreshHostedPreparedPresentation(owner, services));
        Assert.Same(previous, child.PreparedPresentation);
    }

    private static (BattleSession Owner, BattleSession Child, NativeBattleGraphicsState Graphics) Fixture()
    {
        byte[] name = new byte[24];
        name[0] = 65;
        NativeBattleAiAbility[] unusedAbilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], unusedAbilities, 0, []) { DisplayNames = ["Known alias"], NameBuffers = [name] };
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 4).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true }).ToArray();
        allies[3] = allies[3] with { Name = "Fourth ally", ModelId = 1, Costume = 2, WeaponId = 5 };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 5).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy", ModelId = 61 }).ToArray();
        enemies[4] = enemies[4] with { Name = "Extra enemy", NativeAi = ai };
        BattleSession owner = new(new BattleDefinition { UsesExpandedRoster = true, Combatants = [.. allies, .. enemies] }, BattleAtbMode.TurnBased);
        BattleSession child = new(new BattleDefinition { Combatants = [allies[0], enemies[0]] }, BattleAtbMode.TurnBased);
        ((List<BattleCombatant>)typeof(BattleSession).GetField("combatants", PrivateInstance)!.GetValue(child)!).Clear();
        ((Dictionary<string, BattleCombatant>)typeof(BattleSession).GetField("byId", PrivateInstance)!.GetValue(child)!).Clear();
        Field(child, "hostedBattlePresentation", true);
        Set(child, "NativeStage", NativeBattleStageState.FromColdProcess() with { ResourceStageId = 22 });
        Set(child, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() });
        var (roster, aliases) = owner.ReadBattlePresentationPacketBindings();
        NativeBattleModelTextureBinding body = new(new byte?[16].ToImmutableArray().SetItem(0, 1));
        NativeBattleModelTextureBinding linked = new(new byte?[16].ToImmutableArray().SetItem(0, 2));
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, modelOwner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with
        {
            BodyTextureSlots = modelOwner == Ally || modelOwner == Enemy ? body : null,
            LinkedTextureSlots = modelOwner == Ally ? linked : null
        }) with { Callback = 11, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        return (owner, child, graphics);
    }

    private static void Set(BattleSession session, string property, object? value)
    {
        typeof(BattleSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(session, value);
    }

    private static void Field(BattleSession session, string name, object? value)
    {
        typeof(BattleSession).GetField(name, PrivateInstance)!.SetValue(session, value);
    }
}
