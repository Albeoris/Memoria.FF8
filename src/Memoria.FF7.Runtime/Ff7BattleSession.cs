using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public enum Ff7BattleOutcome
{
    Fighting,
    Victory,
    Defeat,
    Escaped
}

public sealed record Ff7BattleActorState(int Identity, Ff7BattleAiActor Actor, ushort Gauge, byte[] AiMemory, bool AwaitingTurn = false, bool Defending = false, Ff7BattleStatusTimers Timers = default, byte LimitGauge = 0, Dictionary<byte, int>? AllUses = null)
{
    public int? SuccessorSlot { get; init; }
    public uint StatusProtection { get; init; }
}
public sealed record Ff7BattleActionState(Ff7BattleQueuedCommand Command, long StartedAt, int Animation, bool Applied = false, BattleActionPoseState? Pose = null, Ff7BattleTargetCallbacks? Reactions = null, Ff7BattleMotion? Motion = null, Ff7BattleFireState? Fire = null, Ff7BattleIceState? Ice = null, Ff7BattleBoltState? Bolt = null, Ff7BattleCureState? Cure = null, Ff7BattleBraverState? Braver = null)
{
    public ushort GuestAppliedTargets { get; init; }
    public int? FacingYaw { get; init; }
    public Ff7BattleReactionState? TargetReactions { get; init; }
    public Ff7BattleHit? PendingDisplayHit { get; init; }
    public Ff7BattleGrenadeState? Grenade { get; init; }
    public bool GrenadeStarted { get; init; }
    public Ff7BattleGuestLimitState? GuestLimit { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte? GuestAttackTargetCue { get; init; }
    public Ff7BattleBeatRushState? BeatRushEffect { get; init; }
    public Ff7BattleBigShotState? BigShot { get; init; }
    public Ff7BattleSmokeShotState? SmokeShot { get; init; }
    public Ff7BattleSmogState? Smog { get; init; }
    public Ff7BattleDestroyedState? Destroyed { get; init; }
    public Ff7BattleSearchScopeState? SearchScope { get; init; }
    public Ff7BattleRocketState? Rockets { get; init; }
    public bool SilkStarted { get; init; }
    public bool WaveStarted { get; init; }
    public bool EnergyBallStarted { get; init; }
    public bool HellBubblesStarted { get; init; }
    public bool TailLaserStarted { get; init; }
    public bool BeamGunStarted { get; init; }
    public bool BigBomberStarted { get; init; }
    public bool LaserCannonStarted { get; init; }
    public bool PrimaryCallbacksRunning { get; init; }
    public bool EffectRunning => PrimaryCallbacksRunning || Fire is { Completed: false } || Ice is { Completed: false } || Bolt is { Completed: false } || Cure is { Completed: false } || Braver is { Completed: false } || BeatRushEffect is { Completed: false } || BigShot is { Completed: false } || SmokeShot is { Completed: false } || Smog is { Completed: false } || Destroyed is { Completed: false } || SearchScope is { Completed: false } || Rockets is { Completed: false };
}
public sealed record Ff7BattleHit(int Source, int Target, ushort AttackId, Ff7BattleDamageResult Damage, long Tick)
{
    public int? NativeResultMarker { get; init; }
}
public sealed record Ff7BattleReaction(int Slot, long Tick);
public sealed record Ff7BattleLimitSelection(int Slot, ushort Targets, Ff7BeatRushReel Reel);
public sealed record Ff7BattleSnapshot(long RequestId, int FormationId, byte Layout, long Tick, Ff7BattleActorState[] Actors, Ff7BattleRngState Random, Ff7BattleCommandQueueState Queue, Ff7InventorySlot[] Inventory, Ff7BattleActionState? Action = null, Ff7BattleHit? LastHit = null, Ff7BattleOutcome Outcome = Ff7BattleOutcome.Fighting, Ff7BattleReaction? Reaction = null, Ff7BattleLimitSelection? BeatRush = null, short BeatRushPosition = 0, Ff7BattleEscapeState Escape = default, bool EscapeBlocked = false, ushort RequestFlags = 0, byte RequestMode = 0, int? EntryMusicTrack = null, Ff7BattleMessageState? Messages = null, int TerminalFrames = 0, long? EscapePoseStarted = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Ff7BattleGuestVitalsBinding? GuestVitals = null)
{
    public Ff7ResolvedEnemyExtension[]? EnemyExtensions { get; init; }
    public byte[]? AiGlobalMemory { get; init; }
    public byte[]? AiSaveBank1 { get; init; }
    public byte[]? AiSaveBankDirty { get; init; }
    public byte[]? FormationAiMemory { get; init; }
    public byte FormationAiPending { get; init; }
    public ushort? CounterProcessedMask { get; init; }
    public byte? ImprisonedType { get; init; }
    public Ff7BattleCameraPresentation? Camera { get; init; }
    public Ff7BattleIntroState? Intro { get; init; }
    public Ff7BattleActorMotionState? ActorMotion { get; init; }
    public Ff7BattleActorModelState[] ActorModels { get; init; } = [];
    public int ModelPrimaryTarget { get; init; }
    public Ff7BattleHit? DisplayedHit { get; init; }
    public bool GuestAttackUsed { get; init; }
    public bool GuestPresentationAttached { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Ff7BattleGuestProjection? GuestProjection { get; init; }
    public byte? GuestBaseDexterity { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? GuestLimitReadyAt { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Ff7BattleGuestSlotState[]? GuestSlots { get; init; }

    public IReadOnlyList<Ff7BattleGuestSlotState> GetGuestSlotStates()
    {
        return GuestSlots ?? (GuestVitals is { } binding ? [new(binding, GuestProjection, GuestBaseDexterity, GuestLimitReadyAt)] : []);
    }

    public Ff7BattleSoundState? Sounds { get; init; }
    public Ff7BattleSoundReceipt[] SoundRequests { get; init; } = [];
    public short[]? SoundProjectedX { get; init; }
    public int SoundAttacker { get; init; }
    public short BigShotChargeRotation { get; init; }
    public short BigShotCounterRotation { get; init; }
    public Ff7BattleHitmarkState[] Hitmarks { get; init; } = [];
    public Ff7BattleSecondaryState? SecondaryEffects { get; init; }
    public Ff7BattlePrimaryState? PrimaryEffects { get; init; }
    public short SilkDirection { get; init; } = 1;
    public Ff7BattleStageFadeState? StageFade { get; init; }
    public Ff7BattleStageFadeState? RenderStageFade { get; init; }
    public short SearchScopeScaleCounter { get; init; }
}

/// <summary>One FFVII encounter, bound to a single field request and its native physical actor slots.</summary>
public sealed partial class Ff7BattleSession
{
    private readonly IReadOnlyList<KernelSection> kernel;
    private readonly Ff7BattleRng random;
    private readonly Ff7BattleAnimationResources? animations;
    private readonly IReadOnlyList<Ff7BattleAttack> limits;
    private readonly IReadOnlyList<byte> beatRushSymbols;
    private readonly Ff7BattlePlayerStats?[] party = new Ff7BattlePlayerStats?[3];
    private readonly Ff7CharacterState?[] characters = new Ff7CharacterState?[3];
    private readonly int[] limitDivisors = new int[3];
    private int speedDenominator;
    private const byte BattleSpeed = 128;
    public const int LogicUpdatesPerSecond = 60;
    private const int LogicUpdatesPerPresentation = 4;
    private Ff7BattleSnapshot state;

    public Ff7BattleSession(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> materiaModifiers, long requestId, ushort requestFlags = 0, Ff7BattleSnapshot? snapshot = null, Ff7BattleAnimationResources? animations = null, IReadOnlyList<Ff7BattleAttack>? limits = null, IReadOnlyList<byte>? beatRushSymbols = null, byte requestMode = 0, int? musicTrack = null, short bigShotChargeRotation = 0, short bigShotCounterRotation = 0, short searchScopeScaleCounter = 0, Ff7BattleCameraScriptResources? cameraScripts = null, byte cameraPresentationCounter = 0, short silkDirection = 1, Ff7EnemyExtensionCatalog? enemyExtensions = null, IReadOnlyList<byte>? enemyExtensionPartyLevels = null, byte[]? initialSaveBank1 = null)
    {
        Formation = formation;
        SceneBattleData nativeData = SceneBattleData.Read(formation);
        Ff7ResolvedEnemyExtension[] resolvedExtensions = snapshot is null ? ResolveEnemyExtensions(nativeData, enemyExtensions, enemyExtensionPartyLevels ?? progress.Party.Where(id => id != 255).Select(id => (byte)progress.Characters.Single(c => c.Id == id).Level).ToArray()) : snapshot.EnemyExtensions ?? [];
        Data = ApplyEnemyExtensions(nativeData, resolvedExtensions);
        Formation = formation with { EnemyRewards = formation.EnemyRewards.Select(reward => Data.Enemies.FirstOrDefault(e => e.Placement.EnemyId == reward.EnemyId)?.Rewards ?? reward).ToArray() };
        this.kernel = kernel;
        this.animations = animations;
        this.cameraScripts = cameraScripts;
        this.limits = limits ?? [];
        this.beatRushSymbols = beatRushSymbols ?? [];
        if (snapshot is null)
        {
            progress = Ff7NativePartyRecovery.Publish(progress, Ff7MateriaResources.ReadRecords(kernel), materiaModifiers).Progress;
        }
        List<Ff7BattleActorState> initialActors = [];
        List<byte> partyDexterity = [];
        for (int slot = 0; slot < characters.Length; slot++)
        {
            int id = progress.Party[slot];
            if (id == 255)
            {
                continue;
            }
            Ff7CharacterState character = progress.Characters.Single(character => character.Id == id);
            Ff7BattlePlayerStats stats = Ff7NativeBattlePlayerStats.Create(character, kernel, materiaModifiers);
            characters[slot] = character;
            party[slot] = stats;
            limitDivisors[slot] = Ff7NativeBattleLimits.ReadGaugeDivisor(character, kernel);
            partyDexterity.Add(character.NativeRecord[6]);
            ReadOnlySpan<byte> record = character.NativeRecord;
            uint statuses = stats.Actor.Statuses | (character.HitPoints == 0 ? 1u : 0u);
            uint conditions = stats.Actor.BackRow ? 0x48u : 8u;
            initialActors.Add(new(id, new(slot, statuses, conditions, (ushort)character.MagicPoints, BinaryPrimitives.ReadUInt16LittleEndian(record[58..]), (uint)character.HitPoints, BinaryPrimitives.ReadUInt16LittleEndian(record[56..]), stats.Actor.Defense, stats.Actor.MagicDefense, Variant: (byte)(id + 0x10), HurtAction: 5), 0, [], LimitGauge: record[15], AllUses: new(Ff7NativeBattleAll.Read(character, kernel))));
        }
        speedDenominator = Ff7NativeAtb.SpeedDenominator(partyDexterity.ToArray());
        foreach (Ff7NativeEnemyState enemy in Ff7NativeEnemyInitialization.Create(Data))
        {
            initialActors.Add(new(enemy.EnemyId, new(enemy.ActorSlot, enemy.Statuses, enemy.Conditions, enemy.MagicPoints, enemy.MaxMagicPoints, enemy.HitPoints, enemy.MaxHitPoints, enemy.Defense, enemy.MagicDefense, IdleAction: enemy.IdleAction, HurtAction: enemy.HurtAction), 0, []));
        }
        random = new(kernel[2].Data.Span, snapshot?.Random ?? Ff7BattleRng.BeginBattle(progress.BattleRng?.CrtSeed ?? 1));
        if (snapshot is not null)
        {
            ValidateSnapshot(snapshot, requestId, initialActors);
            state = CopySnapshot(snapshot);
            RestoreNativeAiState(initialSaveBank1, progress.Gil);
            RefreshGuestSpeedDenominator();
            InitializeActorModels(snapshot.Tick == 0);
            InitializeCamera(cameraPresentationCounter, snapshot.Tick == 0);
            if (animations is not null)
            {
                EnsurePrimaryEffects();
                RestoreGrenadePresentation();
            }
            return;
        }
        byte layout = Ff7NativeAtb.ResolveLayout(formation.Layout, formation.BattleFlags, (requestFlags & 4) != 0);
        Ff7BattleAiActor[] setup = Ff7NativeAtb.ApplyFormationConditions(layout, initialActors.Select(actor => actor.Actor).ToArray(), Data);
        state = new(requestId, formation.FormationId, layout, 0, initialActors.Select((actor, index) => actor with { Actor = setup[index] }).ToArray(), random.Capture(), Ff7BattleCommandQueueState.Create(), (Ff7InventorySlot[])progress.Inventory.Clone(), Escape: Ff7NativeBattleEscape.Start(formation, Ff7NativeAtb.BattleTypeForLayout(layout)), RequestFlags: requestFlags, RequestMode: requestMode, EntryMusicTrack: musicTrack) { BigShotChargeRotation = bigShotChargeRotation, BigShotCounterRotation = bigShotCounterRotation, SearchScopeScaleCounter = searchScopeScaleCounter, SilkDirection = silkDirection, EnemyExtensions = resolvedExtensions };
        RestoreNativeAiState(initialSaveBank1, progress.Gil, fresh: true);
        foreach (SceneBattleEnemy enemy in Data.Enemies)
        {
            InvokeAi(enemy.FormationSlot + 4, 0);
        }
        FlushFormationAi();
        Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(state.Actors.Select(actor => actor.Actor).ToArray(), NativeImprisonedType);
        ushort[] gauges = Ff7NativeAtb.InitializeForLayout(masks.TurnEligible, layout, false, random);
        foreach (Ff7BattleActorState actor in state.Actors)
        {
            Replace(actor with { Gauge = gauges[actor.Actor.Slot] });
        }
        InitializeActorModels(true);
        InitializeCamera(cameraPresentationCounter, true);
        if (animations is not null)
        {
            EnsurePrimaryEffects();
        }
    }

    public SceneFormation Formation { get; }
    public SceneBattleData Data { get; }
    public byte Layout => state.Layout;
    public long Tick => state.Tick;
    public short BigShotChargeRotation => state.BigShotChargeRotation;
    public short BigShotCounterRotation => state.BigShotCounterRotation;
    public short SearchScopeScaleCounter => state.SearchScopeScaleCounter;
    public short SilkDirection => state.SilkDirection;
    public long PresentationTick => (Tick + LogicUpdatesPerPresentation - 1) / LogicUpdatesPerPresentation;
    public Ff7BattleOutcome Outcome => state.Outcome;
    public IReadOnlyList<Ff7BattleActorState> Actors => Array.AsReadOnly(state.Actors);
    public Ff7BattleActionState? Action => state.Action;
    public Ff7BattleHit? LastHit => state.LastHit;
    public Ff7BattleHit? DisplayedHit => state.DisplayedHit;
    public Ff7BattleReaction? Reaction => state.Reaction;
    public string? Message => state.Messages?.Visible;
    public Ff7BattleActionTitle? ActionTitle => state.PrimaryEffects?.ActionTitle;

    public string SceneAttackName(int slot) => ReadBattleText(Data.Attacks.Single(attack => attack.Slot == slot).Name.Span);
    public bool EscapeBlocked => state.EscapeBlocked;
    public bool ReturnsAfterDefeat => state.RequestMode != 0;
    public int? MusicTrack => Outcome switch
    {
        Ff7BattleOutcome.Victory when (Formation.BattleFlags & 8) != 0 && (state.RequestFlags & 0x120) == 0 => 46,
        Ff7BattleOutcome.Defeat when !ReturnsAfterDefeat => 58,
        _ => state.EntryMusicTrack
    };
    public bool ResultReady => Outcome != Ff7BattleOutcome.Fighting && state.TerminalFrames >= TerminalDuration;
    public int? ReadyPlayer => IntroActive || Outcome != Ff7BattleOutcome.Fighting || BeatRushSelecting ? null : state.Actors.FirstOrDefault(actor => actor.Actor.Slot < 3 && actor.AwaitingTurn && CanAct(actor))?.Actor.Slot;
    public bool BeatRushSelecting => Outcome == Ff7BattleOutcome.Fighting && state.BeatRush is { Reel.Stopped: false };
    public Ff7BeatRushSlot? BeatRushVisible => state.BeatRush?.Reel.ReadVisibleSymbol(beatRushSymbols);
    public long RequestId => state.RequestId;

    private bool CelebrateVictory => (Formation.BattleFlags & 8) != 0 && (state.RequestFlags & 0x100) == 0;
    private int TerminalDuration => Outcome switch
    {
        Ff7BattleOutcome.Victory when CelebrateVictory => 49,
        Ff7BattleOutcome.Escaped => 8,
        Ff7BattleOutcome.Defeat => 30,
        _ => 1
    };

    public Ff7BattleSnapshot Capture()
    {
        return CopySnapshot(state with { Random = random.Capture() });
    }

    public string Name(int slot)
    {
        if (GetGuestBinding(slot) is { } guest)
        {
            return guest.OriginName;
        }
        return slot < 3 ? characters[slot]!.Name : ReadBattleText(Enemy(slot).Rewards.Name.Span);
    }

    public int WeaponModel(int slot) => WeaponModelSelector(slot) & 15;

    private byte WeaponModelSelector(int slot)
    {
        int weaponId = characters[slot]!.NativeRecord[28];
        return kernel[5].Data.Span[weaponId * 44 + 9];
    }

    public void Advance(bool waitForCommand = false, bool confirmLimit = false, bool escapeHeld = false, bool turnBased = false)
    {
        if (Outcome != Ff7BattleOutcome.Fighting)
        {
            if (!ResultReady)
            {
                state = state with { Tick = checked(state.Tick + 1) };
                if ((Tick - 1) % LogicUpdatesPerPresentation == 0)
                {
                    state = state with { TerminalFrames = state.TerminalFrames + 1 };
                    if (Outcome == Ff7BattleOutcome.Escaped)
                    {
                        AppendSounds(Ff7NativeBattleSound.Escape(state.Sounds ?? Ff7BattleSoundState.Empty));
                    }
                    BeginCameraPresentation();
                    AdvanceCameraPresentation();
                    AdvanceBattleSounds();
                    AdvancePrimaryPresentation();
                    PublishSoundScreenPositions();
                    FinishCameraPresentation();
                }
            }
            return;
        }
        state = state with { Tick = checked(state.Tick + 1) };
        if (IntroActive)
        {
            if ((Tick - 1) % LogicUpdatesPerPresentation == 0)
            {
                AdvanceIntroPresentation();
            }
            return;
        }
        bool presentationDue = (Tick - 1) % LogicUpdatesPerPresentation == 0;
        if (presentationDue)
        {
            BeginCameraPresentation();
        }
        AdvanceGuestAttack();
        if (presentationDue)
        {
            AdvancePresentation(escapeHeld);
            AdvanceCameraPresentation();
            AdvanceBattleSounds();
            AdvancePrimaryPresentation();
            PublishSoundScreenPositions();
            FinishCameraPresentation();
        }
        if (Outcome != Ff7BattleOutcome.Fighting)
        {
            return;
        }
        if (BeatRushSelecting)
        {
            AdvanceBeatRush(confirmLimit);
        }
        else if (turnBased && !escapeHeld)
        {
            if (ReadyPlayer is null)
            {
                AdvanceTurn();
            }
        }
        else if (!waitForCommand)
        {
            state = state with { Escape = Ff7NativeBattleEscape.AdvanceClock(state.Escape, escapeHeld, BattleSpeed, Ff7NativeAtb.BattleTypeForLayout(Layout)) };
            AdvanceGauges();
        }
    }

    /// <summary>Advance the native gauges and status timers to the next turn or queued action without advancing presentation.</summary>
    public void AdvanceTurn()
    {
        const int maximumTimeSteps = 4096;
        if (Outcome != Ff7BattleOutcome.Fighting || IntroActive || BeatRushSelecting || state.Action is not null || HasPrimaryPresentationCallbacks || !BattleQueueEmpty)
        {
            return;
        }
        ushort readyPlayers = ReadyPlayerMask();
        for (int step = 0; step < maximumTimeSteps; step++)
        {
            if (!AdvanceGauges() || !BattleQueueEmpty || ReadyPlayerMask() != readyPlayers)
            {
                break;
            }
        }
    }

    private bool BattleQueueEmpty => state.Queue.ReadyRead == state.Queue.ReadyWrite && !state.Queue.Slots.Any(entry => entry is not null);

    private ushort ReadyPlayerMask() => Mask(state.Actors.Where(actor => actor.Actor.Slot < 3 && actor.AwaitingTurn && CanAct(actor)));

    private void AdvancePresentation(bool escapeHeld)
    {
        if (state.Messages is { } messages)
        {
            state = state with { Messages = Ff7NativeBattleMessage.AdvancePresentation(messages, liveFrame: true, paused: false) };
        }
        bool won = !state.Actors.Any(actor => actor.Actor.Slot >= 4 && IsAlive(actor) && Ff7NativeBattleStatuses.IsBattleParticipant(actor.Actor.Statuses, NativeImprisonedType) && (actor.Actor.Conditions & 0x18) != 0);
        bool lost = !state.Actors.Any(actor => actor.Actor.Slot < 3 && IsAlive(actor) && Ff7NativeBattleStatuses.IsBattleParticipant(actor.Actor.Statuses, NativeImprisonedType));
        bool pincer = Ff7NativeAtb.BattleTypeForLayout(Layout) == 4 && state.Actors.Where(actor => actor.Actor.Slot >= 4 && IsAlive(actor)).Select(actor => FacingGroup(actor.Actor)).Distinct().Count() > 1;
        bool partyCanMove = state.Actors.Any(actor => actor.Actor.Slot < 3 && CanAct(actor) && (actor.Actor.Statuses & 0x400) == 0);
        Ff7BattleEscapeInput escape = Ff7NativeBattleEscape.UpdatePresentation(state.Escape, escapeHeld, Ff7NativeBattleEscape.IsAllowed(Formation, (state.RequestFlags & 8) != 0), partyCanMove, pincer, won || lost, random);
        long? escapePose = escapeHeld && !escape.ShowBlockedMessage && !won && !lost ? state.EscapePoseStarted ?? PresentationTick : null;
        state = state with { Escape = escape.State, EscapeBlocked = escape.ShowBlockedMessage, EscapePoseStarted = escapePose };
        if (escape.Escaped)
        {
            state = state with { Outcome = Ff7BattleOutcome.Escaped, Action = null };
            return;
        }
        if (state.Action is Ff7BattleActionState active)
        {
            if (!active.Command.GuestAttack)
            {
                AdvanceAction(active);
            }
            return;
        }
        if (HasPrimaryPresentationCallbacks || (won || lost) && state.Actors.Any(actor => actor.Actor.Slot >= 4 && HasActorDeathPresentation(actor.Actor.Slot)))
        {
            return;
        }
        PromoteReadyCommands();
        Ff7BattleQueueSelection selection = Ff7BattleCommandQueue.TakeNext(state.Queue, won || lost ? 1 : 7);
        state = state with { Queue = selection.State };
        if (selection.Entry is { } entry)
        {
            Dispatch(entry.Command);
        }
        else if (won || lost)
        {
            state = state with { Outcome = lost ? Ff7BattleOutcome.Defeat : Ff7BattleOutcome.Victory };
        }
    }

    private bool AdvanceGauges()
    {
        bool changed = false;
        for (int index = 0; index < state.Actors.Length; index++)
        {
            Ff7BattleActorState actor = state.Actors[index];
            int slot = actor.Actor.Slot;
            if (!IsAlive(actor) || slot >= 4 && (actor.Actor.Conditions & 0x10) == 0)
            {
                continue;
            }
            Ff7BattleStatusTick status = Ff7NativeBattleStatusClock.Advance(actor.Timers, actor.Actor.Statuses, BattleSpeed, NativeImprisonedType, actor.Actor.MaxHitPoints);
            changed |= status.Timers != actor.Timers || status.Statuses != actor.Actor.Statuses || status.HitPointDelta != 0;
            uint hp = (uint)Math.Clamp((long)actor.Actor.HitPoints + status.HitPointDelta, 0, actor.Actor.MaxHitPoints);
            uint statuses = hp == 0 ? status.Statuses | Ff7NativeBattleActionResolver.Death : status.Statuses;
            actor = actor with { Timers = status.Timers, Actor = actor.Actor with { Statuses = statuses, HitPoints = hp } };
            state.Actors[index] = actor;
            if (status.PoisonDue)
            {
                state = state with { Queue = Ff7BattleCommandQueue.Enqueue(state.Queue, new(3, slot, 0x23, 0, (ushort)(1 << slot))) };
            }
            if (actor.AwaitingTurn)
            {
                ObserveGuestLimitReady(actor);
                continue;
            }
            Ff7BattleDamageActor stats = Stats(actor);
            ushort gauge = actor.Gauge;
            byte? guestDexterity = GetGuestProjection(slot)?.Dexterity;
            ushort increment = Ff7NativeAtb.Increment(slot, guestDexterity ?? stats.Dexterity, guestDexterity is not null ? (sbyte)0 : stats.DexterityModifier, Ff7NativeBattleStatuses.ProjectClockStatuses(actor.Actor.Statuses, NativeImprisonedType), speedDenominator, BattleSpeed);
            bool ready = Ff7NativeAtb.Advance(ref gauge, increment, false, false);
            changed |= gauge != actor.Gauge;
            actor = actor with { Gauge = gauge, AwaitingTurn = ready };
            state.Actors[index] = actor;
            if (ready && slot >= 4)
            {
                state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, 0xFF) };
            }
            else if (ready)
            {
                ObserveGuestLimitReady(actor);
            }
        }
        return changed;
    }

    private void Dispatch(Ff7BattleQueuedCommand command)
    {
        Ff7BattleActorState source = Actor(command.ActorSlot);
        if (command.Priority >= 2)
        {
            state = state with { CounterProcessedMask = (ushort)(1 << command.ActorSlot) };
        }
        if (command.CommandId != 0xFF)
        {
            PublishNativeActionContext(command);
        }
        if (command.CommandId == 0xFF)
        {
            if (IsAlive(source))
            {
                InvokeAi(command.ActorSlot, 1);
                FlushFormationAi();
            }
            FinishTurn(command.ActorSlot);
            return;
        }
        if (command.Priority != 0)
        {
            foreach (SceneBattleEnemy enemy in Data.Enemies)
            {
                InvokeAi(enemy.FormationSlot + 4, 7);
            }
        }
        if (command.CommandId == 0x21)
        {
            string message = ReadBattleText(command.Message ?? throw new InvalidDataException("A FFVII battle message requires its original text."));
            state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, message) };
            if (command.Priority is 1 or 2)
            {
                FinishTurn(command.ActorSlot);
            }
            FlushFormationAi();
            return;
        }
        if (command.CommandId == 0x22)
        {
            if (command.Argument >= 16)
            {
                throw new InvalidDataException("A native callback command requires a valid callback index.");
            }
            foreach (Ff7BattleActorState recipient in state.Actors.Where(actor => (command.TargetMask & 1 << actor.Actor.Slot) != 0).ToArray())
            {
                if (recipient.Actor.Slot >= 4)
                {
                    InvokeAi(recipient.Actor.Slot, (byte)command.Argument);
                }
            }
            FlushFormationAi();
            return;
        }
        if (command.CommandId == 0x24)
        {
            state = state with { Action = new(command, Tick, -1) };
            return;
        }
        if (!CanAct(source) && command.Priority != 0 && command.CommandId != 0x23)
        {
            CancelGuestItem(command);
            ResetGauge(command.ActorSlot);
            return;
        }
        if (TryDispatchGuestAttack(command))
        {
            return;
        }
        command = ResolveEnemyCommand(command);
        command = command with { TargetMask = ResolveQueuedTargets(command) };
        int animation = Animation(command);
        Ff7BattleMotion? motion = command.CommandId == (byte)Ff7BattleCommand.Change && animation >= 0 && animations is not null
            ? new(Ff7BattlePosition.FromVector(HomePlacement(command.ActorSlot).Position), default, 0)
            : null;
        state = state with { Action = new(command, Tick, animation, Motion: motion), SoundAttacker = command.ActorSlot };
    }

