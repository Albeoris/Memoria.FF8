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
public sealed record BattleDiagnosticNativeContact([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] long DispatchSerial, [property: JsonRequired] string SubmittedTargetId, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] ImmutableArray<byte> HeaderPayload, [property: JsonRequired] uint HeaderUnknownByteMask, [property: JsonRequired] int? FirstContactIndex, [property: JsonRequired] int RetainedContactIndex, [property: JsonRequired] ImmutableArray<byte> ContactPayload);
public sealed record BattleDiagnosticPeriodicStatus([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] int StatusBit, [property: JsonRequired] short TimerBefore, [property: JsonRequired] int Decrement, [property: JsonRequired] short TimerAfter, [property: JsonRequired] ushort SavedStatuses, [property: JsonRequired] uint BattleStatuses, [property: JsonRequired] int MaximumHp, [property: JsonRequired] int ResolvedAmount, [property: JsonRequired] long Callback);
public sealed record BattleDiagnosticNativeHpWord([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] int ModelId, [property: JsonRequired] int ScriptIndex, [property: JsonRequired] int ProgramCounter, [property: JsonRequired] ImmutableArray<byte> ScriptBytes, [property: JsonRequired] byte Opcode, [property: JsonRequired] ushort Operand, [property: JsonRequired] short Amount, [property: JsonRequired] ushort BeforeHpWord, [property: JsonRequired] ushort AfterHpWord, [property: JsonRequired] long Callback);
public sealed record BattleDiagnosticNativeStatusRemoval([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] byte NativeCommand, [property: JsonRequired] ushort NativeActionId, [property: JsonRequired] uint ActionStatuses1, [property: JsonRequired] uint AppliedBattleStatuses);
public sealed record BattleDiagnosticResolution(long Frame, long Callback, string Kind, string? ActorId, string? OriginalTargetId, string ActualTargetId, int ResolvedAmount, int ActualAmount, int BeforeHp, int AfterHp, string HpOwner)
{
    public BattleDiagnosticNativeContact? NativeContact { get; init; }
    public BattleDiagnosticPeriodicStatus? PeriodicStatus { get; init; }
    public BattleDiagnosticNativeHpWord? NativeHpWord { get; init; }
    public BattleDiagnosticNativeStatusRemoval? NativeStatusRemoval { get; init; }
}
public sealed record BattleDiagnosticStock(int MagicId, int Quantity);
public sealed record BattleDiagnosticCombatant(string Id, int CurrentHp, int MaximumHp, ushort Statuses0, uint Statuses1, BattleDiagnosticStock[] MagicStocks)
{
    public int? NativeIndex { get; init; }
    public int? ModelId { get; init; }
    public string[]? NativeAiScriptSha256 { get; init; }
}

public sealed partial class BattleSession
{
    private long diagnosticDispatchSerial;
    private BattleDiagnosticNativeContact? diagnosticNativeContact;
    private BattleDiagnosticPeriodicStatus? diagnosticPeriodicStatus;
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

