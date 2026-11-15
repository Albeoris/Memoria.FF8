namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool originalParticipantSlotsInitialized;

    private static BattleActorReference TargetActorReference(BattleCombatant actor)
    {
        return actor.Side == BattleSide.Allies ? new BattlePartyActorReference(actor.Id) : new BattleEnemyActorReference(actor.Id);
    }

    private BattleActorReference? ReadOriginalTargetOccupant(BattleRosterSlot slot)
    {
        return FindAllocatedNativeActor(NativeBattleSlots.Encode(slot)) is { } actor ? TargetActorReference(actor) : null;
    }

    private BattleRosterSlot? ReadOriginalTargetSlot(BattleActorReference reference)
    {
        BattleCombatant? actor = FindTargetActor(reference);
        return actor is not null && HasNativeRosterSlot(actor) ? NativeBattleSlots.Decode(actor.NativeActorIndex!.Value) : null;
    }

    private BattleCombatant? FindTargetActor(BattleActorReference reference)
    {
        return byId.TryGetValue(reference.ActorId, out BattleCombatant? actor) && reference == TargetActorReference(actor) ? actor : null;
    }

    private static BattleTargetRequest SelectBattleTarget(BattleCombatant actor, bool allowsKnockedOutAllies = false, BattleTargetEligibility eligibility = BattleTargetEligibility.SavedStatus)
    {
        return new(new BattleTargetSelection.Explicit([TargetActorReference(actor)]), eligibility, allowsKnockedOutAllies, BattleTargetReplacement.None);
    }

    private bool IsBattleTargetEligible(BattleCombatant actor, BattleTargetRequest request)
    {
        if (!actor.IsActive)
        {
            return false;
        }
        if (request.DeathCallbackSelf == TargetActorReference(actor))
        {
            return true;
        }
        if (actor.Untargetable)
        {
            return false;
        }

        bool allowKo = request.AllowsKnockedOutAllies && actor.IsPlayer;
        BattleTargetEligibility eligibility = request.Eligibility == BattleTargetEligibility.DeathCallback
            ? UsesOriginalEnemySlots ? BattleTargetEligibility.Living : BattleTargetEligibility.AiAvailable
            : request.Eligibility;
        return eligibility switch
        {
            BattleTargetEligibility.DrawnMagic => actor.IsAlive || allowKo,
            BattleTargetEligibility.CompelledAction or BattleTargetEligibility.Living => !actor.IsEjected && (actor.IsAlive || allowKo),
            BattleTargetEligibility.AiAvailable => IsExpandedAiTarget(actor) || allowKo && !actor.IsEjected && !actor.IsAlive && request.Selection is not BattleTargetSelection.RandomOnSide,
            _ => (NativeSavedStatusBits(actor) & 1) == 0 || allowKo
        };
    }

    private BattleCombatant[] ResolveBattleTargets(BattleTargetRequest request, bool sampleRandom = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        ushort? diagnosticWord = sampleRandom && diagnosticReflectionProduction is not null
            ? NativeBattleTargetRequestAdapter.ProjectOriginalWord(request, ReadOriginalTargetSlot)
            : null;
        void ObserveOriginalResolution(int selected, int available, int resolved)
        {
            if (diagnosticWord is ushort word)
            {
                int modifiers = word >> 13;
                if (request.Selection is BattleTargetSelection.RandomOnSide)
                {
                    modifiers &= ~4;
                }
                ObserveDiagnosticReflectionTargetResolution(word, selected, modifiers, available, resolved);
            }
        }

        bool allowReplacement = request.Replacement == BattleTargetReplacement.UseCurrentBattlePolicy && !nativeScriptState.DisableAutomaticTargetReplacement;
        IEnumerable<BattleCombatant> SidePopulation(BattleSide side)
        {
            return Roster(side).Where(actor => actor.Slot is not null && (UsesExpandedRoster || HasNativeRosterSlot(actor))).OrderBy(actor => actor.Slot!.Value.Position);
        }

        BattleCombatant[] ResolveSelection(BattleTargetSelection selection, out bool unavailable)
        {
            unavailable = false;
            switch (selection)
            {
                case BattleTargetSelection.None:
                    return [];
                case NativeBattleTargetRequestAdapter.DeferredOperands original:
                    var resolved = NativeBattleTargetRequestAdapter.ResolveDeferred(original, ReadOriginalTargetOccupant, reference => IsBattleTargetEligible(FindTargetActor(reference)!, request), allowReplacement, diagnosticWord is null ? null : ObserveOriginalResolution);
                    unavailable = resolved.Unavailable;
                    return resolved.Targets.Select(reference => FindTargetActor(reference)!).ToArray();
                case BattleTargetSelection.SideGroup group:
                    IEnumerable<BattleCombatant> members = group.Side switch
                    {
                        BattleTargetSide.Allies => SidePopulation(BattleSide.Allies),
                        BattleTargetSide.Enemies => SidePopulation(BattleSide.Enemies),
                        _ => SidePopulation(BattleSide.Allies).Concat(SidePopulation(BattleSide.Enemies))
                    };
                    BattleCombatant[] grouped = members.Where(actor => IsBattleTargetEligible(actor, request)).ToArray();
                    unavailable = grouped.Length == 0;
                    return grouped;
                case BattleTargetSelection.RandomOnSide { SamplesOriginalSlots: true } originalRandom when sampleRandom:
                    return ResolveSelection(ChooseOriginalRandomActionTarget(originalRandom.Side), out unavailable);
                case BattleTargetSelection.RandomOnSide random:
                    BattleCombatant[] population = SidePopulation(random.Side).Where(actor => IsBattleTargetEligible(actor, request)).ToArray();
                    if (!sampleRandom || population.Length == 0)
                    {
                        unavailable = population.Length == 0;
                        return population;
                    }
                    return [population[NextRandomByte() % population.Length]];
                case BattleTargetSelection.Explicit exact:
                    BattleCombatant[] selected = exact.Actors.Select(FindTargetActor).OfType<BattleCombatant>().ToArray();
                    BattleCombatant[] eligible = selected.Where(actor => IsBattleTargetEligible(actor, request)).ToArray();
                    if (eligible.Length != 0)
                    {
                        return eligible;
                    }
                    if (allowReplacement && selected.FirstOrDefault() is { Slot: { } slot } first)
                    {
                        BattleCombatant[] available = SidePopulation(first.Side).Where(actor => IsBattleTargetEligible(actor, request)).ToArray();
                        BattleCombatant? replacement = available.FirstOrDefault(actor => actor.Slot!.Value.Position > slot.Position)
                            ?? available.LastOrDefault(actor => actor.Slot!.Value.Position < slot.Position);
                        if (replacement is not null)
                        {
                            return [replacement];
                        }
                    }
                    unavailable = allowReplacement;
                    return request.Replacement == BattleTargetReplacement.UseCurrentBattlePolicy ? selected.Where(actor => actor.IsActive).ToArray() : [];
                default:
                    throw new InvalidDataException("Battle target request has no selection contract.");
            }
        }

        BattleCombatant[] actors = ResolveSelection(request.Selection, out bool targetUnavailable);
        if (sampleRandom)
        {
            nativeActionScratch = nativeActionScratch with { TargetUnavailableB0E = targetUnavailable };
        }
        return actors;
    }

    private BattleTargetSelection ChooseOriginalRandomActionTarget(BattleSide side)
    {
        int count = side == BattleSide.Allies ? NativeBattleSlots.AllyCount : NativeBattleSlots.EnemyCount;
        BattleRosterSlot Slot(int index)
        {
            return side == BattleSide.Allies ? BattleRosterSlot.Ally(index) : BattleRosterSlot.Enemy(index);
        }

        for (int index = 0; index < count; index++)
        {
            BattleCombatant? actor = FindAllocatedNativeActor(NativeBattleSlots.Encode(Slot(index)));
            if (side == BattleSide.Allies && actor is not { IsActive: true })
            {
                continue;
            }
            if (OriginalRandomStatusBlocked(actor, 5))
            {
                continue;
            }
            while (true)
            {
                BattleRosterSlot sampled = Slot(NextRandomByte() % count);
                BattleCombatant? selected = FindAllocatedNativeActor(NativeBattleSlots.Encode(sampled));
                if (!OriginalRandomStatusBlocked(selected, 1))
                {
                    return NativeBattleTargetRequestAdapter.SelectOriginalSlot(sampled);
                }
            }
        }
        return side == BattleSide.Allies ? NativeBattleTargetRequestAdapter.EmptyOriginalSelection : NativeBattleTargetRequestAdapter.SelectOriginalSlot(BattleRosterSlot.Enemy(0));
    }

    private bool OriginalRandomStatusBlocked(BattleCombatant? actor, ushort bits)
    {
        if (actor is not null)
        {
            return (NativeSavedStatusBits(actor) & bits) != 0;
        }
        if (originalParticipantSlotsInitialized)
        {
            // Native participant initialization and release set KO on empty original slots.
            return true;
        }
        throw new NotSupportedException("Original random target selection reached a slot without retained saved status.");
    }
}
