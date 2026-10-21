using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleAtbMode
{
    Normal,
    TurnBased
}

public enum BattleGunbladeMode
{
    Manual,
    Automatic
}

public enum BattleOutcome
{
    Active,
    Victory,
    Defeat,
    ScriptedExit
}

public enum BattleAbility
{
    Attack,
    Magic,
    Draw,
    GuardianForce,
    Item,
    EnemyAbility,
    NativeCommand,
    LimitBreak
}

public enum BattleActionEffect
{
    Damage,
    Heal,
    Revive,
    FullHeal,
    Scan,
    Status,
    RemoveStatus,
    Native
}

public sealed record BattleActionDefinition(BattleAbility Ability, int Id, string Name, BattleActionEffect Effect, int Power, bool Allies, bool AllTargets = false, BattleStatusKind[]? Statuses = null, int DrawResist = 0, int Element = 0, int LevelPowerCoefficient = 0, int ConstantPower = 0, int Level = 1, int DamagePercent = 0, bool Available = true, string? DisplayName = null)
{
    public byte? NativeAttackFlags { get; init; }
    public BattleNativeAction? NativeAction { get; init; }
    public bool? NativeGuardianBoostAvailable { get; init; }
}
public sealed record BattleStock(int Id, int Count);
public sealed record BattleDrawSpell(int Id, int Resistance);

public enum BattleStatusKind
{
    Poison,
    Regen,
    Haste,
    Slow,
    Protect,
    Shell,
    Blind,
    Silence,
    Sleep
}

public sealed record BattleStatusEffectDefinition
{
    public BattleStatusKind Kind { get; init; }
    public int DurationFrames { get; init; } = 300;
    public int TickIntervalFrames { get; init; } = 30;
    public int Magnitude { get; init; } = 10;
    public bool Permanent { get; init; }
}

public sealed record BattleCombatantDefinition
{
    public int? MagicRecordId { get; init; }
    public byte? Information246 { get; init; }
    public BattleNativeEnemySpecial? NativeSpecial { get; init; }
    public BattleNativeCharacterGrowth? NativeGrowth { get; init; }
    public ushort[]? GuardianForceCompatibility { get; init; }
    public int? SceneSlot { get; init; }
    public bool InitialActive { get; init; } = true;
    public bool InitialHidden { get; init; }
    public bool InitialUntargetable { get; init; }
    public bool InitialSuspended { get; init; }
    public uint NativeCombatantFlags { get; init; }
    public NativeBattleAiDefinition? NativeAi { get; init; }
    public NativeBattleEnemyTemplate? NativeTemplate { get; init; }
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsPlayer { get; init; }
    public int ModelId { get; init; }
    public int Costume { get; init; }
    public int MaximumHp { get; init; } = 100;
    public int CurrentHp { get; init; } = 100;
    public int Speed { get; init; } = 100;
    public int Attack { get; init; } = 20;
    public int Defense { get; init; } = 5;
    public int AbilityPoints { get; init; }
    public int? RewardSlot { get; init; }
    public BattleEnemyRewardDefinition? Rewards { get; init; }
    public int Level { get; init; } = 1;
    public int Magic { get; init; } = 20;
    public int Spirit { get; init; } = 5;
    public int Evasion { get; init; }
    public int Luck { get; init; }
    public ushort NativeKillCount { get; init; }
    public int Hit { get; init; } = 255;
    public bool? NativeCharacterFlag100 { get; init; }
    public int PhysicalPower { get; init; } = 20;
    public int DrainPercent { get; init; }
    public int ElementAttack { get; init; }
    public int ElementAttackPercent { get; init; }
    public int[] ElementDefenses { get; init; } = [800, 800, 800, 800, 800, 800, 800, 800];
    public BattleStock[] MagicStocks { get; init; } = [];
    public NativeMagicStock[]? NativeMagicInventory { get; init; }
    public NativeBattleDrawSlot[]? NativeDrawSlots { get; init; }
    public BattleDrawSpell[] DrawSpells { get; init; } = [];
    public NativeBattleMugDefinition? NativeMug { get; init; }
    public int[] DrawGuardianForces { get; init; } = [];
    public int InitialAtb { get; init; }
    public int? WeaponId { get; init; }
    public string? WeaponName { get; init; }
    public int[] EquipmentIds { get; init; } = [];
    public BattleAbility[] Abilities { get; init; } = [BattleAbility.Attack];
    public ImmutableArray<BattleNativeCommandBinding> NativeCommands { get; init; } = [];
    public ImmutableArray<byte> NativeCommandSlots { get; init; } = [];
    public BattleNativeLimitDefinition? NativeLimit { get; init; }
    public ImmutableArray<ushort> NativeRenzokukenMagicIds { get; init; } = [];
    public BattleNativeAction? NativeAttack { get; init; }
    public BattleNativePlayerStatRules? NativePlayerStatRules { get; init; }
    public BattleStatusEffectDefinition[] StatusEffects { get; init; } = [];
    public ushort NativeRetainedStatusBits { get; init; }
    public ushort NativeStatus0 { get; init; }
    public ushort? NativeDeathCount { get; init; }
    public uint NativeStatus1 { get; init; }
    public byte[] NativeStatusResistances { get; init; } = [];
}

