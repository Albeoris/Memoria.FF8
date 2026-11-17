using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleTestDefinitions
{
    internal static BattleDefinition CreateTargetGuardianDefinition(GameAssets game, BattleAssets assets, int guardian, ushort effectId, out NativeBattleGraphicsDefinition graphicsResources)
    {
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition action = assets.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == guardian) with { Level = 10, Available = true, NativeGuardianBoostAvailable = false };
        Assert.Equal(effectId, action.NativeAction!.MagicId);
        Assert.Equal((byte)254, action.NativeAction.Command);
        Assert.Equal((ushort)(64 + guardian), action.NativeAction.ActionId);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeBattleStatusAllocation partyStatus = assets.ReadPlayerNativeStatusData(vitals, 0);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), NativeStatusResistances = partyStatus.Resistances.ToArray(), GuardianForceCompatibility = new ushort[16], EquipmentIds = [guardian], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce] },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, MaximumHp = 9999, CurrentHp = 9999, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(61), NativeStatusResistances = assets.ReadEnemyNativeStatusData(61).Resistances.ToArray() }
        ];
        // Explicit independent installed-resource component: the ordinary command owns charge, headers, formula and contact production.
        graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation);
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            GuardianForces = [new(guardian, 1000, 1000)],
            ActionDefinitions = [action],
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
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([effectId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            LimitProgress = game.ReadNewGameLimitProgress(),
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        return NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
    }

    internal static BattleDefinition CreateGuestLimitDefinition(GameAssets game, BattleAssets assets, int characterId, ushort effectId, out NativeBattleGraphicsDefinition graphicsResources)
    {
        BattleDefinition original = CreateTargetGuardianDefinition(game, assets, 0, 116, out _);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        int row = characterId < 8 ? characterId : 0;
        if (characterId >= 8)
        {
            NativeCharacterVital[] records = vitals.Records.ToArray();
            records[row] = records[row] with { CharacterId = checked((byte)characterId) };
            vitals = vitals with { DreamEnabled = true, Records = records };
        }
        NativeCharacterVital character = vitals.Records[row];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, row);
        BattleNativeCharacterGrowth growth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics stats = rules.Project(character.MagicInventory, growth);
        int weapon = characterId >= 8 ? vitals.DreamWeapons[characterId - 8] : character.Weapon;
        BattleCombatantDefinition actor = original.Combatants[0] with { Name = "Guest", ModelId = characterId, WeaponId = weapon, EquipmentIds = [], Abilities = [BattleAbility.Attack, BattleAbility.LimitBreak], Level = rules.Level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = stats.Speed, Luck = stats.Luck, Hit = stats.Hit, Evasion = stats.Evasion, ElementDefenses = stats.ElementDefenses.ToArray(), ElementAttack = stats.ElementAttack, ElementAttackPercent = stats.ElementAttackPercent, NativeMagicInventory = character.MagicInventory.ToArray(), NativeLimit = assets.ReadNativeLimitDefinition(characterId), NativeAttack = assets.ReadPlayerNativeAttackData(vitals, row), NativePlayerStatRules = rules, NativeStatusResistances = rules.ProjectStatusResistances(character.MagicInventory).ToArray(), NativeDeathCount = 0, NativeCharacterFlag100 = (character.MemberFlags & 0x100) != 0, NativeGrowth = growth };
        BattleCombatantDefinition[] actors = [actor, original.Combatants[1] with { Information246 = assets.ReadEnemyNativeInformation246(61) }];
        graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94));
        return original with { Combatants = actors, GuardianForces = [], ActionDefinitions = [], NativeLimitsDisabled = false, LimitProgress = game.ReadNewGameLimitProgress(), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([effectId]) };
    }
}