    private void InvokeAi(int slot, byte callback)
    {
        Ff7BattleActorState actor = Actor(slot);
        Ff7BattleAiActor[] actors = state.Actors.Select(candidate => candidate.Actor).ToArray();
        Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(actors, NativeImprisonedType);
        Ff7BattleAiResult result = Ff7BattleAi.RunActorCallback(new(slot, actors, masks.Targetable, masks.KnockedOut, masks.ExcludedOpponents, callback, GlobalMemory: state.AiGlobalMemory, SaveBank1: state.AiSaveBank1), actor.AiMemory, Data, random, kernel[1].Data);
        Replace(actor with { AiMemory = result.LocalMemory });
        state = state with { AiGlobalMemory = result.GlobalMemory.ToArray() };
        ApplyNativeSaveWrites(result);
        ApplyNativeAiTransfers(result.TransferUpdates);
        if (callback < 8)
        {
            state = state with { FormationAiPending = (byte)(state.FormationAiPending | 1 << callback) };
        }
        foreach (Ff7BattleAiActor updated in result.ActorUpdates)
        {
            Ff7BattleActorState previous = Actor(updated.Slot);
            Replace(previous with { Actor = updated, Timers = Ff7NativeBattleStatusClock.Apply(previous.Timers, previous.Actor.Statuses, updated.Statuses) });
        }
        foreach (Ff7BattleAiCommand command in result.Commands)
        {
            Ff7BattleQueuedCommand queued = Ff7BattleQueuedCommand.FromAi(command);
            state = state with { Queue = Ff7BattleCommandQueue.Enqueue(state.Queue, queued) };
            ApplyQueueConditions(queued);
        }
    }