public sealed record BattleDefinition
{
    public NativeCharacterVitals? MagicVitals { get; init; }
    public NativeBattleClockRules? NativeClockRules { get; init; }
    public NativeBattleSupportRules? NativeSupportRules { get; init; }
    public NativeBattleRetainedMemory? NativeRetainedMemory { get; init; }
    public NativeVibrationProcessState? NativeVibrationProcess { get; init; }
    public NativeGlobalSoundProcess? NativeGlobalSoundProcess { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicProcessState? NativeMusicProcess { get; init; }
    public NativeBattleGraphicsDefinition? NativeGraphics { get; init; }
    public ImmutableArray<NativeBattleModuleResource> NativeModuleResources { get; init; } = [];
    public ImmutableArray<NativeBattleDefensiveResource> NativeDefensiveResources { get; init; } = [];
    public ImmutableArray<NativeBattleStaticMeshResource> NativeEffectMeshes { get; init; } = [];
    public ImmutableArray<NativeBattleEffectBinding> NativeEffectCatalog { get; init; } = [];
    public NativeBattleEffectInitializerAsset? NativeCommonEffectTexture { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleArchiveFile? NativeEntryWaveFile { get; init; }
    public NativeBattleArchiveFile? NativeVictoryFile { get; init; }
    public NativeBattleActionCameraRules? NativeActionCameraRules { get; init; }
    public NativeBattleCameraBank? NativeStageCameraBank { get; init; }
    public NativeBattleStageSource? NativeStageSource { get; init; }
    public ImmutableArray<byte> NativeStageCameraSelectors { get; init; } = [];
    public ImmutableArray<byte> NativeStageCameraControl { get; init; } = [];
    public NativeBattleEntryInput? NativeEntry { get; init; }
    public NativeGlobalRandomState? NativeGlobalRandom { get; init; }
    public NativeKnownMagic? NativeKnownMagic { get; init; }
    public NativeScannedModels? NativeScannedModels { get; init; }
    public bool? NativeScanAlwaysShow { get; init; }
    public NativeCardInventory? NativeCards { get; init; }
    public NativeCrtRandomState? NativeCrtRandom { get; init; }
    public NativeBattleUiRandomState? NativeBattleUiRandom { get; init; }
    public uint? NativeElapsedCounter { get; init; }
    public BattleNativeDevourEffect[] NativeDevourEffects { get; init; } = [];
    public string?[] NativeCardNames { get; init; } = [];
    public string?[] NativeItemNames { get; init; } = [];
    public byte? NativeSavedBattleFlags { get; init; }
    public byte NativeSavedBattleFlagsKnownMask { get; init; }
    public bool? NativeExamTimerZero { get; init; }
    public NativeExamTimerState? NativeExamTimerAtEntry { get; init; }
    public NativeBattleStatusRules? NativeStatusRules { get; init; }
    public byte? NativeMessageSpeed { get; init; }
    public string?[] NativeGlobalMessages { get; init; } = [];
    public string[] NativeCommandNames { get; init; } = [];
    public uint[]? PersistentAiVariables { get; init; }
    public string Name { get; init; } = "Debug battle";
    public int? StageId { get; init; }
    public int? EncounterId { get; init; }
    public BattleCombatantDefinition[] Combatants { get; init; } = [];
    public BattleActionDefinition[] ActionDefinitions { get; init; } = [];
    public BattleNativeAction[] NativeActions { get; init; } = [];
    public BattleGuardianForceVital[] GuardianForces { get; init; } = [];
    public byte? NativeBattleSpeed { get; init; }
    public uint? NativeSavedOptions { get; init; }
    public Dictionary<byte, byte[]> NativeScanNames { get; init; } = [];
    public Dictionary<string, byte[]> NativeScanActorNames { get; init; } = [];
    public ImmutableArray<byte> NativeInputButtonMap { get; init; } = [];
    public NativePocketStationState? NativePocketStation { get; init; }
    public string? NativeChocoboName { get; init; }
    public NativeLimitProgress? LimitProgress { get; init; }
    public ImmutableArray<BattleNativeRenzokukenResource> NativeRenzokukenResources { get; init; } = [];
    public ImmutableArray<byte> NativeCommandSequences { get; init; } = [];
    public NativeShotAvailabilityState? NativeShotAvailability { get; init; }
    public bool? NativeLimitsDisabled { get; init; }
    public byte? NativeFieldBattleRules { get; init; }
    public BattleStock[] Items { get; init; } = [];
    public NativeItemStock[]? NativeItemInventory { get; init; }
    public byte NativeSeed { get; init; }
    public ushort NativeFlags { get; init; }
    public ushort? NativeFieldBattleMode { get; init; }
    public NativeBattleRandomState? InitialRandom { get; init; }
    public bool RareItem { get; init; }
}

public enum BattleActionPhase
{
    Windup,
    Recovery
}

public sealed record BattleAttackCommand(string ActorId, string TargetId, BattleAbility Ability = BattleAbility.Attack, int ActionId = 0, bool DrawCast = false, int NativeTargetSelector = 0)
{
    public string? MagicReservationId { get; init; }
    public BattleSmartAttackPreview? SmartAttack { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int? NativeHeaderIndex { get; init; }
    public BattleNativeCallback? NativeCallback { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public byte? NativeAiCallbackStage { get; init; }
    public BattleNativeRenzokukenInvocation? NativeRenzokuken { get; init; }
    public BattleNativeDuelInvocation? NativeDuel { get; init; }
    public BattleNativeSlotsRoll? NativeSlots { get; init; }
    public ImmutableArray<ushort> NativeMagicTargets { get; init; } = [];
    public BattleNativeDrawContext? NativeDraw { get; init; }
    public string? DrawCastTargetId { get; init; }
    public BattleNativePlayerReaction? NativePlayerReaction { get; init; }
    public NativeLimitCallback? NativeLimitCallback { get; init; }
    public BattleNativeGraphicCommand? NativeGraphic { get; init; }
    public BattleIndependentGuardianInvocation? IndependentGuardian { get; init; }
    public byte? NativeQueuedCommand { get; init; }
    public bool FreeNativeMagic { get; init; }
    public int? ReservedNativeItemSlot { get; init; }
    public BattleNativeSpecialContext? NativeSpecial { get; init; }
    public BattleNativeAction? NativeAction { get; init; }
    public byte? NativeSequence { get; init; }
    public ushort NativeTargetMask { get; init; }
}
public sealed record BattleActionPresentation(string ActorId, string TargetId, BattleActionPhase Phase, int RemainingFrames, BattleAbility Ability = BattleAbility.Attack, int ActionId = 0, bool DrawCast = false, int NativeTargetSelector = 0)
{
    public string? MagicReservationId { get; init; }
    public BattleSmartAttackPreview? SmartAttack { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int? NativeHeaderIndex { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public byte? NativeAiCallbackStage { get; init; }
    public BattleNativeRenzokukenInvocation? NativeRenzokuken { get; init; }
    public BattleNativeDuelInvocation? NativeDuel { get; init; }
    public BattleNativeSlotsRoll? NativeSlots { get; init; }
    public ImmutableArray<ushort> NativeMagicTargets { get; init; } = [];
    public BattleNativeDrawContext? NativeDraw { get; init; }
    public string? DrawCastTargetId { get; init; }
    public BattleNativePlayerReaction? NativePlayerReaction { get; init; }
    public NativeLimitCallback? NativeLimitCallback { get; init; }
    public int? NativeSequenceFrame { get; init; }
    public BattleNativeGraphicCommand? NativeGraphic { get; init; }
    public BattleIndependentGuardianInvocation? IndependentGuardian { get; init; }
    public byte? NativeQueuedCommand { get; init; }
    public bool FreeNativeMagic { get; init; }
    public int? ReservedNativeItemSlot { get; init; }
    public BattleNativeSpecialContext? NativeSpecial { get; init; }
    public short? GunbladeTriggerTiming { get; init; }
    public BattleNativeAction? NativeAction { get; init; }
    public byte? NativeSequence { get; init; }
    public ushort NativeTargetMask { get; init; }
}
public sealed record BattleEvent(long Frame, string Kind, string? ActorId, string? TargetId, int Amount, string Message, BattleAbility Ability = BattleAbility.Attack)
{
    public NativeBattleSequenceSound? NativeSound { get; init; }
    public NativeBattleModuleSound? NativeModuleSound { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? NativeUiSound { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicCommandReceipt? NativeMusicCommand { get; init; }
    public bool NativeStopAllSounds { get; init; }
    public byte? NativeGraphicMagicId { get; init; }
    public byte? NativeSequence { get; init; }
    public int? NativeMessageMode { get; init; }
    public int NativeMessageDelayFrames { get; init; }
}
public sealed record BattleStatusSnapshot(BattleStatusKind Kind, int RemainingFrames, int UntilTickFrames, BattleStatusEffectDefinition? Definition = null);
public sealed record BattleCombatantSnapshot(string Id, int CurrentHp, int Atb, BattleStatusSnapshot[] StatusEffects)
{
    public NativeBattleAtbState? NativeAtb { get; init; }
    public ushort[]? GuardianForceCompatibility { get; init; }
    public NativeBattleStatusSnapshot? NativeStatus { get; init; }
    public BattleStock[] MagicStocks { get; init; } = [];
    public NativeMagicStock[]? NativeMagicInventory { get; init; }
    public NativeBattleDrawSlot[]? NativeDrawSlots { get; init; }
    public NativeBattleParticipantSnapshot? NativeParticipant { get; init; }
}

public sealed record BattleSnapshot
{
    public NativeCharacterVitals? MagicVitals { get; init; }
    public NativeExamTimerState? NativeExamTimer { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? NativeHostCallbacks { get; init; }
    public NativeGlobalSoundProcess? NativeGlobalSoundProcess { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicProcessState? NativeMusicProcess { get; init; }
    public NativeBattleFileIoState? NativeFileIo { get; init; }
    public NativeVibrationProcessState? NativeVibrationProcess { get; init; }
    public NativeBattleModelTextureCache? NativeModelTextureCache { get; init; }
    public NativeBattleModelLoader? NativeModelLoader { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFileStaging? NativeFileStaging { get; init; }
    public NativeBattleModelCpuResources? NativeModelCpuResources { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleGuardianBoost? NativeGuardianBoost { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleCommandBanks? NativeCommandBanks { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int? NativeEffectIoResult { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleEffectMemory? NativeEffectMemory { get; init; }
    public NativeBattleStageState? NativeStage { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleActionMemory? NativeActionMemory { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattlePartyPresentation? NativePartyPresentation { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattlePartyStatusMirrors? NativePartyStatusMirrors { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleActionPacketsState? NativeActionPackets { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleActionScratch? NativeActionScratch { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public System.Collections.Immutable.ImmutableArray<byte?> NativeElementAttack { get; init; } = [null, null, null, null, null, null, null];
    [System.Text.Json.Serialization.JsonRequired]
    public byte? NativeReactionOrder { get; init; }
    public NativeBattleClockState? NativeClock { get; init; }
    public NativeBattleSupportState? NativeSupport { get; init; }
    public NativeBattleGraphicsState? NativeGraphics { get; init; }
    public NativeBattleTimedAction? NativeTimedAction { get; init; }
    public NativeBattleCameraState? NativeCamera { get; init; }
    public byte? NativeEntryAdvantage { get; init; }
    public NativeGlobalRandomState? NativeGlobalRandom { get; init; }
    public ulong NativeGlobalRandomCalls { get; init; }
    public NativeKnownMagic? NativeKnownMagic { get; init; }
    public NativeScannedModels? NativeScannedModels { get; init; }
    public NativeCardInventory? NativeCards { get; init; }
    public NativeCrtRandomState? NativeCrtRandom { get; init; }
    public NativeBattleUiRandomState? NativeBattleUiRandom { get; init; }
    public int Version { get; init; } = 1;
    public BattleDefinition Definition { get; init; } = new();
    public BattleAtbMode Mode { get; init; }
    public BattleGunbladeMode GunbladeMode { get; init; }
    public long Frame { get; init; }
    public BattleOutcome Outcome { get; init; }
    public BattleOutcome? DebugOutcome { get; init; }
    public BattleCombatantSnapshot[] Combatants { get; init; } = [];
    public NativeBattleEnemyAllocation[] NativeAllocations { get; init; } = [];
    public BattleAttackCommand[] QueuedActions { get; init; } = [];
    public BattleActionPresentation? ActiveAction { get; init; }
    public BattleEvent[] Events { get; init; } = [];
    public BattleStock[] Items { get; init; } = [];
    public NativeItemStock[]? NativeItemInventory { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public BattleEnemyDefeat[] EnemyDefeats { get; init; } = [];

    public NativeBattleRandomState? Random { get; init; }
    public uint[]? NativeAiVariables { get; init; }
    public uint[]? PersistentAiVariables { get; init; }
    public BattleStock[] ScriptedRewards { get; init; } = [];
    public BattleStock[]? NativeRewardItems { get; init; }
    public int NativeLastAllocatedIndex { get; init; } = -1;
    public NativeBattleScriptState NativeScriptState { get; init; } = new();
    public BattleNativeLimitState[] NativeLimits { get; init; } = [];
    public BattleGuardianForceVital[] GuardianForces { get; init; } = [];
    public BattleGuardianForceCast[] GuardianForceCasts { get; init; } = [];
    public BattleStatusKind[] CustomHandlers { get; init; } = [];
}

public sealed partial class BattleCombatant
{
    internal ushort[]? MutableGuardianForceCompatibility { get; set; }
    internal IReadOnlyList<ushort>? GuardianForceCompatibility => MutableGuardianForceCompatibility;
    private uint nativeCombatantFlags;
    private int currentHp;
    private BattleCombatantDefinition definition;
    internal BattleCombatantDefinition SourceDefinition => definition;
    internal readonly List<BattleStatusState> Effects;
    internal ushort NativeStatus0 { get; set; }
    internal uint NativeStatus1 { get; set; }
    internal short[] NativeStatusTimers { get; } = Enumerable.Repeat((short)-1111, 14).ToArray();
    internal byte[] MutableNativeStatusResistances { get; private set; }

    internal BattleCombatant(BattleCombatantDefinition definition)
    {
        MutableGuardianForceCompatibility = definition.GuardianForceCompatibility?.ToArray();
        this.definition = definition;
        NativePendingReaction = definition.InitialActive ? new(0, 0) : new(null, null);
        MutableNativeStatusResistances = InitialNativeStatusResistances();
        NativeGrowth = definition.NativeGrowth;
        nativeCombatantFlags = definition.NativeCombatantFlags | (definition.NativeTemplate?.NativeCombatantFlags ?? 0);
        if (definition.IsPlayer)
        {
            //48B5F0 initializes players to8801; lifecycle assignment below owns the active bit.
            nativeCombatantFlags |= 0x8800;
        }
        CurrentHp = definition.CurrentHp;
        NativeKillCount = definition.NativeKillCount;
        NativeDeathCount = definition.NativeDeathCount;
        Atb = CurrentHp == 0 ? 0 : definition.InitialAtb;
        Effects = definition.StatusEffects.Select(effect => new BattleStatusState(effect, effect.DurationFrames, effect.TickIntervalFrames)).ToList();
        NativeStatus0 = (ushort)((definition.NativeTemplate?.NativeStatusAllocation?.Saved ?? definition.NativeStatus0) | definition.NativeRetainedStatusBits);
        RefreshNativeHpStatusFlags();
        NativeStatus1 = definition.NativeTemplate?.NativeStatusAllocation?.Battle ?? definition.NativeStatus1;
        if (definition.NativeCharacterFlag100 == true)
        {
            nativeCombatantFlags |= 0x100;
        }
        RestoreMagicInventory(definition.NativeMagicInventory ?? ExpandMagicStocks(definition.MagicStocks));
        RestoreDrawSlots(definition.NativeDrawSlots ?? ExpandDrawSlots(definition.DrawSpells, definition.DrawGuardianForces));
        IsActive = definition.InitialActive;
        Hidden = definition.InitialHidden;
        Untargetable = definition.InitialUntargetable;
        Suspended = definition.InitialSuspended;
        ResolvedNativeLevel = definition.NativeTemplate is not null && definition.InitialActive ? definition.Level : null;
        RefreshNativeAtbSpeed();
    }

    public string Id => definition.Id;
    public string Name => NativeDisplayNameIndex is int index ? definition.NativeAi!.DisplayNames[index] : definition.Name;
    public bool IsPlayer => definition.IsPlayer;
    public int ModelId => definition.ModelId;
    public int Costume => definition.Costume;
    public ImmutableArray<BattleNativeCommandBinding> NativeCommands => definition.NativeCommands;
    public ImmutableArray<byte> NativeCommandSlots => definition.NativeCommandSlots;
    public BattleNativeLimitDefinition? NativeLimit => definition.NativeLimit;
    public ImmutableArray<ushort> NativeRenzokukenMagicIds => definition.NativeRenzokukenMagicIds;
    public BattleNativeAction? NativeAttack => definition.NativeAttack;
    public int? SceneSlot => definition.SceneSlot;
    public uint NativeCombatantFlags
    {
        get => nativeCombatantFlags;
        internal set => nativeCombatantFlags = value;
    }
    public bool IsActive
    {
        get => (nativeCombatantFlags & 1) != 0;
        internal set => SetNativeCombatantFlag(1, value);
    }
    public bool Hidden
    {
        get => (nativeCombatantFlags & 2) != 0;
        internal set => SetNativeCombatantFlag(2, value);
    }
    public bool Untargetable
    {
        get => (nativeCombatantFlags & 0x40) != 0;
        internal set => SetNativeCombatantFlag(0x40, value);
    }
    public bool Suspended
    {
        get => (nativeCombatantFlags & 0x80) != 0;
        internal set => SetNativeCombatantFlag(0x80, value);
    }

    private void SetNativeCombatantFlag(uint mask, bool enabled)
    {
        nativeCombatantFlags = enabled ? nativeCombatantFlags | mask : nativeCombatantFlags & ~mask;
    }
    internal NativeBattleAiDefinition? NativeAi => definition.NativeAi;
    internal uint[] AiVariables { get; } = new uint[8];
    internal byte AiTurnCount { get; set; }
    internal string? LastAttackerId { get; set; }
    internal NativeBattleActionMemoryOwner? NativeActionMemoryOwner { get; set; }
    internal byte? LastNativeHealing
    {
        get => NativeActionMemoryOwner?.State.Healing(NativeIndex);
        set
        {
            NativeBattleActionMemoryOwner owner = NativeActionMemoryOwner ?? throw new InvalidOperationException("Native healing memory has no bound physical bank.");
            owner.State = owner.State.WithHealing(NativeIndex, value);
        }
    }
    internal bool NativeAllocationPendingStatistics { get; set; }
    internal NativeBattleLastAction? LastNativeAction
    {
        get => NativeActionMemoryOwner?.State.Action(NativeIndex);
        set
        {
            NativeBattleActionMemoryOwner owner = NativeActionMemoryOwner ?? throw new InvalidOperationException("Native action memory has no bound physical bank.");
            owner.State = owner.State.WithAction(NativeIndex, value);
        }
    }
    internal ushort NativePreviousTargetMask { get; set; }
    internal byte? NativeDrawnSpell { get; set; }
    internal BattleAbility? LastAttackerAbility { get; set; }
    internal bool AiDeathHandled { get; set; }
    internal int NativeIndex { get; set; } = -1;
    internal int? ResolvedNativeLevel { get; set; }
    internal byte[] NativeStatMultipliers { get; } = [10, 10, 10, 10, 10, 10];

    internal void BindNativeLevel(int level)
    {
        NativeBattleEnemyTemplate template = definition.NativeTemplate ?? throw new InvalidDataException("Native reserve allocation lacks its installed level template.");
        if ((uint)level >= template.Levels.Length)
        {
            throw new InvalidDataException("Native reserve level exceeds its installed projection catalog.");
        }
        NativeBattleEnemyLevel projection = template.Levels[level];
        definition = definition with { Level = level, MaximumHp = projection.MaximumHp, Attack = projection.Strength, Defense = projection.Vitality, Magic = projection.Magic, Spirit = projection.Spirit, Speed = Math.Max(1, projection.Speed), Evasion = projection.Evasion, ElementDefenses = projection.ElementDefenses, DrawSpells = projection.DrawSpells, DrawGuardianForces = projection.DrawGuardianForces, NativeAi = definition.NativeAi is null ? null : definition.NativeAi with { LevelBand = projection.AiLevelBand } };
        ResolvedNativeLevel = level;
    }
    internal NativeBattleEnemyTemplate? NativeTemplate => definition.NativeTemplate;

    internal void ResetNativeStatusAllocation()
    {
        NativeBattleStatusAllocation? allocation = definition.NativeTemplate?.NativeStatusAllocation;
        MutableNativeStatusResistances = InitialNativeStatusResistances();
        NativeStatus0 = allocation?.Saved ?? (ushort)(definition.NativeStatus0 | definition.NativeRetainedStatusBits);
        NativeStatus1 = allocation?.Battle ?? definition.NativeStatus1;
        Array.Fill(NativeStatusTimers, (short)-1111);
        Effects.Clear();
        Effects.AddRange(definition.StatusEffects.Select(effect => new BattleStatusState(effect, effect.DurationFrames, effect.TickIntervalFrames)));
    }
    public int MaximumHp => definition.MaximumHp;
    public int CurrentHp
    {
        get => currentHp;
        internal set
        {
            currentHp = value;
            RefreshNativeHpStatusFlags();
        }
    }

    internal void AddNativeHitPointWord(short amount)
    {
        //487DF0 opcode60 directly adds a sign-extended WORD to the DWORD; no clamp, status refresh or hit callback.
        currentHp = unchecked(currentHp + amount);
    }

    private void RefreshNativeHpStatusFlags()
    {
        //494360 updates the HP flags at the HP commit boundary, not when AI queries the saved WORD.
        NativeStatus0 &= 0xFCFF;
        if (currentHp != 0 && currentHp < MaximumHp >> 1)
        {
            NativeStatus0 |= 0x200;
            if (currentHp < MaximumHp >> 2)
            {
                NativeStatus0 |= 0x100;
            }
        }
    }
    public int Speed => ScaleNativeStat(4, definition.Speed);
    public int Attack => ScaleNativeStat(0, definition.Attack);
    public int Defense => ScaleNativeStat(1, definition.Defense);
    public int Level => definition.Level;
    public int Magic => ScaleNativeStat(2, definition.Magic);
    public int Spirit => ScaleNativeStat(3, definition.Spirit);
    public int Evasion => ScaleNativeStat(5, definition.Evasion);
    public int Luck => definition.Luck;
    public ushort NativeKillCount { get; internal set; }
    public ushort? NativeDeathCount { get; internal set; }
    public int Hit => definition.Hit;
    public bool? NativeCharacterFlag100 => definition.NativeCharacterFlag100;
    public ushort NativeRetainedStatusBits => definition.NativeRetainedStatusBits;
    //4868C0/486C70 clear Berserk, then copy the complete native WORD to the saved character record.
    public ushort PersistentStatus => (ushort)(BattleSession.NativeSavedStatusBits(this) & ~0x20);
    internal IReadOnlyList<byte> NativeStatusResistances => MutableNativeStatusResistances;

    private byte[] InitialNativeStatusResistances()
    {
        return definition.NativeStatusResistances.Length == 40 ? definition.NativeStatusResistances.ToArray() : definition.NativeTemplate?.NativeStatusAllocation is { } allocation ? allocation.Resistances.ToArray() : definition.NativeStatusResistances.ToArray();
    }

    internal void SetNativeStatMultiplier(int index, byte multiplier)
    {
        if ((uint)index >= NativeStatMultipliers.Length || IsPlayer)
        {
            throw new NotSupportedException("Native battle AI statistic multiplier requires one of the six enemy statistic rows.");
        }
        if (definition.NativeTemplate is not { } template || template.Levels[Level].BaseStatistics.Length != 6)
        {
            throw new NotSupportedException("Native battle AI statistic multiplier requires the original six pre-byte statistic values.");
        }
        NativeStatMultipliers[index] = multiplier;
    }

    private int ScaleNativeStat(int index, int projected)
    {
        int[]? originalStats = definition.NativeTemplate?.Levels[Level].BaseStatistics;
        if (NativeStatMultipliers[index] == 10 && originalStats is not { Length: 6 })
        {
            return projected;
        }
        int original = originalStats is { Length: 6 } ? originalStats[index] : projected;
        //48C1C0 multiplies the signed495930 result, divides by10, then caps and writes its lowBYTE.
        return unchecked((byte)Math.Min(255, original * NativeStatMultipliers[index] / 10));
    }
    public int PhysicalPower => definition.PhysicalPower;
    public int DrainPercent => definition.DrainPercent;
    public int ElementAttack => definition.ElementAttack;
    public int ElementAttackPercent => definition.ElementAttackPercent;
    public IReadOnlyList<int> ElementDefenses => Array.AsReadOnly(definition.ElementDefenses);
    private readonly NativeMagicStock[] nativeMagicInventory = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
    internal Func<NativeCharacterVitals>? MagicOwner { get; set; }
    internal Action<NativeCharacterVitals>? SetMagicOwner { get; set; }
    public int? MagicRecordId => definition.MagicRecordId;
    internal NativeMagicStock[] NativeMagicInventory => MagicOwner is not null && MagicRecordId is int row ? MagicOwner().Records[MagicOwner().MagicSystem.SharedMagicStock ? 0 : row].MagicInventory : nativeMagicInventory;
    public IReadOnlyList<BattleStock> MagicStocks => (MagicOwner is not null && MagicRecordId is int row ? MagicOwner().GetMagicInventory(row) : NativeMagicInventory.AsEnumerable()).Where(stock => stock.SpellId != 0 && stock.Count > 0).Select(stock => new BattleStock(stock.SpellId, stock.Count)).ToArray();
    public IReadOnlyList<NativeMagicStock> NativeMagicSlots => Array.AsReadOnly(NativeMagicInventory);
    public IReadOnlyList<BattleDrawSpell> DrawSpells => nativeDrawSlots.Where(slot => slot.Id is > 0 and < 64).Select(slot => new BattleDrawSpell(slot.Id, slot.Resistance)).ToArray();
    public NativeBattleMugDefinition? NativeMug => definition.NativeTemplate?.NativeMug ?? definition.NativeMug;
    public IReadOnlyList<int> DrawGuardianForces => nativeDrawSlots.Where(slot => slot.Id >= 64).Select(slot => slot.Id - 64).ToArray();
    public int? WeaponId => definition.WeaponId;
    public string? WeaponName => definition.WeaponName;
    public IReadOnlyList<int> EquipmentIds => Array.AsReadOnly(definition.EquipmentIds);
    public IReadOnlyList<BattleAbility> Abilities => Array.AsReadOnly(definition.Abilities);
    public IReadOnlyList<BattleStatusSnapshot> StatusEffects => Effects.Select(effect => new BattleStatusSnapshot(effect.Definition.Kind, effect.RemainingFrames, effect.UntilTickFrames, effect.Definition)).ToArray();
    public int Atb
    {
        get => NativeAtb?.Display ?? displayAtb;
        internal set
        {
            if (NativeAtb is { } native)
            {
                NativeAtb = native with { Current = checked((int)((long)native.Maximum * value / BattleSession.AtbMaximum)) };
            }
            else
            {
                displayAtb = value;
            }
        }
    }
    public bool IsBusy { get; internal set; }
    public bool IsAlive => IsActive && (BattleSession.NativeSavedStatusBits(this) & 1) == 0;
    public bool CanAct => BattleSession.CanNativeBattleActorAct(this);
    public bool IsReady => CanAct && !IsBusy && Atb == BattleSession.AtbMaximum;
}

internal sealed class BattleStatusState(BattleStatusEffectDefinition definition, int remainingFrames, int untilTickFrames)
{
    internal BattleStatusEffectDefinition Definition { get; } = definition;
    internal int RemainingFrames { get; set; } = remainingFrames;
    internal int UntilTickFrames { get; set; } = untilTickFrames;
}
