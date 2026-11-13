using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleReflectedContact(int TargetSlot, Ff7BattleActionReceipt Receipt, Ff7BattleDamageAction Attack)
{
    public bool Reflected { get; init; }
    public ImmutableArray<Ff7BattleGuestCompositeReflection> Reflections { get; init; } = [];
}

public sealed partial class Ff7BattleSession
{
    private sealed record ReflectionPlan(IReadOnlyList<Ff7BattleReflectedContact> Contacts, IReadOnlyDictionary<int, byte> Counts, ushort RemovedReflect);
    private sealed record ReflectionPath(bool Reflected, ImmutableArray<Ff7BattleGuestCompositeReflection> Reflections);

    private ReflectionPlan? PrepareReflections(Ff7BattleQueuedCommand command, Ff7BattleGuestPhysicalHit description, Ff7BattleRng candidateRandom, bool guest, Ff7BattleActionVitals? sourceOverride = null)
    {
        bool reflectable = guest ? description.Attack.Reflectable : description.StatusRecord is { SpecialFlags: var flags } && (flags & 0x200) == 0;
        if (!reflectable || !state.Actors.Any(actor => (command.TargetMask & (1 << actor.Actor.Slot)) != 0 && (actor.Actor.Statuses & Ff7NativeBattleStatuses.Reflect) != 0))
        {
            return null;
        }
        Dictionary<int, Ff7BattleActionVitals> vitals = state.Actors.ToDictionary(actor => actor.Actor.Slot, actor => GuestItemVitals(actor) with { StatusImmunities = (actor.Actor.Slot >= 4 ? ~Enemy(actor.Actor.Slot).NativeStatusMask : 0) | actor.StatusProtection });
        if (sourceOverride is { } sourceVitals)
        {
            vitals[command.ActorSlot] = sourceVitals with { StatusImmunities = vitals[command.ActorSlot].StatusImmunities };
        }
        Dictionary<int, byte> counts = [];
        Dictionary<int, int> cachedRecipients = [];
        ushort exhausted = 0;
        ushort permanentVisited = 0;
        List<Ff7BattleReflectedContact> contacts = [];
        SortedDictionary<int, ushort> pending = new() { [command.ActorSlot] = command.TargetMask };
        Dictionary<(int Sender, int Target), ReflectionPath> pendingPaths = state.Actors.Where(actor => (command.TargetMask & (1 << actor.Actor.Slot)) != 0).ToDictionary(actor => (command.ActorSlot, actor.Actor.Slot), _ => new ReflectionPath(false, []));
        while (pending.Count != 0)
        {
            SortedDictionary<int, ushort> next = [];
            Dictionary<(int Sender, int Target), ReflectionPath> nextPaths = [];
            foreach ((int sender, ushort targetMask) in pending)
            {
                foreach (Ff7BattleActorState actor in state.Actors.Where(actor => (targetMask & (1 << actor.Actor.Slot)) != 0).OrderBy(actor => actor.Actor.Slot))
                {
                    ReflectionPath path = pendingPaths[(sender, actor.Actor.Slot)];
                    int slot = actor.Actor.Slot;
                    Ff7BattleActionVitals source = vitals[command.ActorSlot];
                    Ff7BattleActionVitals target = vitals[slot];
                    Ff7BattleDamageAction attack = ProjectGuestOutgoingAttack(slot, description);
                    Ff7BattleActionReceipt receipt;
                    if (guest || GetGuestProjection(slot) is not null)
                    {
                        receipt = Ff7NativeBattleActionResolver.ResolveGuestContact(description.Source, source, Stats(actor), target, attack, guest ? description.StatusRecord : Ff7GuestBattleStatuses.ProjectIncoming(description.StatusRecord), candidateRandom, slot == command.ActorSlot, GetGuestProjection(slot)?.OriginStatusResistances, GetGuestBinding(slot) is null ? 0 : GetGuestOriginBattleStatuses(slot), GetGuestBinding(command.ActorSlot) is null ? (ushort)0 : GetGuestOriginSavedStatuses(command.ActorSlot), GetGuestBinding(slot) is null ? (ushort)0 : GetGuestOriginSavedStatuses(slot));
                    }
                    else
                    {
                        receipt = Ff7NativeBattleActionResolver.Resolve(description.Source, source, Stats(actor), target, attack, description.StatusRecord, candidateRandom, slot == command.ActorSlot);
                    }
                    ushort bit = (ushort)(1 << slot);
                    if (receipt.Damage.Missed || (target.Statuses & Ff7NativeBattleStatuses.Reflect) == 0 || (permanentVisited & bit) != 0)
                    {
                        contacts.Add(new(slot, receipt, attack) { Reflected = path.Reflected, Reflections = path.Reflections });
                        vitals[command.ActorSlot] = receipt.Source;
                        vitals[slot] = receipt.Target;
                        continue;
                    }
                    int recipient;
                    if ((sender < 4) != (slot < 4))
                    {
                        recipient = sender;
                    }
                    else if (!cachedRecipients.TryGetValue(slot, out recipient))
                    {
                        ushort candidates = Mask(state.Actors.Where(candidate => (candidate.Actor.Conditions & 8) != 0 && (candidate.Actor.Slot < 4) != (slot < 4)));
                        if (candidates == 0)
                        {
                            throw new NotSupportedException("The reflected action has no opposite-side target occurrence.");
                        }
                        int ordinal = candidateRandom.NextByte() % BitOperations.PopCount(candidates);
                        while (ordinal-- > 0)
                        {
                            candidates &= (ushort)(candidates - 1);
                        }
                        recipient = BitOperations.TrailingZeroCount(candidates);
                        cachedRecipients[slot] = recipient;
                    }
                    ImmutableArray<Ff7BattleGuestCompositeReflection> transitions = path.Reflections;
                    if ((target.StatusImmunities & Ff7NativeBattleStatuses.Reflect) != 0)
                    {
                        permanentVisited |= bit;
                    }
                    else
                    {
                        byte remaining = counts.GetValueOrDefault(slot, actor.ReflectionCount ?? (byte)3);
                        if (remaining == 0)
                        {
                            exhausted |= bit;
                            vitals[slot] = target with { Statuses = target.Statuses & ~Ff7NativeBattleStatuses.Reflect };
                        }
                        else
                        {
                            remaining--;
                        }
                        counts[slot] = remaining;
                        transitions = transitions.Add(new(slot, remaining, (exhausted & bit) != 0));
                    }
                    next[slot] = (ushort)(next.GetValueOrDefault(slot) | (1 << recipient));
                    if (nextPaths.TryGetValue((slot, recipient), out ReflectionPath? existingPath))
                    {
                        transitions = existingPath.Reflections.AddRange(transitions).GroupBy(value => value.Slot).Select(group => new Ff7BattleGuestCompositeReflection(group.Key, group.Min(value => value.Remaining), group.Any(value => value.Exhausted))).ToImmutableArray();
                    }
                    nextPaths[(slot, recipient)] = new(true, transitions);
                }
            }
            pending = next;
            pendingPaths = nextPaths;
        }
        return new(contacts, counts, exhausted);
    }

    private void CommitReflections(ReflectionPlan plan)
    {
        foreach ((int slot, byte count) in plan.Counts)
        {
            Ff7BattleActorState actor = Actor(slot);
            uint statuses = (plan.RemovedReflect & (1 << slot)) != 0 ? actor.Actor.Statuses & ~Ff7NativeBattleStatuses.Reflect : actor.Actor.Statuses;
            Replace(actor with { ReflectionCount = count, Actor = actor.Actor with { Statuses = statuses } });
        }
    }
}
