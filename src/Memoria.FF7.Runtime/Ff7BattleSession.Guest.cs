using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestInvocation(long RequestId, int FormationId, long StartedAt, int ActorSlot, int TargetSlot)
{
    public ushort TargetMask { get; init; }
    public ushort GuestAbility { get; init; }
    public byte? GuestCommand { get; init; }
    public bool Reflected { get; init; }
}
public sealed record Ff7BattleGuestAttackTarget(int Slot, SceneBattleEnemy? Native, Ff7BattleAiActor Actor, Ff7BattlePlacement Placement, ushort CollisionRadius)
{
    public int JointCount { get; init; }
    public ushort OriginSavedStatuses { get; init; }
}
public sealed record Ff7BattleGuestPhysicalHit(Ff7BattleDamageActor Source, Ff7BattleDamageAction Attack, ushort Elements)
{
    public Ff7BattleGuestPreparedContact? PreparedContact { get; init; }
    public Ff7BattleGuestItemEffect? ItemEffect { get; init; }
    public Ff7BattleGuestProcessOperation? ProcessOperation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public byte? ElementPercent { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public Ff7BattleAttack? StatusRecord { get; init; }
}
public sealed record Ff7BattleGuestPreparedContact(Ff7BattleGuestInvocation Invocation, Ff7BattleActionVitals SourceBefore, Ff7BattleActionVitals TargetBefore, Ff7BattleActionReceipt Receipt);
public sealed record Ff7BattleGuestContactContext(Ff7BattleDamageActor Source, Ff7BattleActionVitals SourceVitals, Ff7BattleDamageActor Target, Ff7BattleActionVitals TargetVitals, Ff7BattleDamageAction Attack, ReadOnlyMemory<byte> RandomKernel);
public sealed record Ff7BattleGuestAbilityFailure(long Tick, int ActorSlot, ushort Ability, string Reason);
public sealed record Ff7BattleGuestAttackUpdate(Ff7BattleGuestPhysicalHit? Hit, bool Completed)
{
    public IReadOnlyList<int> CompositeContacts { get; init; } = [];
    public IReadOnlyList<Ff7BattleGuestPhysicalHit> GroupHits { get; init; } = [];
    public byte? AttackTargetCue { get; init; }
    public int? LimitPresentedContact { get; init; }
    public byte? LimitTargetCue { get; init; }
}

/// <summary>Owns one origin-specific physical presentation and its immutable contact description.</summary>
public interface IFf7BattleGuestAttack
{
    int ActorSlot { get; }
    Guid EncounterId { get; }
    Ff7BattleGuestInvocation? Invocation { get; }
    long HostTick { get; }
    bool ContactPublished { get; }
    bool Supports(Ff7BattleGuestAttackTarget target);
    void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target);
    Ff7BattleGuestAttackUpdate Advance(long hostTick);
    void AdvanceIdle(long hostTick, uint statuses, long? reactionTick);
}

public interface IFf7BattleGuestCamera
{
    float ViewTranslationY { get; }
    SceneCameraPlacement? LeasedCamera => null;
}

/// <summary>Explicit FFVIII-origin spell IDs and their origin-owned stock, independent of host spell records.</summary>
public interface IFf7BattleGuestMagic
{
    bool SupportsMagicSequence(ushort ability) => MagicIds.Contains(ability);
    IReadOnlyList<int> MagicCastCounts(ushort ability) => [1];
    int MagicCastCost(ushort ability, int casts) => casts;
    bool CanRepeatMagic(ushort ability, int ordinal) => ordinal == 1;
    bool PrepareSource(ushort ability, Func<byte> nextRandomByte, out string? failure)
    {
        failure = null;
        return true;
    }
    IReadOnlyList<ushort> MagicIds => [];
    string? PrepareMagicUnsupportedReason(Ff7BattleGuestPhysicalHit prepared) => null;
    int MagicStock(ushort ability);
    bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target);
    Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability);
    void PrepareMagic(Ff7BattleGuestPhysicalHit prepared);
}

public interface IFf7BattleGuestOperation
{
    bool HandlesOperation(ushort ability);
    string ExecuteOperation(ushort ability, int targetSlot, Func<byte> nextRandomByte);
}

