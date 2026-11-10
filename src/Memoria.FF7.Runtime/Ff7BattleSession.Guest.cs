using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestInvocation(long RequestId, int FormationId, long StartedAt, int ActorSlot, int TargetSlot);
public sealed record Ff7BattleGuestAttackTarget(int Slot, SceneBattleEnemy Native, Ff7BattleAiActor Actor, Ff7BattlePlacement Placement, ushort CollisionRadius);
public sealed record Ff7BattleGuestPhysicalHit(Ff7BattleDamageActor Source, Ff7BattleDamageAction Attack, ushort Elements);
public sealed record Ff7BattleGuestAttackUpdate(Ff7BattleGuestPhysicalHit? Hit, bool Completed);

/// <summary>Owns one origin-specific physical presentation and its immutable contact description.</summary>
public interface IFf7BattleGuestAttack
{
    int ActorSlot { get; }
    Ff7BattleGuestInvocation? Invocation { get; }
    bool ContactPublished { get; }
    bool Supports(Ff7BattleGuestAttackTarget target);
    void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target);
    Ff7BattleGuestAttackUpdate Advance(long hostTick);
}

public sealed partial class Ff7BattleSession
{
    private IFf7BattleGuestAttack? guestAttack;

    public void BindGuestAttack(IFf7BattleGuestAttack guest)
    {
        ArgumentNullException.ThrowIfNull(guest);
        if (guestAttack is not null || animations is null || guest.ActorSlot is < 0 or >= 3 || !state.Actors.Any(actor => actor.Actor.Slot == guest.ActorSlot))
        {
            throw new InvalidOperationException("A guest attack requires one occupied host slot and original battle geometry.");
        }
        if (state.Action is { Command.GuestAttack: true } active)
        {
            if (guest.Invocation != GuestInvocation(active.Command, active.StartedAt) || guest.ContactPublished != active.Applied || guest.ActorSlot != active.Command.ActorSlot)
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
    }

    public bool SubmitGuestAttack(int slot, int targetSlot)
    {
        if (Outcome != Ff7BattleOutcome.Fighting || guestAttack is not { } guest || slot != guest.ActorSlot || ReadyPlayer != slot || Actor(slot).Actor.Statuses != 0 || targetSlot is < 4 or > 9 || !state.Actors.Any(actor => actor.Actor.Slot == targetSlot && IsAlive(actor) && (actor.Actor.Conditions & 8) != 0))
        {
            return false;
        }
        if (!guest.Supports(DescribeGuestTarget(targetSlot)))
        {
            return false;
        }
        Ff7BattleActorState actor = Actor(slot);
        state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)Ff7BattleCommand.Attack, targetMask: (ushort)(1 << targetSlot), guestAttack: true) };
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
        if (!IsAlive(target) || (target.Actor.Conditions & 8) == 0 || !guest.Supports(DescribeGuestTarget(invocation.TargetSlot)))
        {
            FinishTurn(command.ActorSlot);
            return true;
        }
        guest.Begin(invocation, VisualPlacement(command.ActorSlot), DescribeGuestTarget(invocation.TargetSlot));
        state = state with { Action = new(command, Tick, -1) };
        return true;
    }

    private void AdvanceGuestAttack()
    {
        if (state.Action is not { Command.GuestAttack: true } active || active.StartedAt == Tick)
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
            state = state with { Action = null, Message = null };
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
        Ff7BattleDamageAction attack = hit.Attack with { Affinity = Ff7NativeBattleDamage.ResolveEnemyAffinity(Enemy(targetSlot), hit.Elements) };
        if (attack is not { Formula: 0x11, UsePhysicalStats: true, HasStatusOperation: false, TargetsMagicPoints: false, Healing: false })
        {
            throw new NotSupportedException("The current guest contact requires one ordinary physical HP attack.");
        }
        (source, attack) = ProjectPhysicalFacing(source, target, attack);
        Replace(source);
        Ff7BattleActionVitals sourceVitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
        Ff7BattleActionVitals targetVitals = new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses, ~Enemy(targetSlot).NativeStatusMask);
        Ff7BattleActionReceipt result = Ff7NativeBattleActionResolver.Resolve(hit.Source with { BackRow = (source.Actor.Conditions & 0x40) != 0 }, sourceVitals, Stats(target), targetVitals, attack, null, random);
        PublishHit(command, target, attack, result);
        if (!result.Damage.Missed)
        {
            state = state with { Reaction = new(targetSlot, Tick) };
        }
    }

    private Ff7BattleGuestInvocation GuestInvocation(Ff7BattleQueuedCommand command, long startedAt)
        => new(RequestId, Formation.FormationId, startedAt, command.ActorSlot, BitOperations.TrailingZeroCount(command.TargetMask));

    private Ff7BattleGuestAttackTarget DescribeGuestTarget(int slot)
        => new(slot, Enemy(slot), Actor(slot).Actor, VisualPlacement(slot), animations!.CollisionRadius(ActorModel(slot)));

    private IFf7BattleGuestAttack RequireGuestAttack(Ff7BattleQueuedCommand command)
        => guestAttack is { } guest && guest.ActorSlot == command.ActorSlot ? guest : throw new NotSupportedException("Resume the guest attack with its original presentation and origin state.");
}