    public void ConfigureDiagnosticScheduling(bool holdEnemyCommands, bool instantGuardianForceCharge = false, string? readinessActorId = null)
    {
        // Construction queues participant initialization before scheduling can be configured.
        if (Frame != 0 || (NativeHostCallbacks ?? 0) != 0 || ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any(command => command.NativeCallback?.Stage != 0) || guardianForceCasts.Count != 0)
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

    public BattleDiagnosticCombatant[] ObserveDiagnosticCombatants()
    {
        return combatants.Select(ObserveDiagnosticCombatant).ToArray();
    }

    private static BattleDiagnosticCombatant ObserveDiagnosticCombatant(BattleCombatant actor)
    {
        return new(actor.Id, actor.CurrentHp, actor.MaximumHp, actor.NativeStatus0, actor.NativeStatus1, actor.NativeMagicSlots.Where(stock => stock.SpellId != 0).Select(stock => new BattleDiagnosticStock(stock.SpellId, stock.Count)).ToArray()) { NativeIndex = actor.NativeIndex is >= 0 and < 7 ? actor.NativeIndex : null, ModelId = actor.ModelId, NativeAiScriptSha256 = actor.NativeAi?.Scripts.Select(script => Convert.ToHexString(SHA256.HashData(script))).ToArray() };
    }

    private void ObserveDiagnosticNativeHpWord(BattleCombatant actor, int scriptIndex, int pc, byte[] script, ushort operand, int before)
    {
        if (DiagnosticResolution is { } observer)
        {
            long callback = NativeHostCallbacks ?? Frame;
            short amount = unchecked((short)operand);
            BattleDiagnosticNativeHpWord proof = new(0x487DF0, actor.Id, checked((byte)actor.NativeIndex), actor.ModelId, scriptIndex, pc, script.ToImmutableArray(), 60, operand, amount, unchecked((ushort)before), unchecked((ushort)actor.CurrentHp), callback);
            observer(new(Frame, callback, "native_ai_hp_word", null, actor.Id, actor.Id, amount, amount, before, actor.CurrentHp, actor.IsPlayer ? "character" : "enemy") { NativeHpWord = proof });
        }
    }

    private void ObserveDiagnosticResolution(string kind, BattleCombatant? actor, BattleCombatant target, int amount, int before, int after)
    {
        if (DiagnosticResolution is { } observer)
        {
            BattleActionPresentation? action = resolvingNativeAction ?? ActiveAction;
            BattleDiagnosticPeriodicStatus? periodic = diagnosticPeriodicStatus is { } receipt && receipt.RecipientId == target.Id && receipt.ResolvedAmount == amount ? receipt : null;
            string? actorId = periodic is null ? actor?.Id ?? action?.ActorId : null;
            BattleDiagnosticNativeContact? contact = diagnosticNativeContact is { } current && current.ActorId == actorId && current.RecipientId == action?.TargetId ? current : null;
            BattleDiagnosticNativeStatusRemoval? removal = null;
            if (periodic is null && contact is null && kind == "damage" && before > amount && after == 0 && actor is not null && actor.Id == action?.ActorId && action.NativeAction is { } native && (native.Statuses1 & 0x10000) != 0 && (NativeBattleStatusBits(target) & 0x10000) != 0)
            {
                removal = new(0x494410, checked((byte)actor.NativeIndex), checked((byte)target.NativeIndex), action.NativeQueuedCommand ?? native.Command, native.ActionId, native.Statuses1, NativeBattleStatusBits(target));
            }
            observer(new(Frame, NativeHostCallbacks ?? Frame, kind, actorId, periodic is null ? action?.TargetId ?? target.Id : target.Id, target.Id, amount, Math.Abs(after - before), before, after, target.IsPlayer ? "character" : "enemy") { NativeContact = periodic is null ? contact : null, PeriodicStatus = periodic, NativeStatusRemoval = removal });
        }
    }

    private BattleDiagnosticNativeContact? ObserveDiagnosticGuardianContact(BattleActionPresentation? submitted, BattleCombatant actor, BattleCombatant recipient, NativeBattleActionHeaderSlot header, NativeBattleContactPacket retained, int contactIndex, ushort actionId)
    {
        if (DiagnosticResolution is null || submitted is null || submitted.ActorId != actor.Id || submitted.Ability != BattleAbility.GuardianForce || submitted.ActionId != actionId - 64 || submitted.NativeHeaderIndex != 0 || submitted.NativeQueuedCommand is byte queued && queued != 254 || submitted.NativeAction is { } native && (native.Command != 254 || native.ActionId != actionId) || diagnosticDispatchSerial == 0)
        {
            return null;
        }
        return new(0x4850A0, diagnosticDispatchSerial, submitted.TargetId, actor.Id, checked((byte)actor.NativeIndex), recipient.Id, checked((byte)recipient.NativeIndex), 0, header.Payload, header.UnknownByteMask, header.FirstContactIndex, contactIndex, retained.Encode().ToImmutableArray());
    }

    private sealed class DiagnosticBattleEventLog : List<BattleEvent>
    {
        internal event Action<BattleEvent>? Published;

        public new void Add(BattleEvent value)
        {
            base.Add(value);
            Published?.Invoke(value);
        }
    }

    private void ObserveDiagnosticEffectDispatch(NativeBattleGraphicsState graphics, NativeBattleOriginalEffectState effect)
    {
        diagnosticDispatchSerial++;
        if (DiagnosticEffectDispatched is not { } observer)
        {
            return;
        }
        string? actorId = effect.Parameters is { Actor78: < 7 } parameters ? graphics.ActorIds[parameters.Actor78] : graphics.ActionActorId;
        BattleActionPresentation? action = resolvingNativeAction ?? ActiveAction;
        if (action?.ActorId != actorId)
        {
            action = null;
        }
        BattleAttackCommand? bound = action is null ? nativeCommandBanks.BoundCommands.FirstOrDefault(command => command.ActorId == actorId && command.NativeAction?.MagicId == effect.RequestedEffect) : null;
        BattleAbility? ability = action?.Ability ?? bound?.Ability;
        int? actionId = action?.NativeAction?.ActionId ?? action?.ActionId ?? bound?.NativeAction?.ActionId ?? bound?.ActionId;
        byte? nativeCommand = action?.NativeAction?.Command ?? bound?.NativeAction?.Command;
        if (effect.Parameters is { HeaderIndex7C: >= 0 and < NativeBattleActionPacketsState.HeaderCapacity } entered)
        {
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[entered.HeaderIndex7C];
            actionId = header.ReadByte(4) | header.ReadByte(5) << 8;
            nativeCommand = header.ReadByte(1);
            if (actorId is not null && byId.TryGetValue(actorId, out BattleCombatant? selectedActor))
            {
                ability = ResolveDiagnosticHeaderAbility(selectedActor.IsPlayer, ability, selectedActor.NativeLimit?.Command, nativeCommand.Value);
            }
        }
        BattleAttackCommand? callbackBound = bound ?? nativeCommandBanks.BoundCommands.FirstOrDefault(command => command.ActorId == actorId && command.NativeAction?.MagicId == effect.RequestedEffect);
        byte? callbackStage = action?.NativeAiCallbackStage ?? callbackBound?.NativeAiCallbackStage;
        string? scriptSha256 = callbackStage is byte stage && actorId is not null && byId.TryGetValue(actorId, out BattleCombatant? scriptActor) && scriptActor.NativeAi is { } ai && stage < ai.Scripts.Length ? Convert.ToHexString(SHA256.HashData(ai.Scripts[stage])) : null;
        BattleDiagnosticEffectDispatch dispatch = new(diagnosticDispatchSerial, NativeHostCallbacks ?? graphics.Callback, actorId, ability, actionId, effect.RequestedEffect, effect.Binding.Effect, nativeCommand) { NativeAiCallbackStage = callbackStage, NativeAiScriptSha256 = scriptSha256, NativeTargetMask = effect.Parameters?.TargetMask7A, NativeActorIndex = effect.Parameters?.Actor78, NativeHeaderIndex = effect.Parameters?.HeaderIndex7C };
        observer(CompleteDiagnosticReflection(dispatch));
    }

    private static BattleAbility ResolveDiagnosticHeaderAbility(bool isPlayer, BattleAbility? knownAbility, byte? limitCommand, byte nativeCommand)
    {
        if (!isPlayer)
        {
            return BattleAbility.EnemyAbility;
        }
        if (knownAbility is { } known && known != BattleAbility.EnemyAbility)
        {
            // The actor-matched action or effect-matched bank binding owns semantic
            // identity. The actual header command/action above remain independently recorded.
            return known;
        }
        if (limitCommand == nativeCommand || limitCommand == 5 && nativeCommand == 250)
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

    /// <summary>Conservative normal-owner drain observation, without serialization or owner mutation.</summary>
    public IReadOnlyList<string> ObserveDiagnosticDrainBlockers()
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Diagnostic drain requires normal native graphical owners.");
        NativeBattleFileIoState files = NativeFileIo ?? throw new NotSupportedException("Diagnostic drain requires the actual file completion owner.");
        if (files.ActiveCount is null)
        {
            throw new NotSupportedException("Diagnostic drain cannot infer an unknown file scheduler count.");
        }
        List<string> blockers = [];
        if (ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any())
        {
            blockers.Add("action-or-command");
        }
        if (nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions || pendingSlotsDeal is not null || nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            blockers.Add("timed-input-or-mandatory-reaction");
        }
        if (!graphics.PendingContacts.IsEmpty || graphics.Descriptors.Queue.Length != 0 || graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0))
        {
            blockers.Add("contact-or-descriptor");
        }
        if (graphics.Effects.Head is not null || graphics.Effects.PublishedCount != 0 || graphics.Auxiliary.Head is not null || graphics.ModuleEffects.Head is not null)
        {
            blockers.Add("effect-or-auxiliary-task");
        }
        if (files.ActiveCount != 0 || NativeOriginalEffectBusy(graphics))
        {
            blockers.Add("file-camera-sound-or-effect-resource");
        }
        return blockers;
    }
}
