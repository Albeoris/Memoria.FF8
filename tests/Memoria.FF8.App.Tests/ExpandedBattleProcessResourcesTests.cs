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
    public void Installed_battle_entry_rebinds_material_owners_across_roster_changes()
    {
        const ushort formation = 87;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameState bootstrap = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        GameSession session = new(bootstrap with { FieldId = 132, FieldName = "field_132", GlobalRandom = new(917) });
        session.RequestField(new FieldTransition(133, -105, -1751, 22, 128));
        session.SetDepartingFieldState(bootstrap.DepartingFieldState);
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        world.EnsureField();
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        foreach ((int allyCount, int addedEnemies, int expectedEnemies) in new[] { (3, 0, 1), (3, 1, 2), (4, 2, 4), (3, 0, 1) })
        {
            for (int callback = 0; callback < 60 && !world.Events.CanAcceptMovementInput; callback++)
            {
                Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            }
            Assert.True(world.Events.CanAcceptMovementInput);
            session.SetCharacterVitals(vitals.WithBattleParty(Enumerable.Range(0, allyCount).Select(row => checked((byte)row)).ToArray(), allyCount));
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allyCount, AdditionalEnemies = addedEnemies });
            string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
            NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
            session.SetRandomEncounters(new(true, random, new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(battle.NativeEntryReady);
            BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            BattleModelRoster roster = battle.ReadBattlePresentationRoster();
            Assert.Equal(allyCount, roster.Party.Count());
            Assert.Equal(expectedEnemies, roster.Enemies.Count());
            Assert.Equal(roster.Owners.Select(ActorId).Order(), process.Memory.Textures!.PresentationTextures!.Keys.Order());
            Assert.All(roster.Owners, owner => Assert.NotNull(process.Graphics.ActorModels.Get(owner).OriginalResourceRow104));

            ExpandedBattleInstalledEntry.AdmitDebugVictory(world);
            for (int callback = 0; callback < 1200 && !battle.CanReturnToField; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(battle.CanReturnToField);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
        }
    }

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
        Assert.True(entry.Combatants.Count(actor => actor.IsPlayer) > NativeBattleSlots.AllyCount || entry.AddedEnemySources is { Count: > 0 });
        Assert.True(entry.UsesOriginalEnemySlots);

        Assert.Equal(world.BattleResources.LoadEncounter(formation).Flags, entry.NativeEntry!.FormationFlags);
        Assert.NotNull(logical.NativeEntryAdvantage);
        Assert.Equal(5, entry.AddedEnemySources!.Count);
        Assert.Equal(Enumerable.Range(8, 5), entry.AddedEnemySources.Keys);
        Assert.All(entry.Combatants.Where(actor => !actor.IsPlayer), actor =>
        {
            Assert.NotNull(actor.NativeTemplate);
            Assert.Equal(world.BattleResources.ReadEnemyNativeInformation246(actor.ModelId), actor.Information246);
        });

        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(logical.Presentation);
        BattlePresentationResources selected = ReadField<BattlePresentationResources>(process, "resources");
        Assert.Equal(entry.NativeGlobalRandom, selected.InitialGlobalRandom);
        Assert.Equal(entry.NativeCrtRandom, selected.InitialCrtRandom);
        Assert.Equal(entry.NativeStageSource, selected.NativeStageSource);
        Assert.Equal(entry.NativeStageCameraBank, selected.NativeStageCameraBank);
        Assert.Equal(entry.NativeMusicProcess, selected.InitialMusic);
        Assert.Equal(entry.NativeGlobalSoundProcess, selected.InitialSound);
        Assert.Equal(entry.NativeModuleResources, selected.NativeModuleResources);
        Assert.Equal(entry.NativeEffectCatalog, selected.NativeEffectCatalog);
        Assert.Equal(entry.NativeEntryWaveFile, selected.NativeEntryWaveFile);
        Assert.Equal(entry.NativeRetainedMemory!.ColdExecutableSha256, selected.InitialState!.ColdExecutableSha256);
        Assert.Equal(entry.NativeRetainedMemory.EffectMemory!.SelectedIndex, process.Memory.SelectedIndex);
        BattleModelRoster roster = AssertBindings(logical, process, selected);
        Assert.Equal(4, roster.Party.Count());
        Assert.Equal(6, roster.Enemies.Count());
        Assert.Equal(world.BattleResources.ReadNativePcUiIcons().Binding, selected.NativeGraphics!.PcUiIcons);
        Assert.False(process.EntryReady);

        bool observedSparseModelLoad = false;
        NativeExamTimerState timer = session.ExamTimer!;
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
            NativeExamTimerState displayed = Assert.IsType<NativeExamTimerState>(process.Timer);
            Assert.True(displayed.HasSameClock(timer.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1)));
            Assert.True(displayed.HasSameClock(session.ExamTimer!));
            Assert.Same(process.PreparedPresentation, logical.PreparedPresentation);
            timer = displayed;
            BattleActorModels loaded = process.Graphics.ActorModels;
            if (!observedSparseModelLoad && loaded.Roster.Count > 0)
            {
                Assert.True(loaded.Roster.Count < roster.Count);
                NativeBattleActionPacketsState packets = process.ActionPackets;
                Assert.Equal(roster.Owners, packets.Roster.Owners);
                foreach (BattleModelOwner pending in roster.Owners.Where(owner => !loaded.Contains(owner)))
                {
                    Assert.False(packets.ModelAliases.TryEncodeExact(pending, out _));
                }
                observedSparseModelLoad = true;
            }
        }
        Assert.True(observedSparseModelLoad);
        Assert.True(process.EntryReady, $"Entry stalled at lifecycle {process.Graphics.EntryLifecycle}, callback {process.Graphics.Callback}.");
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        Assert.Equal(entry.StageId, process.Stage.ResourceStageId);
        Assert.True(process.GlobalRandomCalls >= 27);
        Assert.All(selected.NativeGraphics!.Bindings, binding =>
        {
            NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(logical.ReadPresentationModelOwner(binding.ActorId));
            Assert.Equal((byte?)binding.ModelId, model.ModelId4);
            Assert.Equal(binding.InitialPosition, model.Transform!.Initial20);
            Assert.NotNull(model.OriginalResourceRow104);
        });
        Assert.Equal(0, logical.Frame);
        Assert.Equal(process.Graphics.Callback, logical.NativeHostCallbacks);
        Assert.Same(process, logical.Presentation);
        Assert.Same(process, world.Battle!.Presentation);
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
        InstalledBattlePresentation.Attach(logical, game, assets, encounter);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(logical.Presentation);
        BattlePresentationResources selected = ReadField<BattlePresentationResources>(process, "resources");
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

    private static BattleModelRoster AssertBindings(BattleSession logical, BattlePresentationProcess process, BattlePresentationResources selected)
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
