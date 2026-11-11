using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestInvocation(long RequestId, int FormationId, long StartedAt, int ActorSlot, int TargetSlot)
{
    public ushort GuestAbility { get; init; }
    public byte? GuestCommand { get; init; }
}
public sealed record Ff7BattleGuestAttackTarget(int Slot, SceneBattleEnemy? Native, Ff7BattleAiActor Actor, Ff7BattlePlacement Placement, ushort CollisionRadius);
public sealed record Ff7BattleGuestPhysicalHit(Ff7BattleDamageActor Source, Ff7BattleDamageAction Attack, ushort Elements)
{
    public Ff7BattleGuestPreparedContact? PreparedContact { get; init; }
    public Ff7BattleGuestItemEffect? ItemEffect { get; init; }
}
public sealed record Ff7BattleGuestPreparedContact(Ff7BattleGuestInvocation Invocation, Ff7BattleActionVitals SourceBefore, Ff7BattleActionVitals TargetBefore, Ff7BattleActionReceipt Receipt);
public sealed record Ff7BattleGuestAttackUpdate(Ff7BattleGuestPhysicalHit? Hit, bool Completed);

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
}

/// <summary>Explicit FFVIII-origin spell IDs and their origin-owned stock, independent of host spell records.</summary>
public interface IFf7BattleGuestMagic
{
    int MagicStock(ushort ability);
    bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target);
    Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability);
    void PrepareMagic(Ff7BattleGuestPhysicalHit prepared);
}

public sealed partial class Ff7BattleSession
{
    private IFf7BattleGuestAttack? guestAttack;

    public void BindGuestAttack(IFf7BattleGuestAttack guest)
    {
        ArgumentNullException.ThrowIfNull(guest);
        if (guestAttack is not null || animations is null || guest.ActorSlot is < 0 or >= 3 || !state.Actors.Any(actor => actor.Actor.Slot == guest.ActorSlot) || state.GuestVitals is { } binding && (binding.EncounterId != guest.EncounterId || binding.HostSlot != guest.ActorSlot))
        {
            throw new InvalidOperationException("A guest attack requires one occupied host slot and original battle geometry.");
        }
        if (state.Action is { Command.GuestAttack: true } active)
        {
            Ff7BattleCommandQueue.ValidateCommand(active.Command);
            if (guest.Invocation != GuestInvocation(active.Command, active.StartedAt) || guest.HostTick != Tick || guest.ContactPublished != active.Applied || guest.ActorSlot != active.Command.ActorSlot)
            {
                throw new InvalidDataException("The guest presentation does not match the saved host action occurrence.");
            }
        }
        else if (guest.Invocation is not null)
        {
            throw new InvalidDataException("The guest presentation has no active host action occurrence.");
        }
        if (state.Queue.Ready.Any(command => command is { GuestAttack: true } && command.ActorSlot != guest.ActorSlot) || state.Queue.Slots.Any(entry => entry?.Command is { GuestAttack: true } command && command.ActorSlot != guest.ActorSlot))
        {
            throw new InvalidDataException("The saved guest queue belongs to a different host slot.");
        }
        guestAttack = guest;
        state = state with { GuestPresentationAttached = true };
    }

    public bool SubmitGuestAttack(int slot, int targetSlot)
        => SubmitGuestAbility(slot, targetSlot, 0);

    public bool SubmitGuestMagic(int slot, int targetSlot, ushort ability = 1)
        => ability is 1 or 4 or 7 or 21 && SubmitGuestAbility(slot, targetSlot, ability);