public sealed partial class Ff7BattleSession
{
    private readonly IFf7BattleGuestAttack?[] guestAttacks = new IFf7BattleGuestAttack?[3];
    public Ff7BattleGuestAbilityFailure? GuestAbilityFailure { get; private set; }

    private IFf7BattleGuestAttack? GetGuestAttack(int slot) => (uint)slot < guestAttacks.Length ? guestAttacks[slot] : null;

    public void BindGuestAttack(IFf7BattleGuestAttack guest)
    {
        ArgumentNullException.ThrowIfNull(guest);
        bool hasProjectedGuests = state.GuestSlots is not null || state.GuestVitals is not null;
        if (GetGuestAttack(guest.ActorSlot) is not null || animations is null || guest.ActorSlot is < 0 or >= 3 || !state.Actors.Any(actor => actor.Actor.Slot == guest.ActorSlot) || (hasProjectedGuests ? GetGuestBinding(guest.ActorSlot)?.EncounterId != guest.EncounterId : guestAttacks.Any(current => current is not null)))
        {
            throw new InvalidOperationException("A guest attack requires one occupied host slot and original battle geometry.");
        }
        if (state.Action is { Command.GuestAttack: true } active && active.Command.ActorSlot == guest.ActorSlot)
        {
            Ff7BattleCommandQueue.ValidateCommand(active.Command);
            if (active is { Command.GroupTarget: true, GuestComposite: null } && guest is IFf7BattleGuestGroupMagic { PreparedGroupContacts.Count: > 0 } group)
            {
                System.Collections.Immutable.ImmutableArray<Ff7BattleGuestCompositeContact> contacts = [.. group.PreparedGroupContacts.Select((hit, index) => new Ff7BattleGuestCompositeContact(index, 0, 0, hit))];
                int next = BitOperations.PopCount(active.GuestAppliedTargets);
                if (next > contacts.Length || contacts.Take(next).Aggregate(0, (mask, contact) => mask | (1 << contact.HitDescription.PreparedContact!.Invocation.TargetSlot)) != active.GuestAppliedTargets)
                {
                    throw new InvalidDataException("The retained group receipts conflict with their saved contact prefix.");
                }
                active = active with { GuestComposite = new(contacts, next) { GroupPublication = true } };
                state = state with { Action = active };
            }
            bool matches = active.GuestComposite is { } compositeState
                ? (compositeState.GroupPublication ? guest is IFf7BattleGuestGroupMagic : guest is IFf7BattleGuestCompositeActions) && guest.Invocation == GuestInvocation(active.Command, active.StartedAt) && guest.ContactPublished == (compositeState.NextContact != 0)
                : active.GuestLimit is { } limitState
                ? guest is IFf7BattleGuestLimit limit && (limitState.PresentationCompleted ? guest.Invocation is null : guest.Invocation == GuestInvocation(active.Command, active.StartedAt) && limit.PublishedContacts == limitState.Contacts)
                : guest.Invocation == GuestInvocation(active.Command, active.StartedAt) && guest.ContactPublished == active.Applied;
            if (!matches || guest.HostTick != Tick)
            {
                throw new InvalidDataException("The guest presentation does not match the saved host action occurrence.");
            }
        }
        else if (guest.Invocation is not null)
        {
            throw new InvalidDataException("The guest presentation has no active host action occurrence.");
        }
        if (!hasProjectedGuests && (state.Action is { Command.GuestAttack: true } other && other.Command.ActorSlot != guest.ActorSlot || state.Queue.Ready.Any(command => command is { GuestAttack: true } && command.ActorSlot != guest.ActorSlot) || state.Queue.Slots.Any(entry => entry?.Command is { GuestAttack: true } command && command.ActorSlot != guest.ActorSlot)))
        {
            throw new InvalidDataException("The saved guest queue belongs to a different host slot.");
        }
        guestAttacks[guest.ActorSlot] = guest;
        state = state with { GuestPresentationAttached = true };
    }

    public bool SubmitGuestAttack(int slot, int targetSlot)
        => SubmitGuestAbility(slot, targetSlot, 0);

    public bool SubmitGuestMagic(int slot, int targetSlot, ushort ability = 1)
        => targetSlot is >= 0 and <= 9 && targetSlot != 3 && Submit(slot, Ff7BattleCommand.Magic, ability, (ushort)(1 << targetSlot));

