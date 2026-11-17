using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class InstalledBattlePractice
{
    internal static BattleSession Create(BattleDefinition source, BattleAtbMode mode, GameAssets game, BattleAssets assets, bool useEncounter = false)
    {
        if (source.Combatants.Any(actor => actor.ModelId < 0))
        {
            throw new InvalidDataException("Each practice participant requires an installed model. Select an enemy from the installed catalog.");
        }
        BattleStage stage = assets.LoadStage(source.StageId ?? throw new InvalidDataException("Practice requires an installed stage ID."));
        BattleEncounter? encounter = useEncounter
            ? assets.LoadEncounter(source.EncounterId ?? throw new InvalidDataException("Authored practice placement requires an installed encounter ID."))
            : null;
        if (encounter is not null && encounter.StageId != stage.Id)
        {
            throw new InvalidDataException("The authored practice formation and selected stage must match.");
        }
        NativeInputConfiguration input = game.ReadNewGameInputConfiguration();
        byte speed = source.NativeBattleSpeed ?? input.NativeBattleSpeedByte ?? throw new InvalidDataException("Practice requires the installed battle speed default.");
        NativeCharacterVitals playerDefaults = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        NativeBattleRetainedMemory retained = game.ReadColdProcessNativeBattleMemory();
        NativeBattleMusicProcessState music = source.NativeMusicProcess ?? new(new NativeMusicProcessState(1, false, 0, 1, 0, 0), 1, null);
        BattleDefinition definition = source with
        {
            EncounterId = null,
            UsesOriginalEnemySlots = false,
            AddedEnemySources = null,
            NativeActions = assets.NativeActions.ToArray(),
            NativeBattleSpeed = speed,
            NativeStatusRules = assets.ReadNativeStatusRules(speed),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeEntry = new(0, 0),
            NativeFlags = 0x80,
            NativeRetainedMemory = retained,
            NativeCrtRandom = source.NativeCrtRandom ?? NativeCrtRandomState.FromColdProcess(),
            NativeBattleUiRandom = source.NativeBattleUiRandom ?? NativePlaytimeState.FromColdProcess().CreateBattleUiRandom(),
            NativeGlobalRandom = source.NativeGlobalRandom ?? game.ReadNewGameGlobalRandom(),
            NativeGlobalSoundProcess = source.NativeGlobalSoundProcess ?? NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeMusicProcess = music,
            NativeVibrationProcess = source.NativeVibrationProcess ?? NativeVibrationProcessState.FromColdProcess(),
            NativeInputButtonMap = input.ButtonMap,
            NativeMessageSpeed = input.BattleMessageSpeed,
            NativeSavedOptions = input.Options,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeCommandNames = assets.ReadNativeCommandNames(),
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageSource = stage.NativeSource,
            NativeStageCameraSelectors = [encounter?.PrimaryCamera ?? 0, encounter?.AlternativeCamera ?? 0],
            NativeStageCameraControl = stage.NativeCameraControl,
            NativeRenzokukenResources = game.ReadNativeRenzokukenResources(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalog(),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeVictoryFile = assets.ReadNativeBattleFile(766),
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            Combatants = source.Combatants.Select(BindActor).ToArray()
        };
        BattleSession active = new(definition, mode);
        return InstalledBattlePresentation.Attach(active, game, assets, encounter);

        BattleCombatantDefinition BindActor(BattleCombatantDefinition actor)
        {
            if (actor.IsPlayer)
            {
                int row = Array.FindIndex(playerDefaults.Records, record => record.CharacterId == actor.ModelId);
                if (row < 0)
                {
                    throw new InvalidDataException($"Practice character {actor.ModelId} has no installed player data.");
                }
                int weaponId = actor.WeaponId ?? playerDefaults.GetBattleWeapon(row);
                BattleWeaponDefinition weapon = assets.WeaponDefinitions.SingleOrDefault(item => item.Id == weaponId && item.CharacterId == actor.ModelId)
                    ?? throw new InvalidDataException($"Weapon {weaponId} does not belong to practice character {actor.ModelId}.");
                NativeCharacterVital[] records = playerDefaults.Records.ToArray();
                records[row] = records[row] with { Weapon = checked((byte)weaponId) };
                NativeCharacterVitals vitals = playerDefaults with { Records = records };
                bool currentEquipment = actor.NativeAttack is { } attack && attack.KernelIndex == weaponId && attack.CharacterId == actor.ModelId;
                return actor with
                {
                    WeaponId = weaponId,
                    WeaponName = weapon.Name,
                    NativeAttack = currentEquipment ? actor.NativeAttack : assets.ReadPlayerNativeAttackData(vitals, row),
                    NativePlayerStatRules = currentEquipment && actor.NativePlayerStatRules is not null ? actor.NativePlayerStatRules : assets.ReadPlayerNativeStatRules(vitals, row),
                    NativeLimit = actor.NativeLimit ?? assets.ReadNativeLimitDefinition(actor.ModelId),
                    NativeStatusResistances = actor.NativeStatusResistances.Length == 40 ? actor.NativeStatusResistances : assets.ReadPlayerNativeStatusData(vitals, row).Resistances.ToArray()
                };
            }
            NativeBattleStatusAllocation statuses = assets.ReadEnemyNativeStatusData(actor.ModelId);
            return actor with
            {
                InitialOriginalEnemyOrdinal = null,
                Information246 = assets.ReadEnemyNativeInformation246(actor.ModelId),
                Information254 = assets.ReadEnemyNativeEntryFlags(actor.ModelId),
                NativeAi = actor.NativeAi ?? assets.ReadEnemyAi(actor.ModelId, actor.Level),
                NativeCombatantFlags = actor.NativeCombatantFlags | assets.ReadEnemyNativeCombatantFlags(actor.ModelId),
                NativeStatusResistances = actor.NativeStatusResistances.Length == 40 ? actor.NativeStatusResistances : statuses.Resistances.ToArray()
            };
        }
    }
}