    private void PromoteReadyCommands()
    {
        Ff7BattleCommandQueueState queue = state.Queue;
        state = state with { Queue = Ff7BattleCommandQueue.PromoteReady(queue) };
        for (int read = queue.ReadyRead; read != queue.ReadyWrite; read = (read + 1) % queue.Ready.Length)
        {
            ApplyQueueConditions(queue.Ready[read]!);
        }
    }

    private void ApplyQueueConditions(Ff7BattleQueuedCommand command)
    {
        Ff7BattleActorState actor = Actor(command.ActorSlot);
        uint conditions = Ff7BattleCommandQueue.ActorConditionsAfterEnqueue(command, actor.Actor.Conditions);
        Replace(actor with { Actor = actor.Actor with { Conditions = conditions }, Defending = (conditions & 0x20) != 0 });
    }

    private Ff7BattleDamageActor Stats(Ff7BattleActorState actor)
    {
        Ff7BattleAiActor native = actor.Actor;
        Ff7BattleDamageActor stats = native.Slot < 3 ? party[native.Slot]!.Value.Actor : Ff7NativeBattleDamage.FromEnemy(Enemy(native.Slot));
        stats = stats with { Level = native.Level ?? stats.Level, Attack = native.Strength ?? stats.Attack, Magic = native.Magic ?? stats.Magic, Dexterity = native.Speed ?? stats.Dexterity, Evade = native.PhysicalEvasion ?? stats.Evade };
        return ProjectGuestDefense(actor, stats with { NativeConditions = native.Conditions, Statuses = native.Statuses, Defense = native.Defense, MagicDefense = native.MagicDefense, BackRow = (native.Conditions & 0x40) != 0, Defending = actor.Defending, CurrentHitPoints = native.HitPoints });
    }

