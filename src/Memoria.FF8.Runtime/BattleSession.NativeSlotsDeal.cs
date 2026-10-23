using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeSlotsDeal(Guid Id, string ActorId, byte CrisisLevel, ImmutableArray<BattleNativeSlotsRoll> Candidates, int RevealMask, int? SelectedIndex)
{
    public int CharacterLevel { get; init; }
}

public sealed partial class BattleSession
{
    private BattleNativeSlotsDeal? pendingSlotsDeal;

    public bool SelphieCardSlots => definition.SelphieCardSlots;

    public BattleNativeSlotsDeal? PendingSlotsDeal => pendingSlotsDeal;

    public void RefreshNativeSlotsDeal()
    {
        if (pendingSlotsDeal is { } deal && !NativeSlotsDealOwnerValid(deal))
        {
            pendingSlotsDeal = null;
            nativeLimitLevels.Remove(deal.ActorId);
            nativeSlotsRolls.Remove(deal.ActorId);
            CloseNativeActionMenu();
        }
    }

    public string GetNativeSlotsCardName(byte magicId)
    {
        return FindInstalledNativeAction(16, magicId).Name ?? throw new NotSupportedException("Slots requires installed spell names.");
    }

    public BattleNativeSlotsDeal BeginNativeSlotsDeal(string actorId)
    {
        RefreshNativeSlotsDeal();
        if (PendingSlotsDeal is { } existing)
        {
            if (existing.ActorId != actorId)
            {
                throw new InvalidOperationException("The pending Slots choice belongs to another actor.");
            }
            return existing;
        }
        BattleCombatant actor = GetActor(actorId);
        byte crisis = GetNativeLimitLevel(actorId);
        if (!SelphieCardSlots || Outcome != BattleOutcome.Active || !CanChoosePlayerCommand(actorId) || !CanNativeBattleActorAct(actor) || actor.NativeLimit is not { Command: 16 } rules || crisis is < 1 or > 4)
        {
            throw new InvalidOperationException("Card Slots requires Selphie's ready owned Limit selection and enabled battle rule.");
        }
        if (nativeGlobalRandom is null)
        {
            throw new NotSupportedException("Card Slots revelation requires its actual saved global random owner.");
        }
        NativeBattleRandomState before = random;
        NativeGlobalRandomState globalBefore = nativeGlobalRandom;
        ulong callsBefore = nativeGlobalRandomCalls;
        try
        {
            ImmutableArray<BattleNativeSlotsRoll>.Builder candidates = ImmutableArray.CreateBuilder<BattleNativeSlotsRoll>(2 * crisis + 1);
            for (int index = 0; index < candidates.Capacity; index++)
            {
                BattleNativeSlotsRoll roll = GenerateNativeSlotsRoll(rules, actor.Level, crisis);
                BattleNativeAction action = FindInstalledNativeAction(16, roll.MagicId);
                ValidateNativeActionContract(action);
                if (roll.Repetitions is < 1 or > 3 || action.TargetFlags is null || (action.TargetFlags.Value & 0x30) == 0x30 || action.ActionId >= 51 && definition.LimitProgress?.SpecialMagicFlags is null)
                {
                    throw new NotSupportedException("Card Slots requires supported native repetitions, targeting and saved special magic ownership.");
                }
                candidates.Add(roll);
            }
            int revealMask = NextNativeGlobalRandom() & ((1 << candidates.Count) - 1);
            nativeSlotsRolls.Remove(actorId);
            pendingSlotsDeal = new(Guid.NewGuid(), actorId, crisis, candidates.MoveToImmutable(), revealMask, null) { CharacterLevel = actor.Level };
            PublishNativeEscapeButtons(0);
            OpenNativeActionMenu();
            return pendingSlotsDeal;
        }
        catch
        {
            random = before;
            nativeGlobalRandom = globalBefore;
            nativeGlobalRandomCalls = callsBefore;
            pendingSlotsDeal = null;
            throw;
        }
    }

    public bool SelectNativeSlotsCard(Guid identity, int index)
    {
        RefreshNativeSlotsDeal();
        if (PendingSlotsDeal is not { } deal || deal.Id != identity || deal.SelectedIndex is not null || index < 0 || index >= deal.Candidates.Length)
        {
            return false;
        }
        pendingSlotsDeal = deal with { SelectedIndex = index };
        return true;
    }

    public bool SubmitNativeSlotsCard(Guid identity)
    {
        RefreshNativeSlotsDeal();
        if (PendingSlotsDeal is not { SelectedIndex: int selected } deal || deal.Id != identity)
        {
            return false;
        }
        // Apply the existing command pipeline transactionally so failed admission cannot publish a partial command.
        try
        {
            BattleSession candidate = Restore(Capture(), handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler));
            candidate.pendingSlotsDeal = null;
            BattleNativeSlotsRoll roll = deal.Candidates[selected];
            candidate.nativeSlotsRolls[deal.ActorId] = roll;
            candidate.QueueNativePlayerCommand(deal.ActorId, deal.ActorId, 16, roll.MagicId, crisis: deal.CrisisLevel);
            candidate.CloseNativeActionMenu();
            PublishCandidate(candidate);
            return true;
        }
        catch
        {
            pendingSlotsDeal = null;
            nativeLimitLevels.Remove(deal.ActorId);
            nativeSlotsRolls.Remove(deal.ActorId);
            CloseNativeActionMenu();
            throw;
        }
    }

    private bool NativeSlotsDealOwnerValid(BattleNativeSlotsDeal deal)
    {
        return Outcome == BattleOutcome.Active && !HasPendingNativeOutcome && byId.TryGetValue(deal.ActorId, out BattleCombatant? actor) && actor.IsActive && actor.IsAlive && actor.IsPlayer && actor.NativeLimit?.Command == 16 && CanChoosePlayerCommand(actor.Id) && CanNativeBattleActorAct(actor) && GetNativeLimitLevel(actor.Id) == deal.CrisisLevel;
    }

    private void RequireNoPendingSlotsDeal()
    {
        RefreshNativeSlotsDeal();
        if (PendingSlotsDeal is not null)
        {
            throw new InvalidOperationException("The pending Slots card choice owns battle command input.");
        }
    }

    private void RestoreNativeSlotsDeal(BattleNativeSlotsDeal? deal)
    {
        pendingSlotsDeal = null;
        if (deal is null)
        {
            return;
        }
        if (!SelphieCardSlots || deal.Id == Guid.Empty || deal.CrisisLevel is < 1 or > 4 || deal.CharacterLevel is < 1 or > 100 || deal.Candidates.IsDefault || deal.Candidates.Length != 2 * deal.CrisisLevel + 1 || deal.RevealMask < 0 || deal.RevealMask >= (1 << deal.Candidates.Length) || deal.SelectedIndex is int selected && (selected < 0 || selected >= deal.Candidates.Length) || nativeGlobalRandom is null || !NativeSlotsDealOwnerValid(deal) || nativeSlotsRolls.ContainsKey(deal.ActorId))
        {
            throw new InvalidDataException("Saved Slots deal contradicts its immutable choice or current actor ownership.");
        }
        foreach (BattleNativeSlotsRoll roll in deal.Candidates)
        {
            if (roll is null || roll.Repetitions is < 1 or > 3)
            {
                throw new InvalidDataException("Saved Slots candidate has invalid native repetitions.");
            }
            _ = FindInstalledNativeAction(16, roll.MagicId);
        }
        pendingSlotsDeal = deal;
    }
}
