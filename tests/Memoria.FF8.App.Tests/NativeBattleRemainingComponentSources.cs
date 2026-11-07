using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRemainingComponentSources
{
    internal static bool Supports(ushort effect) => effect is 9 or 15 or 30 or 57 or 218 or 236 or 237 or 243 or 244 or 245 or 246 or 247 or 250 or 253 or 254 or 255 or 264 or 265 or 266 or 267 or 268 or 269 or 270 or 276 or 308 or 314;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string outputDirectory)
    {
        if (!Supports(effect))
        {
            return null;
        }
        if (Directory.Exists(outputDirectory))
        {
            throw new IOException("Component preparation requires a fresh owned output directory.");
        }
        Directory.CreateDirectory(outputDirectory);
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(gameDirectory);
        BattleSnapshot source = effect switch
        {
            15 => PrepareDraw(game, assets),
            57 => NativeBattleRestorationPresentationTests.PrepareComponent(game, assets, Assert.Single(assets.ActionDefinitions, action => action.NativeAction is { Command: 2, MagicId: 24 }), 57, descriptorEffect: 57).Capture(),
            269 => PrepareAllocation(gameDirectory, outputDirectory),
            308 => PrepareTrio(game, assets, outputDirectory),
            218 => PrepareGuardian(gameDirectory, outputDirectory),
            _ => Export(OriginalRequest(effect), gameDirectory, Path.Combine(outputDirectory, "fixture"), "queued-action104")
        };
        // The final standalone source owns these resources; intermediate setup captures are disposable.
        foreach (string snapshot in Directory.EnumerateFiles(outputDirectory, "*.battle.json", SearchOption.AllDirectories))
        {
            File.Delete(snapshot);
        }
        return source;
    }

    internal static NativeEffectFixtureRequest OriginalRequest(ushort effect)
    {
        (ushort action, int formation) = effect switch
        {
            9 => ((ushort)1, 115),
            30 => ((ushort)23, 463),
            236 => ((ushort)127, 115),
            237 => ((ushort)130, 115),
            243 => ((ushort)147, 164),
            244 => ((ushort)148, 164),
            245 => ((ushort)149, 164),
            246 => ((ushort)150, 164),
            247 => ((ushort)144, 164),
            250 => ((ushort)118, 194),
            253 => ((ushort)283, 794),
            254 => ((ushort)281, 794),
            255 => ((ushort)282, 794),
            264 => ((ushort)277, 372),
            265 => ((ushort)259, 363),
            266 => ((ushort)306, 483),
            267 => ((ushort)241, 410),
            268 => ((ushort)260, 377),
            270 => ((ushort)312, 354),
            276 => ((ushort)145, 9),
            314 => ((ushort)191, 216),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        return new($"component{effect}", effect, effect == 9 ? (byte)2 : (byte)8, action, 0, 1600, 240)
        {
            EncounterId = formation,
            CasterActorIndex = effect switch { 9 => (byte)0, 254 => (byte)5, 255 or 265 => (byte)4, _ => (byte)3 },
            PreserveOriginalFormationEnemies = true,
            ControlledShellEffect = effect == 9 ? (ushort)2 : null,
            ControlledContactValue = effect == 276 ? (ushort)0 : (ushort)7
        };
    }

    private static BattleSnapshot Export(NativeEffectFixtureRequest request, string gameDirectory, string output, string boundary)
    {
        NativeEffectFixtureExportResult result = NativeBattleEffectFixtureExporter.Export(request, gameDirectory, output);
        if (result.Status == "blocked")
        {
            throw new InvalidDataException($"Original component preparation failed at {result.Boundary}: {result.FirstBlocker}");
        }
        return NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, boundary + ".battle.json"));
    }

    private static BattleSnapshot PrepareAllocation(string gameDirectory, string output)
    {
        string directory = Path.Combine(output, "allocation");
        WithEnvironment("MEMORIA_FF8_ALLOCATED269_FIXTURE_OUTPUT", directory, () => new NativeBattleAllocated269FixtureExportTests().Export_original269_queued_component_from_actual_native94_allocation());
        return NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(directory, "queued-action104.battle.json"));
    }

    private static BattleSnapshot PrepareTrio(GameAssets game, BattleAssets assets, string output)
    {
        NativeEffectFixtureRequest request = new("original-trio-entry", 308, 8, 286, 0, 1600, 240) { EncounterId = 794, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 0 };
        BattleSnapshot entry = Export(request, game.GameDirectory, Path.Combine(output, "entry"), "after-entry");
        BattleSnapshot admitted = NativeBattleEffect308PrerequisiteTests.PrepareOriginalTrio(entry, game, assets, Path.Combine(output, "prerequisite"));
        return NativeBattleEffect308PresentationTests.QueueComponent(admitted, game, assets);
    }

    private static BattleSnapshot PrepareGuardian(string gameDirectory, string output)
    {
        NativeEffectFixtureRequest request = new("original-guardian-entry", 218, 236, 330, 3, 1600, 240) { EncounterId = 795, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true };
        string fixture = Path.Combine(output, "entry");
        _ = Export(request, gameDirectory, fixture, "after-entry");
        string original = Path.Combine(output, "original");
        WithEnvironment("MEMORIA_FF8_GUARDIAN_SOURCE_BASELINE", Path.Combine(fixture, "after-entry.battle.json"), () =>
            WithEnvironment("MEMORIA_FF8_GUARDIAN_SOURCE_OUTPUT", original, () =>
                new NativeBattleGuardianOriginalSourcePreparationTests().Original_self_selector_queues_the_selected_component_through_native_admission(218, 2, 68, 330, 53)));
        return NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(original, "effect218-actual-source001", "queued-action104.battle.json"));
    }

    private static void WithEnvironment(string name, string value, Action action)
    {
        string? previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    private static BattleSnapshot PrepareDraw(GameAssets game, BattleAssets assets)
    {
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules casterRules = assets.ReadPlayerNativeStatRules(vitals, 0);
        NativeCharacterVital casterVital = vitals.Records[0];
        BattleNativeCharacterGrowth casterGrowth = new(casterVital.HitPointBonus, casterVital.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics casterStatistics = casterRules.Project(casterVital.MagicInventory, casterGrowth);
        NativeBattleStatusAllocation casterStatus = assets.ReadPlayerNativeStatusData(vitals, 0);
        Assert.Equal(casterRules.ProjectStatusResistances(casterVital.MagicInventory).ToArray(), casterStatus.Resistances.ToArray());
        int casterInitialHp = casterStatistics.MaximumHp;
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = casterVital.Weapon, Level = casterRules.Level, MaximumHp = casterInitialHp, CurrentHp = casterInitialHp, Attack = casterStatistics.Strength, Defense = casterStatistics.Vitality, Magic = casterStatistics.Magic, Spirit = casterStatistics.Spirit, Speed = Math.Max(1, casterStatistics.Speed), Luck = casterStatistics.Luck, Hit = casterStatistics.Hit, Evasion = casterStatistics.Evasion, NativeMagicInventory = casterVital.MagicInventory.ToArray(), ElementDefenses = casterStatistics.ElementDefenses.ToArray(), ElementAttack = casterStatistics.ElementAttack, ElementAttackPercent = casterStatistics.ElementAttackPercent, NativeStatus0 = casterStatus.Saved, NativeStatus1 = casterStatus.Battle, NativeStatusResistances = casterStatus.Resistances.ToArray(), InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, NativePlayerStatRules = casterRules, NativeGrowth = casterGrowth, Abilities = [BattleAbility.Draw] },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, Level = 7, NativeDrawSlots = assets.ReadEnemyDrawSlots(61, 7), SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, MaximumHp = 9999, CurrentHp = 9999, NativeStatusResistances = assets.ReadEnemyNativeStatusData(61).Resistances.ToArray() }
        ];
        // Fresh installed-resource component; the ordinary Draw command produces its own stock result and contact.
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeActions = assets.NativeActions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeFieldBattleRules = 0,
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeSupportRules = assets.ReadNativeSupportRules(),
            NativeBattleSpeed = 0,
            NativeMessageSpeed = 3,
            NativeElapsedCounter = 0,
            NativeSavedBattleFlags = 8,
            NativeSavedBattleFlagsKnownMask = 255,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeBattleUiRandom = new(0),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([15]),
            NativeKnownMagic = game.ReadNewGameKnownMagic(),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeCommandNames = assets.ReadNativeCommandNames(),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeScannedModels = game.ReadNewGameScannedModels(),
            NativeScanAlwaysShow = (game.ReadNewGameInputConfiguration().Options & 0x100) != 0,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };

        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party");
        BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == "enemy");
        NativeBattleDrawSlot slot = enemy.NativeDrawSlots.Where(slot => slot.Id is > 0 and < 64 && caster.NativeMagicSlots.Where(stock => stock.SpellId == slot.Id).Sum(stock => stock.Count) < 100).OrderBy(slot => assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == slot.Id).DrawResist + slot.Resistance).First();
        battle.QueueAction("party", "enemy", BattleAbility.Draw, slot.Id);
        for (int callback = 0; callback < 1200; callback++)
        {
            BattleSnapshot current = battle.Capture();
            if (current.NativeGraphics!.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104) || current.NativeGraphics.OriginalEffect is { Binding.Effect: 15, ReturnedDescriptor: null, Pools.IsEmpty: true, Parameters: not null })
            {
                return current;
            }
            Assert.False(current.NativeGraphics.OriginalEffect is { Binding.Effect: 15, ReturnedDescriptor: > 0 }, "Draw source preparation crossed the module's initialization boundary.");
            battle.Tick();
        }
        throw new InvalidDataException("The actual ordinary Draw command did not publish its pending stock component.");
    }
}