    private void ResetGauge(int slot)
    {
        Ff7BattleActorState actor = Actor(slot);
        Replace(actor with { Gauge = 0, AwaitingTurn = false });
        if (GetGuestSlotState(slot) is { LimitReadyAt: not null } guest)
        {
            SetGuestSlotState(guest with { LimitReadyAt = null });
        }
    }

    private void FinishTurn(int slot)
    {
        if (!state.Queue.Slots.Any(entry => entry?.Command.ActorSlot == slot && entry.Command.Priority >= 2 && entry.Command.CommandId != 0x23))
        {
            ResetGauge(slot);
        }
    }

    private void Replace(Ff7BattleActorState actor)
    {
        int index = Array.FindIndex(state.Actors, candidate => candidate.Actor.Slot == actor.Actor.Slot);
        state.Actors[index] = actor;
    }

    private Ff7BattleActorState Actor(int slot) => state.Actors.Single(candidate => candidate.Actor.Slot == slot);
    private SceneBattleEnemy Enemy(int slot) => Data.Enemies.Single(enemy => enemy.FormationSlot + 4 == slot);
    private static ushort Mask(IEnumerable<Ff7BattleActorState> actors) => (ushort)actors.Aggregate(0, (mask, actor) => mask | 1 << actor.Actor.Slot);
    private static bool IsAlive(Ff7BattleActorState actor) => actor.Actor.HitPoints != 0 && (actor.Actor.Statuses & 0x4001) == 0;
    private bool CanAct(Ff7BattleActorState actor) => IsAlive(actor) && (Ff7NativeBattleStatuses.ProjectClockStatuses(actor.Actor.Statuses, NativeImprisonedType) & 0x02004404) == 0;
    private static string ReadBattleText(ReadOnlySpan<byte> text) => string.Concat(FieldText.Read(text).Select(token => token.Text));