    private bool SubmitGuestAbility(int slot, int targetSlot, ushort ability)
    {
        if (Outcome != Ff7BattleOutcome.Fighting || GetGuestAttack(slot) is not { } guest || ReadyPlayer != slot || !CanAct(Actor(slot)) || targetSlot is < 0 or > 9 || targetSlot == 3 || !state.Actors.Any(actor => actor.Actor.Slot == targetSlot && GuestMagicTargetIsLegal(guest, ability, actor)))
        {
            return false;
        }
        if (!SupportsGuestAbility(guest, ability, DescribeGuestTarget(targetSlot)) || GuestAbilityRequiresMagic(guest, ability) && !Ff7GuestBattleStatuses.CanCastMagic(Actor(slot).Actor.Statuses))
        {
            return false;
        }
        Ff7BattleActorState actor = Actor(slot);
        state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, ability == 0 ? (byte)Ff7BattleCommand.Attack : (byte)Ff7BattleCommand.Magic, argument: ability, targetMask: (ushort)(1 << targetSlot), guestAttack: true, guestAbility: ability) };
        Replace(actor with { AwaitingTurn = false });
        return true;
    }

    private bool TryDispatchGuestAttack(Ff7BattleQueuedCommand command)
    {
        if (!command.GuestAttack)
        {
            return false;
        }
        IFf7BattleGuestAttack guest = RequireGuestAttack(command);
        if (!AdmitsGuestMagicSequence(command, guest))
        {
            FinishTurn(command.ActorSlot);
            return true;
        }
        if (command.CompelledAttack)
        {
            ushort selected = Ff7NativeBattleTargets.Resolve(command.ActorSlot, 0, party[command.ActorSlot]!.Value.AttackTargetFlags, ushort.MaxValue, false, CompelledTargetMasks(), random, Actor(command.ActorSlot).Actor.Statuses, command.CommandId);
            if (selected == 0)
            {
                FinishTurn(command.ActorSlot);
                return true;
            }
            command = command with { TargetMask = selected, CompelledAttack = false };
        }
        command = command with { TargetMask = RedirectNativeSuccessorTargets(command.TargetMask, Mask(state.Actors.Where(actor => (actor.Actor.Conditions & 8) != 0))) };
        Ff7BattleGuestInvocation invocation = GuestInvocation(command, Tick);
        if (DispatchGuestPresentationOnly(command, guest, invocation))
        {
            return true;
        }
        if (command.CommandId == (byte)Ff7BattleCommand.Magic && !(guest is IFf7BattleGuestOperation operationOwner && operationOwner.HandlesOperation(command.GuestAbility)) && guest is IFf7BattleGuestCompositeActions composite && composite.DescribeCompositeAction(command.GuestAbility) is { } program)
        {
            DispatchGuestCompositeAction(command, guest, composite, program, invocation);
            return true;
        }
        if (command.CommandId == (byte)Ff7BattleCommand.Magic && command.GroupTarget)
        {
            DispatchGuestGroupMagic(command, guest, invocation);
            return true;
        }
        Ff7BattleActorState target = Actor(invocation.TargetSlot);
        IFf7BattleGuestItems? items = command.CommandId == (byte)Ff7BattleCommand.Item ? guest as IFf7BattleGuestItems ?? throw new InvalidDataException("The guest item lost its origin inventory owner.") : null;
        bool usable = items is null ? GuestMagicTargetIsLegal(guest, command.GuestAbility, target) : GuestItemTarget(items, command.GuestAbility, target);
        if (!CanAct(Actor(command.ActorSlot)) || !usable)
        {
            items?.CancelItem(invocation.TargetSlot, command.GuestAbility);
            FinishTurn(command.ActorSlot);
            return true;
        }
        Ff7BattleGuestAttackTarget targetDescription = DescribeGuestTarget(invocation.TargetSlot);
        if (command.CommandId == (byte)Ff7BattleCommand.Limit)
        {
            if (guest is not IFf7BattleGuestLimit { LimitReady: true } limit || !GuestLimitTarget(limit, target))
            {
                FinishTurn(command.ActorSlot);
                return true;
            }
            guest.Begin(invocation, VisualPlacement(command.ActorSlot), targetDescription);
            state = state with { Action = new(command, Tick, -1) { GuestLimit = new() }, GuestAttackUsed = true };
            return true;
        }
        if (items is not null)
        {
            if (PrepareGuestItem(items, invocation, target) is { } reason)
            {
                items.CancelItem(invocation.TargetSlot, command.GuestAbility);
                RejectGuestAbility(command, reason);
                return true;
            }
        }
        else if (!SupportsGuestAbility(guest, command.GuestAbility, targetDescription) || GuestAbilityRequiresMagic(guest, command.GuestAbility) && !Ff7GuestBattleStatuses.CanCastMagic(Actor(command.ActorSlot).Actor.Statuses))
        {
            FinishTurn(command.ActorSlot);
            return true;
        }
        else if (guest is IFf7BattleGuestOperation operation && operation.HandlesOperation(command.GuestAbility))
        {
            string message = operation.ExecuteOperation(command.GuestAbility, invocation.TargetSlot, random.NextByte);
            PublishGuestSourceInteraction(command, invocation.TargetSlot);
            FlushFormationAi();
            state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, message) };
            FinishTurn(command.ActorSlot);
            return true;
        }
        else if (command.GuestAbility != 0)
        {
            IFf7BattleGuestMagic magic = (IFf7BattleGuestMagic)guest;
            if (!magic.PrepareSource(command.GuestAbility, random.NextByte, out string? failure))
            {
                state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, failure ?? "Draw failed.") };
                FinishTurn(command.ActorSlot);
                return true;
            }
            Ff7BattleGuestPhysicalHit hit = magic.DescribeMagic(command.GuestAbility);
            hit = hit with { Attack = ApplyMagicSequenceDamage(command, hit.Attack) };
            Ff7BattleActorState source = Actor(command.ActorSlot);
            Ff7BattleActionVitals sourceVitals = GuestItemVitals(source);
            Ff7BattleActionVitals targetVitals = GuestItemVitals(target);
            ValidateGuestMagic(command.GuestAbility, hit);
            Ff7BattleDamageAction attack = ProjectGuestOutgoingAttack(invocation.TargetSlot, hit);
            Ff7BattleRng candidateRandom = new(kernel[2].Data.Span, random.Capture());
            ReflectionPlan? reflection = PrepareReflections(command, hit, candidateRandom, true);
            Ff7BattleActionReceipt receipt;
            if (reflection is not null)
            {
                if (reflection.Contacts.Count != 1)
                {
                    RejectGuestAbility(command, "The reflected action requires one representable target occurrence.");
                    return true;
                }
                Ff7BattleReflectedContact contact = reflection.Contacts[0];
                command = command with { TargetMask = (ushort)(1 << contact.TargetSlot), Reflected = true };
                invocation = GuestInvocation(command, Tick);
                target = Actor(invocation.TargetSlot);
                targetDescription = DescribeGuestTarget(invocation.TargetSlot);
                targetVitals = GuestItemVitals(target) with { StatusImmunities = (invocation.TargetSlot >= 4 ? ~Enemy(invocation.TargetSlot).NativeStatusMask : 0) | target.StatusProtection };
                attack = contact.Attack;
                receipt = contact.Receipt;
            }
            else
            {
                receipt = Ff7NativeBattleActionResolver.ResolveGuestContact(hit.Source, sourceVitals, Stats(target), targetVitals, attack, hit.StatusRecord, candidateRandom, sameActor: command.ActorSlot == invocation.TargetSlot, originStatusResistances: GetGuestProjection(invocation.TargetSlot)?.OriginStatusResistances, originBattleStatuses: GetOriginBattleStatuses(invocation.TargetSlot), originSourceStatuses: GetOriginSavedStatuses(command.ActorSlot), originTargetStatuses: GetOriginSavedStatuses(invocation.TargetSlot));
            }
            Ff7BattleGuestPhysicalHit preparedHit = hit with { Attack = attack, PreparedContact = new(invocation, sourceVitals, targetVitals, receipt) };
            if (magic.PrepareMagicUnsupportedReason(preparedHit) is { } reason)
            {
                RejectGuestAbility(command, reason);
                return true;
            }
            random.Adopt(candidateRandom);
            magic.PrepareMagic(preparedHit);
            if (reflection is not null)
            {
                CommitReflections(reflection);
            }
        }
        guest.Begin(invocation, VisualPlacement(command.ActorSlot), targetDescription);
        state = state with { Action = new(command, Tick, -1), GuestAttackUsed = true };
        return true;
    }

    private void RejectGuestAbility(Ff7BattleQueuedCommand command, string reason)
    {
        CancelGuestMagicSequence(command.ActorSlot);
        Ff7BattleActorState source = Actor(command.ActorSlot);
        Replace(source with { AwaitingTurn = true });
        GuestAbilityFailure = new(Tick, command.ActorSlot, command.GuestAbility, reason);
    }

    private void AdvanceGuestAttack()
    {
        Ff7BattleActionState? active = state.Action is { Command.GuestAttack: true } action ? action : null;
        if (active is not null && active.StartedAt != Tick)
        {
            AdvanceActiveGuestAttack(active);
        }
        foreach (IFf7BattleGuestAttack? guest in guestAttacks)
        {
            if (guest is not null && guest.ActorSlot != active?.Command.ActorSlot)
            {
                AdvanceGuestIdle(guest);
            }
        }
    }

    private void AdvanceActiveGuestAttack(Ff7BattleActionState active)
    {
        IFf7BattleGuestAttack guest = RequireGuestAttack(active.Command);
        if (active.GuestPresentationOnly)
        {
            AdvanceGuestPresentationOnly(active, guest);
            return;
        }
        if (active.GuestComposite is not null)
        {
            AdvanceGuestCompositeAction(active, guest);
            return;
        }
        if (active.Command.CommandId == (byte)Ff7BattleCommand.Magic && active.Command.GroupTarget)
        {
            throw new InvalidDataException("The active guest group lost its occurrence contact owner.");
        }
        Ff7BattleGuestInvocation invocation = GuestInvocation(active.Command, active.StartedAt);
        if (active.GuestLimit is not null)
        {
            AdvanceGuestLimit(active, guest, invocation);
            return;
        }
        if (guest.Invocation != invocation || guest.ContactPublished != active.Applied)
        {
            throw new InvalidDataException("The guest presentation and active host queue entry disagree about contact ownership.");
        }
        Ff7BattleGuestAttackUpdate update = guest.Advance(Tick);
        if (update.LimitPresentedContact is not null || update.LimitTargetCue is not null || update.CompositeContacts.Count != 0)
        {
            throw new InvalidDataException("A guest Limit presentation cue has no active Limit owner.");
        }
        if (update.Hit is { } hit)
        {
            if (active.Applied)
            {
                throw new InvalidDataException("The guest presentation already published its physical contact.");
            }
            ApplyGuestHit(active.Command, invocation.TargetSlot, hit);
            state = state with { DisplayedHit = state.LastHit };
            active = active with { Applied = true };
        }
        if (update.AttackTargetCue is byte cue)
        {
            if (cue != 4 || active.Command.GuestAbility != 0 || active.Command.CommandId != (byte)Ff7BattleCommand.Attack || active.GuestAttackTargetCue is not null)
            {
                throw new InvalidDataException("The guest Attack target cue has no matching physical action.");
            }
            active = active with { GuestAttackTargetCue = cue };
        }
        if (active.GuestAttackTargetCue is not null && active.Applied)
        {
            if (state.LastHit is not { } contact || contact.Tick < active.StartedAt || contact.Source != invocation.ActorSlot || contact.Target != invocation.TargetSlot)
            {
                throw new InvalidDataException("The guest Attack target cue has no matching physical contact.");
            }
            Ff7BattleAiActor target = Actor(invocation.TargetSlot).Actor;
            if (!contact.Damage.Missed)
            {
                AdmitImpactModel(new(invocation.ActorSlot, invocation.TargetSlot, -1, -1, -1) { ModelAction = target.HurtAction, PostActionKnockedOut = (target.Statuses & 1) != 0 });
            }
            active = active with { GuestAttackTargetCue = null };
        }
        if (update.Completed)
        {
            if (!active.Applied || guest.Invocation is not null)
            {
                throw new InvalidDataException("The guest presentation completed without settling its original physical contact.");
            }
            if (invocation.TargetSlot >= 4 && state.ActorModels.Length != 0 && ActorModelState(invocation.TargetSlot).PresentedKnockedOut != true && state.LastHit is { Damage.Missed: false } finalHit && finalHit.Tick >= active.StartedAt && finalHit.Source == invocation.ActorSlot && finalHit.Target == invocation.TargetSlot && !IsAlive(Actor(invocation.TargetSlot)))
            {
                AdmitGuestKnockout(invocation);
            }
            PublishGuestSourceInteraction(active.Command, invocation.TargetSlot);
            CompleteGuestMagic(active.Command);
            state = state with { Action = null };
            FlushFormationAi();
        }
        else
        {
            state = state with { Action = active };
        }
    }

    private void AdvanceGuestIdle(IFf7BattleGuestAttack guest)
    {
        int slot = guest.ActorSlot;
        uint statuses = Actor(slot).Actor.Statuses;
        if (state.ActorModels.Length != 0 && state.Action is { Command.GuestAttack: false } && state.Sounds?.PreparedImpacts.Any(impact => impact.Target == slot) == true && ActorModelState(slot).PresentedKnockedOut is bool presented)
        {
            statuses = (statuses & ~Ff7NativeBattleActionResolver.Death) | (presented ? Ff7NativeBattleActionResolver.Death : 0);
        }
        guest.AdvanceIdle(Tick, statuses, state.Reaction?.Slot == slot ? state.Reaction.Tick : null);
    }

    private void ApplyGuestHit(Ff7BattleQueuedCommand command, int targetSlot, Ff7BattleGuestPhysicalHit hit)
    {
        PublishNativeActionContext(command, hit.Elements, hit.Attack.UsePhysicalStats ? (ushort)0 : (ushort)4);
        Ff7BattleActorState source = Actor(command.ActorSlot);
        Ff7BattleActorState target = Actor(targetSlot);
        if (command.CommandId == (byte)Ff7BattleCommand.Item)
        {
            PublishGuestItem(command, source, target, hit);
            return;
        }
        Ff7BattleDamageAction attack = ProjectGuestOutgoingAttack(targetSlot, hit);
        if (command.GuestAbility != 0)
        {
            ValidateGuestMagic(command.GuestAbility, hit);
            Ff7BattleGuestPreparedContact prepared = hit.PreparedContact ?? throw new InvalidDataException("Guest magic lost its prepared native numeric receipt.");
            Ff7BattleActionVitals currentSource = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses) { OriginSavedStatuses = GetOriginSavedStatuses(command.ActorSlot) };
            Ff7BattleActionVitals currentTarget = new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses, (targetSlot >= 4 ? ~Enemy(targetSlot).NativeStatusMask : 0) | target.StatusProtection) { OriginSavedStatuses = GetOriginSavedStatuses(targetSlot) };
            if (prepared.Invocation != (GuestInvocation(command, state.Action!.StartedAt) with { TargetSlot = targetSlot }) || (!command.GroupTarget && state.Action.GuestComposite is null && prepared.SourceBefore.Mp != currentSource.Mp) || prepared.SourceBefore.MaxMp != currentSource.MaxMp || (targetSlot != command.ActorSlot && GetGuestProjection(targetSlot) is null && prepared.TargetBefore.MaxHp != currentTarget.MaxHp) || prepared.TargetBefore.MaxMp != currentTarget.MaxMp || prepared.TargetBefore.StatusImmunities != currentTarget.StatusImmunities)
            {
                throw new InvalidDataException("Guest magic numeric receipt no longer matches its host action and vitals.");
            }
            Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ApplyPreparedGuestContact(currentSource, currentTarget, prepared.Receipt, attack, targetSlot == command.ActorSlot);
            if (targetSlot != command.ActorSlot)
            {
                Replace(source with { Actor = source.Actor with { HitPoints = receipt.Source.Hp, MagicPoints = receipt.Source.Mp, Statuses = receipt.Source.Statuses } });
            }
            PublishHit(command, target, attack, receipt);
            if (!prepared.Receipt.Damage.Missed && hit.ProcessOperation is { } operation)
            {
                if (RequireGuestAttack(command) is not IFf7BattleGuestProcess { ProcessActive: true } process || !operation.Supported)
                {
                    throw new InvalidDataException("The process execution operation has no active source owner.");
                }
                process.ApplyProcessOperation(command.GuestAbility, targetSlot, operation);
            }
            if ((receipt.RemovedStatuses & Ff7NativeBattleActionResolver.Death) != 0 && state.ActorModels.Length != 0)
            {
                ReplaceActorModel(ActorModelState(targetSlot) with { PresentedKnockedOut = false });
            }
            if (!prepared.Receipt.Damage.Missed && !attack.Healing)
            {
                state = state with { Reaction = new(targetSlot, Tick) };
                if (targetSlot >= 4 && (receipt.AddedStatuses & Ff7NativeBattleActionResolver.Death) != 0)
                {
                    AdmitGuestKnockout(prepared.Invocation);
                }
            }
            return;
        }
        bool hasStatuses = hit.StatusRecord is not null || attack.ParameterOperation is not null || attack.GuestOriginSavedStatuses != 0;
        if (hit.PreparedContact is not null || attack is not { Formula: 0x11, UsePhysicalStats: true, TargetsMagicPoints: false, Healing: false } || attack.HasStatusOperation != hasStatuses || attack.AddsStatuses != hasStatuses)
        {
            throw new NotSupportedException("The current guest contact requires one ordinary physical HP attack.");
        }
        (source, attack) = ProjectPhysicalFacing(source, target, attack);
        Replace(source);
        Ff7BattleActionVitals sourceVitals = GuestItemVitals(source);
        Ff7BattleActionVitals targetVitals = GuestItemVitals(target);
        Ff7BattleActionReceipt result = Ff7NativeBattleActionResolver.ResolveGuestContact(hit.Source with { BackRow = (source.Actor.Conditions & 0x40) != 0 }, sourceVitals, Stats(target), targetVitals, attack, hit.StatusRecord, random, sameActor: command.ActorSlot == targetSlot, originStatusResistances: GetGuestProjection(targetSlot)?.OriginStatusResistances);
        PublishHit(command, target, attack, result);
        if (!result.Damage.Missed)
        {
            state = state with { Reaction = new(targetSlot, Tick) };
        }
    }

    private void AdmitGuestKnockout(Ff7BattleGuestInvocation invocation)
    {
        short selector = Actor(invocation.TargetSlot).Actor.HurtAction;
        AdmitImpactModel(new(invocation.ActorSlot, invocation.TargetSlot, -1, -1, -1) { ModelAction = selector, PostActionKnockedOut = true });
    }

    private Ff7BattleGuestInvocation GuestInvocation(Ff7BattleQueuedCommand command, long startedAt)
        => new(RequestId, Formation.FormationId, startedAt, command.ActorSlot, command.GuestPrimaryTarget ?? BitOperations.TrailingZeroCount(command.TargetMask)) { TargetMask = command.GroupTarget ? command.TargetMask : (ushort)0, GuestAbility = command.GuestAbility, GuestCommand = command.CommandId switch { (byte)Ff7BattleCommand.Item => command.CommandId, (byte)Ff7BattleCommand.Limit => 5, _ => null }, Reflected = command.Reflected };

    private static bool SupportsGuestAbility(IFf7BattleGuestAttack guest, ushort ability, Ff7BattleGuestAttackTarget target)
        => ability == 0 ? guest.Supports(target) : guest is IFf7BattleGuestAbilities catalog ? catalog.SupportsAbility(Ff7BattleCommand.Magic, ability, target) : guest is IFf7BattleGuestMagic magic && magic.MagicStock(ability) > 0 && magic.SupportsMagic(ability, target);

    private static bool GuestAbilityRequiresMagic(IFf7BattleGuestAttack guest, ushort ability)
        => ability != 0 && (guest is not IFf7BattleGuestAbilities catalog || catalog.Abilities.Single(choice => choice.Command == Ff7BattleCommand.Magic && choice.Ability == ability).RequiresMagic);

    private Ff7BattleDamageAction ProjectGuestOutgoingAttack(int targetSlot, Ff7BattleGuestPhysicalHit hit)
    {
        Ff7BattleDamageAffinity affinity = targetSlot >= 4 ? ResolveNativeEnemyAffinity(targetSlot, hit.Elements) : hit.Attack.Affinity;
        int adjustment = GetGuestProjection(targetSlot)?.OriginElementDefenses is { } defenses ? Ff7GuestBattleElements.DefenseAdjustment(hit.Elements, defenses) : 0;
        return hit.Attack with { Affinity = affinity, GuestElementPercent = hit.ElementPercent, GuestElementAdjustment = adjustment, NativeElements = hit.Elements == ushort.MaxValue ? (ushort)0 : hit.Elements };
    }

    private static void ValidateGuestMagic(ushort ability, Ff7BattleGuestPhysicalHit hit)
    {
        uint supportedStatuses = Ff7GuestBattleStatuses.SupportedHost;
        if (Ff7NativeBattleStatuses.ReadRecordMode(hit.StatusRecord) == 1)
        {
            supportedStatuses |= Ff7NativeBattleStatuses.SupportedRecordStatuses;
        }
        if (ability == 0 || hit.Attack.Formula is not (0x00 or 0x04 or 0x11 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x37 or 0x47 or 0x50 or 0xB1) || hit.Attack.HasStatusOperation != (hit.StatusRecord is not null || hit.Attack.ParameterOperation is not null || hit.Attack.GuestOriginSavedStatuses != 0 || hit.ProcessOperation is not null) || hit.ProcessOperation is { Supported: false } || (hit.Attack.GuestOriginSavedStatuses & ~Ff7OriginSavedStatusRules.Supported) != 0 || hit.StatusRecord is { } record && ((record.StatusMask & ~supportedStatuses) != 0 || record.AdditionalEffect != byte.MaxValue || (record.SpecialFlags & 0x10) == 0))
        {
            throw new NotSupportedException("The guest action requires a supported shared damage and status operation.");
        }
    }

    private Ff7BattleGuestAttackTarget DescribeGuestTarget(int slot)
    {
        Ff7BattleAnimationResources resources = animations ?? throw new InvalidOperationException("Guest targeting requires the original host model geometry.");
        string model = ActorModel(slot);
        return new(slot, slot >= 4 ? Enemy(slot) : null, Actor(slot).Actor, VisualPlacement(slot), resources.CollisionRadius(model)) { JointCount = resources.BoneCount(model), OriginSavedStatuses = GetOriginSavedStatuses(slot) };
    }

    public Ff7BattleGuestAttackTarget GuestAbilityTarget(int slot) => DescribeGuestTarget(slot);

    /// <summary>Exports current guest contact inputs without publishing state or drawing randomness.</summary>
    public Ff7BattleGuestContactContext GuestContactContext(int sourceSlot, int targetSlot, Ff7BattleGuestPhysicalHit hit, bool physical)
    {
        Ff7BattleActorState source = Actor(sourceSlot);
        Ff7BattleActorState target = Actor(targetSlot);
        Ff7BattleDamageAction attack = ProjectGuestOutgoingAttack(targetSlot, hit);
        if (physical)
        {
            (source, attack) = ProjectPhysicalFacing(source, target, attack);
        }
        Ff7BattleDamageActor stats = physical ? hit.Source with { BackRow = (source.Actor.Conditions & 0x40) != 0 } : hit.Source;
        Ff7BattleActionVitals sourceVitals = GuestItemVitals(source);
        Ff7BattleActionVitals targetVitals = GuestItemVitals(target);
        return new(stats, sourceVitals, Stats(target), targetVitals, attack, kernel[2].Data);
    }

    public Vector3 GuestEffectTargetAnchor(int slot)
    {
        if (slot is < 0 or 3 or > 9 || animations is null || !state.Actors.Any(actor => actor.Actor.Slot == slot))
        {
            throw new InvalidDataException("Guest effect geometry requires a physical host actor occurrence.");
        }
        return ImpactAnchor(slot);
    }

    public Vector3 GuestEffectTargetBone(int slot, int joint)
    {
        if (slot is < 0 or 3 or > 9 || animations is null || !state.Actors.Any(actor => actor.Actor.Slot == slot) || (uint)joint >= animations.BoneCount(ActorModel(slot)))
        {
            throw new InvalidDataException("Guest effect bone geometry requires a physical host actor and joint.");
        }
        return BonePosition(slot, joint);
    }

    private IFf7BattleGuestAttack RequireGuestAttack(Ff7BattleQueuedCommand command)
        => GetGuestAttack(command.ActorSlot) ?? throw new NotSupportedException("Resume the guest attack with its original presentation and origin state.");
}
