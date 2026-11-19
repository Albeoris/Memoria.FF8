using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class InstalledBattlePractice
{
    internal static BattleCombatantDefinition PreparePlayer(BattleCombatantDefinition actor, NativeCharacterVitals nativeDefaults, BattleAssets assets, bool initializeDefaults = false)
    {
        int row = Array.FindIndex(nativeDefaults.Records, record => record.CharacterId == actor.ModelId);
        if (!actor.IsPlayer || row < 0)
        {
            throw new InvalidDataException($"Practice character {actor.ModelId} has no installed player data.");
        }
        NativeCharacterVital native = nativeDefaults.Records[row];
        BattleNativePlayerStatRules rules = actor.NativePlayerStatRules is { } existing && existing.CharacterId == actor.ModelId ? existing : assets.ReadPlayerNativeStatRules(nativeDefaults, row);
        NativeMagicStock[] inventory;
        if (actor.NativeMagicInventory is { } stocks)
        {
            inventory = stocks.ToArray();
        }
        else if (initializeDefaults)
        {
            inventory = nativeDefaults.GetMagicInventory(row).ToArray();
        }
        else
        {
            inventory = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
            if (actor.MagicStocks.Length > inventory.Length)
            {
                throw new InvalidDataException("Practice magic stock exceeds thirty-two native slots.");
            }
            for (int index = 0; index < actor.MagicStocks.Length; index++)
            {
                BattleStock stock = actor.MagicStocks[index];
                inventory[index] = new(checked((byte)stock.Id), checked((byte)stock.Count));
            }
        }
        BattleLimitFamily[]? families = actor.NativeLimitFamilies?.Select(entry => entry.Family).ToArray();
        if (families is null && actor.NativeLimit is { } legacy)
        {
            families = BattleLimitFamilyRules.NativeFamilies(legacy.CharacterId);
        }
        BattleLoadoutOverrides assignments = new()
        {
            Characters = System.Collections.Immutable.ImmutableSortedDictionary<int, BattleCharacterLoadoutOverride>.Empty.Add(actor.ModelId, new() { WeaponId = actor.WeaponId, LimitFamilies = families })
        };
        BattleResolvedLoadout resolved = assets.ResolveBattleLoadout(new() { BattleLoadouts = assignments }, actor.ModelId, nativeDefaults.GetBattleWeapon(row));
        BattleCombatantDefinition initialized = actor with
        {
            Level = actor.NativePlayerStatRules is null ? rules.Level : actor.Level,
            MaximumHp = initializeDefaults ? nativeDefaults.GetMagicMaximumHitPoints(row) : actor.MaximumHp,
            CurrentHp = initializeDefaults ? native.HitPoints.Minimum : actor.CurrentHp,
            NativePlayerStatRules = rules,
            NativeGrowth = actor.NativeGrowth ?? native.Growth,
            NativeMagicInventory = inventory,
            NativeAttack = actor.NativeAttack is { } attack && attack.CharacterId == actor.ModelId ? attack : assets.ReadPlayerNativeAttackData(nativeDefaults, row),
            NativeLimit = actor.NativeLimit ?? assets.ReadNativeLimitDefinition(actor.ModelId),
            NativeLimitFamilies = actor.NativeLimitFamilies ?? resolved.Families,
            NativeCharacterFlag100 = actor.NativeCharacterFlag100 ?? assets.ReadCharacterNativeConditionFlag100(actor.ModelId),
            NativeDeathCount = actor.NativeDeathCount ?? native.DeathCount,
            NativeStatusResistances = actor.NativeStatusResistances.Length == 40 ? actor.NativeStatusResistances.ToArray() : assets.ReadPlayerNativeStatusData(nativeDefaults, row).Resistances.ToArray()
        };
        BattleCombatantDefinition equipped = assets.ApplyBattleWeapon(initialized, resolved.Weapon.Id);
        BattleNativePlayerStatistics statistics = equipped.NativePlayerStatRules!.Project(equipped.NativeMagicInventory!, equipped.NativeGrowth!);
        return equipped with { MaximumHp = statistics.MaximumHp, CurrentHp = Math.Clamp(equipped.CurrentHp, 0, statistics.MaximumHp) };
    }

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
        NativeSavedBattleFlags savedFlags = game.ReadNewGameSavedBattleFlags();
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
            LimitProgress = source.LimitProgress ?? game.ReadNewGameLimitProgress(),
            NativeShotAvailability = source.NativeShotAvailability ?? game.ReadNewGameShotAvailability(),
            NativeSavedBattleFlags = source.NativeSavedBattleFlags ?? savedFlags.Value,
            NativeSavedBattleFlagsKnownMask = source.NativeSavedBattleFlags.HasValue ? source.NativeSavedBattleFlagsKnownMask : savedFlags.EffectiveKnownMask,
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
                return PreparePlayer(actor, playerDefaults, assets);
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