    private void ValidateSnapshot(Ff7BattleSnapshot snapshot, long requestId, List<Ff7BattleActorState> expected)
    {
        if (snapshot.RequestId != requestId || snapshot.FormationId != Formation.FormationId || snapshot.Layout != Ff7NativeAtb.ResolveLayout(Formation.Layout, Formation.BattleFlags, (snapshot.RequestFlags & 4) != 0) || snapshot.Tick < 0 || !Enum.IsDefined(snapshot.Outcome) || snapshot.Actors is null || snapshot.Inventory is null || snapshot.Queue is null || snapshot.EntryMusicTrack is < 1 or > 98 || snapshot.ImprisonedType is > 3)
        {
            throw new InvalidDataException("FFVII battle snapshot conflicts with its native encounter or physical actors.");
        }
        if (snapshot.Actors.Any(actor => actor?.Actor is null || actor.AiMemory is not { Length: 0 or Ff7BattleAi.LocalMemorySize }) || !snapshot.Actors.Select(actor => (actor.Actor.Slot, actor.Identity)).SequenceEqual(expected.Select(actor => (actor.Actor.Slot, actor.Identity))))
        {
            throw new InvalidDataException("FFVII battle snapshot has invalid physical actor ownership.");
        }
        ValidateGuestLimitSnapshot(snapshot);
        if (snapshot.TerminalFrames is < 0 or > 49 || snapshot.Messages is { } messages && (messages.Pending is null || messages.Pending.Length > 64 || messages.Pending.Any(entry => entry?.Text is null || entry.Delay > 1 || entry.Remaining > 67)) || snapshot.Action is { } action && (action.Command is null || action.StartedAt < 0 || action.StartedAt > snapshot.Tick || snapshot.Actors.All(actor => actor.Actor.Slot != action.Command.ActorSlot)))
        {
            throw new InvalidDataException("FFVII battle snapshot has an invalid presentation or action cursor.");
        }
        if (snapshot.Action is { GuestAttackTargetCue: byte cue } guest && (cue != 4 || guest.Applied || guest.GuestLimit is not null || !guest.Command.GuestAttack || guest.Command.GuestAbility != 0 || guest.Command.CommandId != (byte)Ff7BattleCommand.Attack))
        {
            throw new InvalidDataException("FFVII battle snapshot has an unowned guest Attack target cue.");
        }
        snapshot.Queue.Validate();
        ValidateGuestVitalsSnapshot(snapshot);
        ValidateSoundSnapshot(snapshot);
        ValidateSecondaryEffects(snapshot);
        ValidatePrimaryEffects(snapshot);
        ValidateCamera(snapshot);
        ValidateActorModels(snapshot);
        if (snapshot.Hitmarks is null || snapshot.Hitmarks.Any(hitmark => hitmark is null || hitmark.Target is < 0 or > 9 or 3 || hitmark.Definition?.Cues is null || hitmark.Frame < -1 || hitmark.Frame > hitmark.Definition.FrameCount || snapshot.Actors.All(actor => actor.Actor.Slot != hitmark.Target)))
        {
            throw new InvalidDataException("FFVII battle snapshot has an invalid target hitmark.");
        }
    }

