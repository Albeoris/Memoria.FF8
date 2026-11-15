using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleProcessResourcesTests
{
    [Fact]
    public void Installed_expanded_entry_uses_the_selected_field_handoff_and_reaches_ready()
    {
        const ushort formation = 87;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2, 3], 4));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4, AdditionalEnemies = 4 });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

        BattleSession logical = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition entry = logical.ReadDefinition();
        Assert.True(entry.UsesExpandedRoster);
        Assert.False(entry.UsesOriginalEnemySlots);
        Assert.Null(entry.NativeGraphics);
        Assert.Equal(world.BattleResources.LoadEncounter(formation).Flags, entry.NativeEntry!.FormationFlags);
        Assert.NotNull(logical.NativeEntryAdvantage);
        Assert.Equal(4, entry.AddedEnemySources!.Count);
        Assert.All(entry.Combatants.Where(actor => !actor.IsPlayer), actor =>
        {
            Assert.Null(actor.NativeTemplate);
            Assert.Equal(world.BattleResources.ReadEnemyNativeInformation246(actor.ModelId), actor.Information246);
        });

        Assert.IsType<ExpandedBattlePresentationHost>(logical.ExpandedPresentationHost);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(logical.ExpandedPresentationProcess);
        BattleSession presentation = ReadField<BattleSession>(process, "presentation");
        BattleDefinition selected = presentation.ReadDefinition();
        Assert.Equal(logical.NativeEntryAdvantage, presentation.NativeEntryAdvantage);
        Assert.Equal(entry.NativeGlobalRandom, selected.NativeGlobalRandom);
        Assert.Equal(entry.NativeCrtRandom, selected.NativeCrtRandom);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(entry.NativeExamTimerAtEntry), JsonSerializer.SerializeToElement(selected.NativeExamTimerAtEntry)));
        Assert.Equal(entry.NativeStageSource, selected.NativeStageSource);
        Assert.Equal(entry.NativeStageCameraBank, selected.NativeStageCameraBank);
        Assert.Equal(entry.NativeMusicProcess, selected.NativeMusicProcess);
        Assert.Equal(entry.NativeGlobalSoundProcess, selected.NativeGlobalSoundProcess);
        Assert.Equal(entry.NativeModuleResources, selected.NativeModuleResources);
        Assert.Equal(entry.NativeEffectCatalog, selected.NativeEffectCatalog);
        Assert.Equal(entry.NativeEntryWaveFile, selected.NativeEntryWaveFile);
        Assert.Equal(entry.NativeRetainedMemory!.ColdExecutableSha256, selected.NativeRetainedMemory!.ColdExecutableSha256);
        Assert.Equal(entry.NativeRetainedMemory.EffectMemory!.SelectedIndex, process.Memory.SelectedIndex);
        Assert.Same(ReadField<Func<ushort, NativeBattleEffectBinding>>(logical, "nativeEffectResourceResolver"), ReadField<Func<ushort, NativeBattleEffectBinding>>(presentation, "nativeEffectResourceResolver"));
        BattleModelRoster roster = AssertBindings(logical, process, selected);
        Assert.Equal(4, roster.Party.Count());
        Assert.Equal(5, roster.Enemies.Count());
        Assert.Equal(world.BattleResources.ReadNativePcUiIcons().Binding, selected.NativeGraphics!.PcUiIcons);
        Assert.False(process.EntryReady);

        NativeExamTimerState timer = session.ExamTimer!;
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
            NativeExamTimerState displayed = Assert.IsType<NativeExamTimerState>(process.Timer);
            Assert.True(displayed.HasSameClock(timer.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1)));
            Assert.True(displayed.HasSameClock(session.ExamTimer!));
            Assert.Same(process.PreparedPresentation, logical.PreparedPresentation);
            timer = displayed;
        }
        Assert.True(process.EntryReady, $"Entry stalled at lifecycle {process.Graphics.EntryLifecycle}, callback {process.Graphics.Callback}.");
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        Assert.Equal(entry.StageId, process.Stage.ResourceStageId);
        Assert.True(process.GlobalRandomCalls >= 27);
        Assert.Equal(logical.NativeEntryAdvantage, presentation.NativeEntryAdvantage);
        Assert.All(selected.NativeGraphics!.Bindings, binding =>
        {
            NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(logical.ReadPresentationModelOwner(binding.ActorId));
            Assert.Equal((byte?)binding.ModelId, model.ModelId4);
            Assert.Equal(binding.InitialPosition, model.Transform!.Initial20);
            Assert.NotNull(model.OriginalResourceRow104);
        });
        Assert.Equal(0, logical.Frame);
        Assert.Equal(process.Graphics.Callback, logical.NativeHostCallbacks);
        Assert.Null(logical.NativeGraphics);
        Assert.Same(process, world.Battle!.ExpandedPresentationProcess);
    }

    [Fact]
    public void Sparse_mixed_entry_uses_authored_and_donor_positions_and_excludes_dormant_prototypes()
    {
        const ushort formation = 164;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(GameSession.Load(TestResources.FixturePath("story-battle-164")));
        using GameWorld world = new(session, directory, sharedAssets: game);
        BattleAssets assets = world.BattleResources;
        BattleEncounter encounter = assets.LoadEncounter(formation);
        BattleEnemyPlacement donor = Assert.Single(assets.LoadEncounter(87).Enemies);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources = ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty.Add(8, new(87, donor.Slot, assets.FormationSourceHash(87)));
        BattleDefinition frozen = new()
        {
            UsesExpandedRoster = true,
            UsesOriginalEnemySlots = true,
            AddedEnemySources = sources,
            MagicVitals = session.State.CharacterVitals,
            Combatants = [.. world.CreateBattleParty(), .. encounter.Slots.Take(4).Select(slot => new BattleCombatantDefinition { Id = $"enemy-{slot.Slot}", SceneSlot = slot.Slot, InitialActive = slot.Enabled, NativeAi = assets.ReadEnemyAi(slot.ModelId, 0) }), new() { Id = "enemy-8", SceneSlot = 8, NativeAi = assets.ReadEnemyAi(donor.ModelId, 0) }]
        };
        // Reconstruct the two source kinds without a local random encounter table. The installed field supplies only the music service.
        BattleDefinition entry = world.CreateEncounterBattle(formation, restoredDefinition: frozen);
        world.EnsureField();
        NativeBattleMusicEntryPreparation music = world.Events.PrepareNativeBattleMusicEntry(formation, 0, new NativeMusicCatalog(directory).ReadBattleEntryResources()).Native;
        entry = entry with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess };
        // This fixture isolates entry resources from the authored encounter's AI execution.
        entry = entry with { Combatants = entry.Combatants.Select(actor => actor with { NativeAi = null, NativeTemplate = actor.NativeTemplate is { } template ? template with { NativeDrawTables = null } : null }).ToArray() };
        BattleSession logical = new(entry, BattleAtbMode.TurnBased);
        ExpandedBattlePresentationHost host = new(game, assets);
        ExpandedBattlePresentationProcess process = host.CreateProcess(logical);
        BattleDefinition selected = ReadField<BattleSession>(process, "presentation").ReadDefinition();
        BattleModelRoster roster = AssertBindings(logical, process, selected);
        Assert.DoesNotContain(roster.Enemies, owner => ActorId(owner) is "enemy-2" or "enemy-3");
        Assert.Contains(roster.Enemies, owner => ActorId(owner) == "enemy-8");
        BattleCombatant[] dormant = logical.Combatants.Where(actor => actor.Slot is null).ToArray();
        Assert.NotEmpty(dormant);
        Assert.All(dormant, actor => Assert.DoesNotContain(selected.NativeGraphics!.Bindings, binding => binding.ActorId == actor.Id));
        foreach (BattleCombatant actor in logical.Combatants.Where(actor => !actor.IsPlayer && actor.Slot is not null && actor.SceneSlot is < 8))
        {
            NativeBattleGraphicsBinding original = Assert.Single(selected.NativeGraphics!.Bindings, binding => binding.ActorId == actor.Id);
            Assert.Equal(encounter.Slots[actor.SceneSlot!.Value].NativePosition, original.InitialPosition);
        }
        NativeBattleGraphicsBinding added = Assert.Single(selected.NativeGraphics!.Bindings, binding => binding.ActorId == "enemy-8");
        Assert.Equal(donor.NativeModelId, (int)added.ModelId);
        Assert.Equal(ExpandedBattleFormation.AddedEnemyPosition(8, 1), added.InitialPosition);
        Assert.Equal(assets.ReadEnemyNativeInformation246(donor.ModelId), entry.Combatants.Single(actor => actor.Id == "enemy-8").Information246);
    }

    private static BattleModelRoster AssertBindings(BattleSession logical, ExpandedBattlePresentationProcess process, BattleDefinition selected)
    {
        BattleModelRoster roster = logical.ReadBattlePresentationRoster();
        Assert.All(process.Graphics.ActorModels.Owners, owner => Assert.Contains(owner, roster.Owners));
        NativeBattleGraphicsDefinition graphics = Assert.IsType<NativeBattleGraphicsDefinition>(selected.NativeGraphics);
        Assert.Equal(roster.Owners.Select(ActorId), graphics.Bindings.Select(binding => binding.ActorId));
        Assert.Equal(roster.Enemies.Cast<BattleModelOwner>().Concat(roster.Party).Select(ActorId), graphics.InitialLoadOrder);
        Assert.All(graphics.Bindings, binding =>
        {
            Assert.NotNull(binding.InitialPosition);
            Assert.NotEmpty(binding.Resource.ModelLoadSources!.Value);
        });
        return roster;
    }

    private static string ActorId(BattleModelOwner owner) => owner switch
    {
        BattlePartyActorModel party => party.Actor.ActorId,
        BattleEnemyActorModel enemy => enemy.Actor.ActorId,
        _ => throw new InvalidDataException("An installed encounter must have canonical battle actors.")
    };

    private static T ReadField<T>(object owner, string name) where T : class
    {
        return Assert.IsType<T>(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
    }
}