    private bool SubmitGuestAbility(int slot, int targetSlot, ushort ability)
    {
        if (Outcome != Ff7BattleOutcome.Fighting || guestAttack is not { } guest || slot != guest.ActorSlot || ReadyPlayer != slot || !CanAct(Actor(slot)) || targetSlot is < 0 or > 9 || targetSlot == 3 || !state.Actors.Any(actor => actor.Actor.Slot == targetSlot && IsAlive(actor) && (actor.Actor.Conditions & 8) != 0))
        {
            return false;
        }
        if (!SupportsGuestAbility(guest, ability, DescribeGuestTarget(targetSlot)) || ability != 0 && !Ff7GuestBattleStatuses.CanCastMagic(Actor(slot).Actor.Statuses))
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
        Ff7BattleGuestInvocation invocation = GuestInvocation(command, Tick);
        Ff7BattleActorState target = Actor(invocation.TargetSlot);
        IFf7BattleGuestItems? items = command.CommandId == (byte)Ff7BattleCommand.Item ? guest as IFf7BattleGuestItems ?? throw new InvalidDataException("The guest item lost its origin inventory owner.") : null;
        bool usable = items is null ? IsAlive(target) && (target.Actor.Conditions & 8) != 0 : GuestItemTarget(items, command.GuestAbility, target);
        if (!CanAct(Actor(command.ActorSlot)) || !usable)
        {
            items?.CancelItem(invocation.TargetSlot, command.GuestAbility);
            FinishTurn(command.ActorSlot);
            return true;
        }
        Ff7BattleGuestAttackTarget targetDescription = DescribeGuestTarget(invocation.TargetSlot);
        if (items is not null)
        {
            PrepareGuestItem(items, invocation, target);
        }
        else if (!SupportsGuestAbility(guest, command.GuestAbility, targetDescription) || command.GuestAbility != 0 && !Ff7GuestBattleStatuses.CanCastMagic(Actor(command.ActorSlot).Actor.Statuses))
        {
            FinishTurn(command.ActorSlot);
            return true;
        }
        else if (command.GuestAbility != 0)
        {
            IFf7BattleGuestMagic magic = (IFf7BattleGuestMagic)guest;
            Ff7BattleGuestPhysicalHit hit = magic.DescribeMagic(command.GuestAbility);
            Ff7BattleActorState source = Actor(command.ActorSlot);
            Ff7BattleActionVitals sourceVitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
            Ff7BattleActionVitals targetVitals = new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses, invocation.TargetSlot >= 4 ? ~Enemy(invocation.TargetSlot).NativeStatusMask : 0);
            ValidateGuestMagic(command.GuestAbility, hit);
            Ff7BattleDamageAction attack = hit.Attack with { Affinity = invocation.TargetSlot >= 4 ? Ff7NativeBattleDamage.ResolveEnemyAffinity(Enemy(invocation.TargetSlot), hit.Elements) : hit.Attack.Affinity };
            Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(hit.Source, sourceVitals, Stats(target), targetVitals, attack, null, random, sameActor: command.ActorSlot == invocation.TargetSlot);
            magic.PrepareMagic(hit with { Attack = attack, PreparedContact = new(invocation, sourceVitals, targetVitals, receipt) });
        }
        guest.Begin(invocation, VisualPlacement(command.ActorSlot), targetDescription);
        state = state with { Action = new(command, Tick, -1), GuestAttackUsed = true };
        return true;
    }

    private void AdvanceGuestAttack()
    {
        if (state.Action is not { Command.GuestAttack: true } active)
        {
            if (guestAttack is { } idle)
            {
                idle.AdvanceIdle(Tick, Actor(idle.ActorSlot).Actor.Statuses, state.Reaction?.Slot == idle.ActorSlot ? state.Reaction.Tick : null);
            }
            return;
        }
        if (active.StartedAt == Tick)
        {
            return;
        }
        IFf7BattleGuestAttack guest = RequireGuestAttack(active.Command);
        Ff7BattleGuestInvocation invocation = GuestInvocation(active.Command, active.StartedAt);
        if (guest.Invocation != invocation || guest.ContactPublished != active.Applied)
        {
            throw new InvalidDataException("The guest presentation and active host queue entry disagree about contact ownership.");
        }
        Ff7BattleGuestAttackUpdate update = guest.Advance(Tick);
        if (update.Hit is { } hit)
        {
            if (active.Applied)
            {
                throw new InvalidDataException("The guest presentation already published its physical contact.");
            }
            ApplyGuestHit(active.Command, invocation.TargetSlot, hit);
            active = active with { Applied = true };
        }
        if (update.Completed)
        {
            if (!active.Applied || guest.Invocation is not null)
            {
                throw new InvalidDataException("The guest presentation completed without settling its original physical contact.");
            }
            FinishTurn(active.Command.ActorSlot);
            state = state with { Action = null };
        }
        else
        {
            state = state with { Action = active };
        }
    }

    private void ApplyGuestHit(Ff7BattleQueuedCommand command, int targetSlot, Ff7BattleGuestPhysicalHit hit)
    {
        Ff7BattleActorState source = Actor(command.ActorSlot);
        Ff7BattleActorState target = Actor(targetSlot);
        if (command.CommandId == (byte)Ff7BattleCommand.Item)
        {
            PublishGuestItem(command, source, target, hit);
            return;
        }
        Ff7BattleDamageAction attack = hit.Attack with { Affinity = targetSlot >= 4 ? Ff7NativeBattleDamage.ResolveEnemyAffinity(Enemy(targetSlot), hit.Elements) : hit.Attack.Affinity };
        if (command.GuestAbility != 0)
        {
            ValidateGuestMagic(command.GuestAbility, hit);
            Ff7BattleGuestPreparedContact prepared = hit.PreparedContact ?? throw new InvalidDataException("Guest magic lost its prepared native numeric receipt.");
            Ff7BattleActionVitals currentSource = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
            Ff7BattleActionVitals currentTarget = new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses, targetSlot >= 4 ? ~Enemy(targetSlot).NativeStatusMask : 0);
            if (prepared.Invocation != GuestInvocation(command, state.Action!.StartedAt) || prepared.SourceBefore.Mp != currentSource.Mp || prepared.SourceBefore.MaxMp != currentSource.MaxMp || (targetSlot != command.ActorSlot && prepared.TargetBefore.MaxHp != currentTarget.MaxHp) || prepared.TargetBefore.MaxMp != currentTarget.MaxMp || prepared.TargetBefore.StatusImmunities != currentTarget.StatusImmunities || (currentTarget.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0)
            {
                throw new InvalidDataException("Guest magic numeric receipt no longer matches its host action and vitals.");
            }
            Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ApplyPreparedHpDamage(currentSource, currentTarget, prepared.Receipt.Damage);
            PublishHit(command, target, attack, receipt);
            if (!prepared.Receipt.Damage.Missed && !attack.Healing)
            {
                state = state with { Reaction = new(targetSlot, Tick) };
            }
            return;
        }
        if (hit.PreparedContact is not null || attack is not { Formula: 0x11, UsePhysicalStats: true, HasStatusOperation: false, TargetsMagicPoints: false, Healing: false })
        {
            throw new NotSupportedException("The current guest contact requires one ordinary physical HP attack.");
        }
        (source, attack) = ProjectPhysicalFacing(source, target, attack);
        Replace(source);
        Ff7BattleActionVitals sourceVitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
        Ff7BattleActionVitals targetVitals = new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses, targetSlot >= 4 ? ~Enemy(targetSlot).NativeStatusMask : 0);
        Ff7BattleActionReceipt result = Ff7NativeBattleActionResolver.Resolve(hit.Source with { BackRow = (source.Actor.Conditions & 0x40) != 0 }, sourceVitals, Stats(target), targetVitals, attack, null, random);
        PublishHit(command, target, attack, result);
        if (!result.Damage.Missed)
        {
            state = state with { Reaction = new(targetSlot, Tick) };
        }
    }

    private Ff7BattleGuestInvocation GuestInvocation(Ff7BattleQueuedCommand command, long startedAt)
        => new(RequestId, Formation.FormationId, startedAt, command.ActorSlot, BitOperations.TrailingZeroCount(command.TargetMask)) { GuestAbility = command.GuestAbility, GuestCommand = command.CommandId == (byte)Ff7BattleCommand.Item ? command.CommandId : null };

    private static bool SupportsGuestAbility(IFf7BattleGuestAttack guest, ushort ability, Ff7BattleGuestAttackTarget target)
        => ability == 0 ? guest.Supports(target) : ability is 1 or 4 or 7 or 21 && (target.Actor.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) == 0 && guest is IFf7BattleGuestMagic magic && magic.MagicStock(ability) > 0 && magic.SupportsMagic(ability, target);

    private static void ValidateGuestMagic(ushort ability, Ff7BattleGuestPhysicalHit hit)
    {
        if (!(ability is 1 or 4 or 7 && hit.Elements == (ability == 1 ? 1 : ability == 4 ? 2 : 4) && hit.Attack is { Formula: 0x22, Power: 8, Accuracy: 100, UsePhysicalStats: false, HasStatusOperation: false, TargetsMagicPoints: false, Healing: false } || ability == 21 && hit.Elements == 0x200 && hit.Attack is { Formula: 0x25, Power: 5, Accuracy: 255, UsePhysicalStats: false, HasStatusOperation: false, TargetsMagicPoints: false, Healing: true }))
        {
            throw new NotSupportedException("The guest magic profile requires the explicitly calibrated FFVIII Fire action.");
        }
    }

    private Ff7BattleGuestAttackTarget DescribeGuestTarget(int slot)
        => new(slot, slot >= 4 ? Enemy(slot) : null, Actor(slot).Actor, VisualPlacement(slot), animations!.CollisionRadius(ActorModel(slot)));

    public Vector3 GuestEffectTargetAnchor(int slot)
    {
        if (slot is < 0 or 3 or > 9 || animations is null || !state.Actors.Any(actor => actor.Actor.Slot == slot))
        {
            throw new InvalidDataException("Guest effect geometry requires a physical host actor occurrence.");
        }
        return ImpactAnchor(slot);
    }

    private IFf7BattleGuestAttack RequireGuestAttack(Ff7BattleQueuedCommand command)
        => guestAttack is { } guest && guest.ActorSlot == command.ActorSlot ? guest : throw new NotSupportedException("Resume the guest attack with its original presentation and origin state.");
}