    private static Ff7BattleSnapshot CopySnapshot(Ff7BattleSnapshot source)
    {
        Ff7BattleCommandQueueState queue = source.Queue;
        return source with
        {
            Actors = source.Actors.Select(actor => actor with { AiMemory = (byte[])actor.AiMemory.Clone(), AllUses = actor.AllUses is null ? null : new(actor.AllUses) }).ToArray(),
            Inventory = (Ff7InventorySlot[])source.Inventory.Clone(),
            AiGlobalMemory = source.AiGlobalMemory?.ToArray(),
            AiSaveBank1 = source.AiSaveBank1?.ToArray(),
            AiSaveBankDirty = source.AiSaveBankDirty?.ToArray(),
            FormationAiMemory = source.FormationAiMemory?.ToArray(),
            EnemyExtensions = source.EnemyExtensions?.Select(e => e with { DrawSlots = e.DrawSlots.ToArray(), Items = e.Items.ToArray() }).ToArray(),
            GuestSlots = source.GuestSlots is null ? null : (Ff7BattleGuestSlotState[])source.GuestSlots.Clone(),
            Action = CopyActionPrimaryEffects(CopyActionReactions(source.Action is { Motion: { VerticalSteps: { } steps } motion } action ? action with { Motion = motion with { VerticalSteps = (short[])steps.Clone() } } : source.Action)),
            Queue = queue with
            {
                Slots = queue.Slots.Select(entry => entry is null ? null : entry with { Command = entry.Command with { Message = entry.Command.Message?.ToArray() } }).ToArray(),
                Ready = queue.Ready.Select(command => command is null ? null : command with { Message = command.Message?.ToArray() }).ToArray(),
                ReadSequences = (byte[])queue.ReadSequences.Clone(),
                WriteSequences = (byte[])queue.WriteSequences.Clone()
            },
            Camera = source.Camera is null ? null : CopyCamera(source.Camera),
            ActorModels = source.ActorModels.Select(model => model with { Joints = model.Joints?.ToArray() }).ToArray(),
            ActorMotion = source.ActorMotion is null ? null : CopyActorMotion(source.ActorMotion),
            Messages = source.Messages is null ? null : source.Messages with { Pending = (Ff7BattleMessageEntry[])source.Messages.Pending.Clone() },
            Hitmarks = source.Hitmarks.ToArray(),
            SecondaryEffects = source.SecondaryEffects is null ? null : CopySecondaryEffects(source.SecondaryEffects),
            PrimaryEffects = source.PrimaryEffects is null ? null : CopyPrimaryEffects(source.PrimaryEffects),
            Sounds = source.Sounds is null ? null : CopySoundState(source.Sounds),
            SoundRequests = (Ff7BattleSoundReceipt[])source.SoundRequests.Clone(),
            SoundProjectedX = source.SoundProjectedX?.ToArray()
        };
    }
}
