using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestProcessAction(ushort Ability, ushort Targets, bool Group = false, int DamageScalePercent = 100)
{
    public Ff7BattleCommand Command { get; init; } = Ff7BattleCommand.Magic;
}

/// <summary>Retains an origin command turn while its native input and action phases run.</summary>
public sealed record Ff7BattleGuestProcessOperation(uint SourceBattleStatuses, byte StatusAttack)
{
    public bool Supported => SourceBattleStatuses == 0x2000000 && StatusAttack == 255;
}

public interface IFf7BattleGuestProcess
{
    bool ProcessActive { get; }
    bool ProcessOwnsReadyTurn { get; }
    void AdvanceProcess(long hostTick);
    Ff7BattleGuestProcessAction? PollProcess(long hostTick);
    void ProcessActionCompleted(ushort ability);
    void ApplyProcessOperation(ushort ability, int targetSlot, Ff7BattleGuestProcessOperation operation);
    void CancelProcess();
}

public interface IFf7BattleGuestPresentationOnly
{
    bool IsPresentationOnly(ushort ability);
    void BeginPresentationOnly(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target);
}

public sealed partial class Ff7BattleSession
{
    public bool CancelGuestProcessSelection(int slot)
    {
        if (GetGuestAttack(slot) is not IFf7BattleGuestProcess { ProcessActive: true } process || state.Action?.Command.ActorSlot == slot || state.Queue.Ready.Any(command => command?.ActorSlot == slot) || state.Queue.Slots.Any(entry => entry?.Command.ActorSlot == slot))
        {
            return false;
        }
        process.CancelProcess();
        Replace(Actor(slot) with { AwaitingTurn = true });
        return true;
    }

    private void AdvanceGuestProcesses()
    {
        foreach (IFf7BattleGuestAttack guest in guestAttacks.OfType<IFf7BattleGuestAttack>())
        {
            if (guest is not IFf7BattleGuestProcess process)
            {
                continue;
            }
            bool held = process.ProcessActive;
            if (Outcome != Ff7BattleOutcome.Fighting || !CanAct(Actor(guest.ActorSlot)))
            {
                process.CancelProcess();
                if (held)
                {
                    FinishTurn(guest.ActorSlot);
                }
                continue;
            }
            process.AdvanceProcess(Tick);
            if (held && !process.ProcessActive)
            {
                FinishTurn(guest.ActorSlot);
                continue;
            }
            if (state.Action is not null || state.Queue.Ready.Any(command => command?.ActorSlot == guest.ActorSlot) || state.Queue.Slots.Any(entry => entry?.Command.ActorSlot == guest.ActorSlot))
            {
                continue;
            }
            if (!held && (!process.ProcessOwnsReadyTurn || !Actor(guest.ActorSlot).AwaitingTurn))
            {
                continue;
            }
            Ff7BattleGuestProcessAction? next = process.PollProcess(Tick);
            if (next is null)
            {
                if (held && !process.ProcessActive)
                {
                    FinishTurn(guest.ActorSlot);
                }
                continue;
            }
            if (next.Targets == 0 || (next.Targets & ~0x3F7) != 0 || next.DamageScalePercent <= 0)
            {
                throw new InvalidDataException("The guest process produced an invalid host action selection.");
            }
            state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, guest.ActorSlot, (byte)next.Command, argument: next.Ability, targetMask: next.Targets, groupTarget: next.Group, guestAttack: true, guestAbility: next.Ability, magicDamageScalePercent: next.DamageScalePercent) };
            Replace(Actor(guest.ActorSlot) with { AwaitingTurn = false });
        }
    }

    private bool CompleteGuestProcess(Ff7BattleQueuedCommand command)
    {
        if (command.GuestAttack && GetGuestAttack(command.ActorSlot) is IFf7BattleGuestProcess { ProcessActive: true } process)
        {
            process.ProcessActionCompleted(command.GuestAbility);
            if (!process.ProcessActive)
            {
                FinishTurn(command.ActorSlot);
            }
            return true;
        }
        return false;
    }

    private bool DispatchGuestPresentationOnly(Ff7BattleQueuedCommand command, IFf7BattleGuestAttack guest, Ff7BattleGuestInvocation invocation)
    {
        if (guest is not IFf7BattleGuestPresentationOnly presentation || !presentation.IsPresentationOnly(command.GuestAbility))
        {
            return false;
        }
        presentation.BeginPresentationOnly(invocation, VisualPlacement(command.ActorSlot), DescribeGuestTarget(invocation.TargetSlot));
        state = state with { Action = new(command, Tick, -1) { GuestPresentationOnly = true }, GuestAttackUsed = true };
        return true;
    }

    private void AdvanceGuestPresentationOnly(Ff7BattleActionState active, IFf7BattleGuestAttack guest)
    {
        if (guest.Invocation != GuestInvocation(active.Command, active.StartedAt))
        {
            throw new InvalidDataException("The presentation-only process lost its invocation owner.");
        }
        Ff7BattleGuestAttackUpdate step = guest.Advance(Tick);
        if (step.Hit is not null || step.GroupHits.Count != 0 || step.CompositeContacts.Count != 0)
        {
            throw new InvalidDataException("A presentation-only process cannot publish numerical contacts.");
        }
        if (step.Completed)
        {
            CompleteGuestMagic(active.Command);
            state = state with { Action = null };
            FlushFormationAi();
        }
    }
}
