using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEffectDispatch(long Serial, long Callback, string? ActorId, BattleAbility? Ability, int? ActionId, ushort RequestedEffect, ushort ExecutedEffect, byte? NativeCommand = null);
public sealed record BattleDiagnosticNativeContact([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] long DispatchSerial, [property: JsonRequired] string SubmittedTargetId, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] string RecipientId, [property: JsonRequired] byte RecipientIndex, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] ImmutableArray<byte> HeaderPayload, [property: JsonRequired] uint HeaderUnknownByteMask, [property: JsonRequired] int? FirstContactIndex, [property: JsonRequired] int RetainedContactIndex, [property: JsonRequired] ImmutableArray<byte> ContactPayload);
public sealed record BattleDiagnosticResolution(long Frame, long Callback, string Kind, string? ActorId, string? OriginalTargetId, string ActualTargetId, int ResolvedAmount, int ActualAmount, int BeforeHp, int AfterHp, string HpOwner)
{
    public BattleDiagnosticNativeContact? NativeContact { get; init; }
}
public sealed record BattleDiagnosticStock(int MagicId, int Quantity);
public sealed record BattleDiagnosticCombatant(string Id, int CurrentHp, int MaximumHp, ushort Statuses0, uint Statuses1, BattleDiagnosticStock[] MagicStocks)
{
    public int? NativeIndex { get; init; }
}

public sealed partial class BattleSession
{
    private long diagnosticDispatchSerial;
    private BattleDiagnosticNativeContact? diagnosticNativeContact;
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
        return combatants.Select(actor => new BattleDiagnosticCombatant(actor.Id, actor.CurrentHp, actor.MaximumHp, actor.NativeStatus0, actor.NativeStatus1, actor.NativeMagicSlots.Where(stock => stock.SpellId != 0).Select(stock => new BattleDiagnosticStock(stock.SpellId, stock.Count)).ToArray()) { NativeIndex = actor.NativeIndex is >= 0 and < 7 ? actor.NativeIndex : null }).ToArray();
    }

    private void ObserveDiagnosticResolution(string kind, BattleCombatant? actor, BattleCombatant target, int amount, int before, int after)
    {
        if (DiagnosticResolution is { } observer)
        {
            BattleActionPresentation? action = resolvingNativeAction ?? ActiveAction;
            string? actorId = actor?.Id ?? action?.ActorId;
            BattleDiagnosticNativeContact? contact = diagnosticNativeContact is { } current && current.ActorId == actorId && current.RecipientId == action?.TargetId ? current : null;
            observer(new(Frame, NativeHostCallbacks ?? Frame, kind, actorId, action?.TargetId ?? target.Id, target.Id, amount, Math.Abs(after - before), before, after, target.IsPlayer ? "character" : "enemy") { NativeContact = contact });
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
        observer(new(diagnosticDispatchSerial, NativeHostCallbacks ?? graphics.Callback, actorId, ability, actionId, effect.RequestedEffect, effect.Binding.Effect, nativeCommand));
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
