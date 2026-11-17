using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEffectDispatch(long Serial, long Callback, string? ActorId, BattleAbility? Ability, int? ActionId, ushort RequestedEffect, ushort ExecutedEffect, byte? NativeCommand = null)
{
    public byte? NativeAiCallbackStage { get; init; }
    public string? NativeAiScriptSha256 { get; init; }
    public ushort? NativeTargetMask { get; init; }
    public byte? NativeActorIndex { get; init; }
    public BattleRosterSlot? ActorSlot { get; init; }
    public BattleActionHeaderReference? HeaderReference { get; init; }
    public ImmutableArray<string> ContactActorIds { get; init; } = [];
    public int? NativeHeaderIndex { get; init; }
    public long? NativeHeaderGeneration { get; init; }
    public BattleDiagnosticReflection? Reflection { get; init; }
    public string? ReflectionRejectionReason { get; init; }
    public BattleDiagnosticReflectionRejection? ReflectionRejection { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticSupportingInvocation([property: JsonRequired] string ActorId, [property: JsonRequired] byte NativeCommand, [property: JsonRequired] ushort ActionId, [property: JsonRequired] ushort EffectId, [property: JsonRequired] byte CallbackStage, [property: JsonRequired] string ScriptSha256)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActorId) || ActorId.Length > 128 || NativeCommand == 0 || ActionId == 0 || EffectId == 0 || CallbackStage is not (2 or 3) || ScriptSha256 is null || ScriptSha256.Length != 64 || ScriptSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Diagnostic supporting invocation requires an explicit actor, positive native command/action/effect identities, reaction callback2 or3 and its actual script SHA256.");
        }
    }
}
public sealed record BattleDiagnosticNativeContact([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] long DispatchSerial, [property: JsonRequired] string SubmittedTargetId, [property: JsonRequired] string ActorId, [property: JsonRequired] byte? ActorIndex, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte? RecipientIndex, [property: JsonRequired] int? HeaderIndex, [property: JsonRequired] ImmutableArray<byte>? HeaderPayload, [property: JsonRequired] uint? HeaderUnknownByteMask, [property: JsonRequired] int? FirstContactIndex, [property: JsonRequired] int? RetainedContactIndex, [property: JsonRequired] ImmutableArray<byte>? ContactPayload)
{
    public BattleRosterSlot? ActorSlot { get; init; }
    public BattleRosterSlot? RecipientSlot { get; init; }
    public BattleActionHeaderReference? HeaderReference { get; init; }
    public long HeaderGeneration { get; init; }
    public BattleContactOccurrence? Occurrence { get; init; }
    public int FirstContactOrdinal { get; init; }
    public int ContactCount { get; init; }
}
public sealed record BattleDiagnosticPeriodicStatus([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte? OriginalRecipientOperand, [property: JsonRequired] int StatusBit, [property: JsonRequired] short TimerBefore, [property: JsonRequired] int Decrement, [property: JsonRequired] short TimerAfter, [property: JsonRequired] ushort SavedStatuses, [property: JsonRequired] uint BattleStatuses, [property: JsonRequired] int MaximumHp, [property: JsonRequired] int ResolvedAmount, [property: JsonRequired] long Callback);
public sealed record BattleDiagnosticNativeHpWord([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte? RecipientIndex, [property: JsonRequired] int ModelId, [property: JsonRequired] int ScriptIndex, [property: JsonRequired] int ProgramCounter, [property: JsonRequired] ImmutableArray<byte> ScriptBytes, [property: JsonRequired] byte Opcode, [property: JsonRequired] ushort Operand, [property: JsonRequired] short Amount, [property: JsonRequired] ushort BeforeHpWord, [property: JsonRequired] ushort AfterHpWord, [property: JsonRequired] long Callback)
{
    public BattleRosterSlot? RecipientSlot { get; init; }
}
public sealed record BattleDiagnosticNativeStatusRemoval([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] byte? ActorIndex, [property: JsonRequired] byte? RecipientIndex, [property: JsonRequired] byte NativeCommand, [property: JsonRequired] ushort NativeActionId, [property: JsonRequired] uint ActionStatuses1, [property: JsonRequired] uint AppliedBattleStatuses)
{
    public BattleRosterSlot? ActorSlot { get; init; }
    public BattleRosterSlot? RecipientSlot { get; init; }
}
public sealed record BattleDiagnosticResolution(long Frame, long Callback, string Kind, string? ActorId, string? OriginalTargetId, string ActualTargetId, int ResolvedAmount, int ActualAmount, int BeforeHp, int AfterHp, string HpOwner)
{
    public BattleDiagnosticNativeContact? NativeContact { get; init; }
    public BattleDiagnosticPeriodicStatus? PeriodicStatus { get; init; }
    public BattleDiagnosticNativeHpWord? NativeHpWord { get; init; }
    public BattleDiagnosticNativeStatusRemoval? NativeStatusRemoval { get; init; }
    public BattleDiagnosticRepairProjection? DiagnosticRepair { get; init; }
}
public sealed record BattleDiagnosticStock(int MagicId, int Quantity);
public sealed record BattleDiagnosticCombatant(string Id, int CurrentHp, int MaximumHp, ushort Statuses0, uint Statuses1, BattleDiagnosticStock[] MagicStocks)
{
    public int? NativeIndex { get; init; }
    public BattleRosterSlot? Slot { get; init; }
    public int? ModelId { get; init; }
    public string[]? NativeAiScriptSha256 { get; init; }
}

public sealed partial class BattleSession
{
    private long diagnosticDispatchSerial;
    private List<BattleDiagnosticResolution>? preparedResolutionObservations;
    public Guid DiagnosticLifecycleId { get; } = Guid.NewGuid();
    public long DiagnosticRestoreCount { get; private set; }
    public long DiagnosticResetCount { get; private set; }
    public event Action<BattleDiagnosticEffectDispatch>? DiagnosticEffectDispatched;
    public event Action<BattleDiagnosticResolution>? DiagnosticResolution;
    public event Action<BattleEvent>? DiagnosticEventPublished
    {
        add => events.Published += value;
        remove => events.Published -= value;
    }
    private bool diagnosticHoldEnemyCommands;
    private bool diagnosticInstantGuardianForceCharge;
    private BattleCombatant? diagnosticReadinessActor;

    private void ObservePresentationEffect(BattlePresentationProcess process, BattlePresentationEffectObservation observation)
    {
        diagnosticDispatchSerial++;
        if (DiagnosticEffectDispatched is not { } observer)
        {
            return;
        }
        string? actorId = observation.ActorId;
        BattleActionPresentation? action = resolvingNativeAction ?? ActiveAction;
        if (action?.ActorId != actorId || observation.Header is not { } observedHeader || action?.NativeAction is not { } sourceAction ||
            sourceAction.Command != observedHeader.Command || sourceAction.ActionId != observedHeader.Action ||
            sourceAction.MagicId != observedHeader.Effect || observedHeader.Effect != observation.Effect.RequestedEffect)
        {
            action = null;
        }
        BattleNativeAction? native = action?.NativeAction;
        BattleAbility? ability = action?.Ability;
        int? actionId = observation.Header?.Action ?? native?.ActionId ?? action?.ActionId;
        byte? command = observation.Header?.Command ?? native?.Command;
        byte? callbackStage = action?.NativeAiCallbackStage;
        ExpandedHostedInvocation? cast = ReadDiagnosticBoundCast(CurrentExpandedHostedInvocation, observation.Effect.Parameters?.Header);
        if ((cast ?? CurrentExpandedHostedInvocation) is { } invocation && invocation.Command.ActorId == actorId &&
            invocation.Header.Command == command && invocation.Header.Action == actionId && invocation.Header.Effect == observation.Effect.RequestedEffect)
        {
            ability ??= invocation.Command.Ability;
            callbackStage = invocation.Command.NativeAiCallbackStage ?? callbackStage;
        }
        BattleCombatant? source = actorId is null ? null : byId.GetValueOrDefault(actorId);
        if (source is not null && command is byte nativeCommand)
        {
            ability = ResolveDiagnosticHeaderAbility(source, ability, nativeCommand);
        }
        string? scriptSha256 = callbackStage is byte stage && source?.NativeAi is { } ai && stage < ai.Scripts.Length
            ? Convert.ToHexString(SHA256.HashData(ai.Scripts[stage]))
            : null;
        BattleDiagnosticEffectDispatch dispatch = new(diagnosticDispatchSerial, NativeHostCallbacks ?? observation.Callback, actorId, ability, actionId, observation.Effect.RequestedEffect, observation.Effect.Binding.Effect, command)
        {
            NativeAiCallbackStage = callbackStage,
            NativeAiScriptSha256 = scriptSha256,
            NativeTargetMask = observation.OriginalTargets,
            NativeActorIndex = observation.OriginalActor,
            ActorSlot = source?.Slot,
            HeaderReference = observation.Effect.Parameters?.Header,
            ContactActorIds = observation.Header?.ContactCount is not null && observation.Effect.Parameters is { } parameters ? ReadDiagnosticContactActorIds(process.ActionPackets, parameters.Header) : [],
            NativeHeaderIndex = observation.OriginalHeader,
            NativeHeaderGeneration = observation.HeaderGeneration
        };
        dispatch = CompleteDiagnosticReflection(process, observation, cast, dispatch);
        if (DiagnosticResolution is not null && dispatch is { NativeCommand: 254, HeaderReference: not null, NativeHeaderGeneration: > 0 })
        {
            diagnosticGuardianDispatch = dispatch;
        }
        ObserveDiagnosticInvocationDispatch(dispatch);
        process.PublishDiagnosticEnteredEffect(observer, dispatch, observation.Effect);
    }

    private static BattleAbility ResolveDiagnosticHeaderAbility(BattleCombatant actor, BattleAbility? knownAbility, byte nativeCommand)
    {
        if (!actor.IsPlayer)
        {
            return BattleAbility.EnemyAbility;
        }
        byte? limit = actor.NativeLimit?.Command;
        if (limit == 14 && nativeCommand is 237 or 238)
        {
            return BattleAbility.NativeCommand;
        }
        if (knownAbility is { } known && known != BattleAbility.EnemyAbility)
        {
            return known;
        }
        if (limit == nativeCommand || limit == 5 && nativeCommand == 250)
        {
            return BattleAbility.LimitBreak;
        }
        return nativeCommand switch
        {
            1 => BattleAbility.Attack,
            2 => BattleAbility.Magic,
            4 or 13 or 244 => BattleAbility.Item,
            6 => BattleAbility.Draw,
            254 => BattleAbility.GuardianForce,
            _ => BattleAbility.NativeCommand
        };
    }

    public void ConfigureDiagnosticScheduling(bool holdEnemyCommands, bool instantGuardianForceCharge = false, string? readinessActorId = null, bool observeAutomaticSupport = false)
    {
        // Construction queues participant initialization before scheduling can be configured.
        if (Frame != 0 || (NativeHostCallbacks ?? 0) != 0 || ActiveAction is not null || actions.Any(action => action.ExpandedAiCallback?.Stage != 0 && (!observeAutomaticSupport || !IsExpandedAutomaticGuardian(action.NativeAction, action.NativePlayerReaction))) || guardianForceCasts.Count != 0)
        {
            throw new InvalidOperationException("Diagnostic scheduling must be declared before the first battle callback or command.");
        }
        BattleCombatant? readinessActor = null;
        if (readinessActorId is not null && !byId.TryGetValue(readinessActorId, out readinessActor))
        {
            throw new ArgumentException("Diagnostic readiness requires an actor in this battle.", nameof(readinessActorId));
        }
        diagnosticReadinessActor = readinessActor;
        diagnosticHoldEnemyCommands = holdEnemyCommands;
        diagnosticInstantGuardianForceCharge = instantGuardianForceCharge;
    }

    /// <summary>Hands a declared player's diagnostic readiness wait to an initialized enemy at the existing idle TurnBased boundary.</summary>
    public bool TryHandoffDiagnosticReadinessActor(string expectedPreviousId, string nextId)
    {
        if (Mode != BattleAtbMode.TurnBased || !diagnosticHoldEnemyCommands || string.IsNullOrWhiteSpace(expectedPreviousId) || string.IsNullOrWhiteSpace(nextId) || !byId.TryGetValue(expectedPreviousId, out BattleCombatant? previous) || !ReferenceEquals(previous, diagnosticReadinessActor) || !previous.IsPlayer || !byId.TryGetValue(nextId, out BattleCombatant? next))
        {
            return false;
        }
        if (!CanAdvanceTurnBasedTime || turnAdvance is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            return false;
        }
        if (next.IsPlayer || next.Slot is not { Side: BattleSide.Enemies } || next.NativeAi is null || next.ResolvedNativeLevel is null || next.NativeAllocationPendingStatistics || (next.NativeCombatantFlags & 0xC3) != 1 || next.IsBusy || !CanNativeBattleActorAct(next))
        {
            return false;
        }
        diagnosticReadinessActor = next;
        return true;
    }

    public BattleDiagnosticCombatant[] ObserveDiagnosticCombatants()
    {
        return combatants.Select(ObserveDiagnosticCombatant).ToArray();
    }

    private static BattleDiagnosticCombatant ObserveDiagnosticCombatant(BattleCombatant actor)
    {
        return new(actor.Id, actor.CurrentHp, actor.MaximumHp, actor.NativeStatus0, actor.NativeStatus1, actor.NativeMagicSlots.Where(stock => stock.SpellId != 0).Select(stock => new BattleDiagnosticStock(stock.SpellId, stock.Count)).ToArray()) { NativeIndex = actor.NativeActorIndex, Slot = actor.Slot, ModelId = actor.ModelId, NativeAiScriptSha256 = actor.NativeAi?.Scripts.Select(script => Convert.ToHexString(SHA256.HashData(script))).ToArray() };
    }

    private void ObserveDiagnosticNativeHpWord(BattleCombatant actor, int scriptIndex, int pc, byte[] script, ushort operand, int before)
    {
        if ((DiagnosticResolution is not null || preparedResolutionObservations is not null))
        {
            long callback = NativeHostCallbacks ?? Frame;
            short amount = unchecked((short)operand);
            BattleDiagnosticNativeHpWord proof = new(0x487DF0, actor.Id, ReadDiagnosticOriginalOperand(actor), actor.ModelId, scriptIndex, pc, script.ToImmutableArray(), 60, operand, amount, unchecked((ushort)before), unchecked((ushort)actor.CurrentHp), callback) { RecipientSlot = actor.Slot };
            PublishDiagnosticResolution(new(Frame, callback, "native_ai_hp_word", null, actor.Id, actor.Id, amount, amount, before, actor.CurrentHp, actor.IsPlayer ? "character" : "enemy") { NativeHpWord = proof });
        }
    }

    private void ObserveDiagnosticResolution(string kind, BattleCombatant? actor, BattleCombatant target, int amount, int before, int after)
    {
        ObserveDiagnosticCounterResolution(actor, target, before, after);
        if (DiagnosticResolution is null && preparedResolutionObservations is null || nativeContactWorking?.DiagnosticPreliminary == true)
        {
            return;
        }
        BattleActionPresentation? action = resolvingNativeAction ?? ActiveAction;
        if (action?.DiagnosticDoomExpiration?.RecipientId == target.Id)
        {
            // The Doom receipt carries the actual graphical death-contact occurrence.
            return;
        }
        string? actorId = actor?.Id ?? action?.ActorId;
        BattleDiagnosticNativeStatusRemoval? removal = null;
        if (kind == "damage" && before > amount && after == 0 && actor is not null &&
            actor.Id == action?.ActorId && action.NativeAction is { } native && (native.Statuses1 & 0x10000) != 0 && (NativeBattleStatusBits(target) & 0x10000) != 0)
        {
            removal = new(0x494410, ReadDiagnosticOriginalOperand(actor), ReadDiagnosticOriginalOperand(target), action.NativeQueuedCommand ?? native.Command, native.ActionId, native.Statuses1, NativeBattleStatusBits(target)) { ActorSlot = actor.Slot, RecipientSlot = target.Slot };
        }
        BattleDiagnosticRepairProjection? repair = diagnosticRepairProjection is { } projection && projection.Request.ActorId == target.Id && projection.RecipientSlot == target.Slot && projection.RecipientIndex == ReadDiagnosticOriginalOperand(target) && projection.Frame == Frame && projection.Callback == (NativeHostCallbacks ?? Frame) && nativeContactWorking is null ? projection : null;
        BattleDiagnosticNativeContact? contact = diagnosticGuardianContact is { } witness && witness.RecipientId == target.Id && nativeContactWorking is { CommittingSecondary: false } working &&
            working.Actor.Id == witness.ActorId && action?.NativeAction is { Command: 254 } ? witness : null;
        PublishDiagnosticResolution(new(Frame, NativeHostCallbacks ?? Frame, kind, actorId, contact?.RecipientId ?? action?.TargetId ?? target.Id, target.Id, amount, Math.Abs(after - before), before, after, target.IsPlayer ? "character" : "enemy")
        {
            NativeContact = contact,
            NativeStatusRemoval = removal,
            DiagnosticRepair = repair
        });
    }

    private static byte? ReadDiagnosticOriginalOperand(BattleCombatant actor)
    {
        return actor.NativeActorIndex is int index ? checked((byte)index) : null;
    }

    private void PublishDiagnosticResolution(BattleDiagnosticResolution resolution)
    {
        if (preparedResolutionObservations is { } prepared)
        {
            prepared.Add(resolution);
        }
        else
        {
            DiagnosticResolution?.Invoke(resolution);
        }
    }

    private BattleDiagnosticPeriodicStatus? CaptureDiagnosticDoomExpiration(BattleCombatant actor)
    {
        return DiagnosticResolution is not null
            ? new(0x483470, actor.Id, actor.NativeActorIndex is int index ? checked((byte)index) : null, 10, actor.NativeStatusTimers[10], 0, -1111, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), actor.MaximumHp, actor.CurrentHp, NativeHostCallbacks ?? Frame)
            : null;
    }

    private void ObserveDiagnosticDoomContact(BattleActionPresentation action, string recipient, NativeBattleContactHalf contact)
    {
        if (DiagnosticResolution is not { } observer || action.DiagnosticDoomExpiration is not { ResolvedAmount: > 0 } proof ||
            action.NativeAction is not { Command: 246, ActionId: 43, MagicId: 9 } || recipient != proof.RecipientId ||
            (contact.SavedStatuses4 & 1) == 0 || GetActor(recipient) is not { CurrentHp: 0 } target)
        {
            return;
        }
        long callback = NativeHostCallbacks ?? Frame;
        observer(new(Frame, callback, "damage", null, recipient, recipient, proof.ResolvedAmount, proof.ResolvedAmount, proof.ResolvedAmount, 0, target.IsPlayer ? "character" : "enemy")
        {
            PeriodicStatus = proof with { Callback = callback }
        });
    }

    private sealed class DiagnosticBattleEventLog : List<BattleEvent>
    {
        internal event Action<BattleEvent>? Published;

        public new void Add(BattleEvent value)
        {
            base.Add(value);
            Published?.Invoke(value);
        }

        internal void PublishCommitted(BattleEvent[] suffix)
        {
            base.AddRange(suffix);
            if (Published is { } observer)
            {
                foreach (BattleEvent value in suffix)
                {
                    observer(value);
                }
            }
        }
    }

    /// <summary>Conservative normal-owner drain observation, without serialization or owner mutation.</summary>
    public IReadOnlyList<string> ObserveDiagnosticDrainBlockers()
    {
        List<string> blockers = [];
        if (ActiveAction is not null || actions.Count != 0)
        {
            blockers.Add("action-or-command");
        }
        if (nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            blockers.Add("timed-input-or-mandatory-reaction");
        }
        (Presentation ?? throw new InvalidOperationException("Diagnostic drain requires its current presentation process.")).AppendDiagnosticPresentationBlockers(blockers);
        return blockers;
    }
}
