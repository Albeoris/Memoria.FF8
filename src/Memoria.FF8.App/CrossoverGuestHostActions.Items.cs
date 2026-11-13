using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    public bool HasOriginItems => encounter.Items is not null;
    public IReadOnlyList<ushort> ItemIds => kit.Commands.Contains((byte)4) && encounter.Items is { } inventory ? ReadOriginItemIds(inventory.Slots, assets.NativeActions) : [];

    internal static IReadOnlyList<ushort> ReadOriginItemIds(IEnumerable<NativeItemStock> stocks, IEnumerable<BattleNativeAction> actions)
    {
        HashSet<ushort> battleItems = actions.Where(action => action.Command == 4).Select(action => action.ActionId).ToHashSet();
        return stocks.Where(stock => stock.ItemId != 0 && stock.Count != 0 && battleItems.Contains(stock.ItemId)).Select(stock => (ushort)stock.ItemId).Distinct().ToArray();
    }
    public int ItemStock(ushort item) => ItemIds.Contains(item) ? encounter.Items!.Slots.SingleOrDefault(stock => stock.ItemId == item)?.Count ?? 0 : 0;
    public string ItemName(ushort item) => OriginalItem(item).Name ?? throw new InvalidDataException("The original item has no localized source name.");

    public bool SupportsItem(ushort item, Ff7BattleGuestAttackTarget target)
    {
        BattleNativeAction? original = assets.NativeActions.SingleOrDefault(action => action.Command == 4 && action.ActionId == item);
        return kit.Commands.Contains((byte)4) && HasOriginItems && original?.TargetFlags is byte flags && CanSelectTarget(flags, target.Slot) && OriginItemUnsupportedReason(original) is null && ItemOriginStatusUnsupportedReason(original, target.Slot) is null && PresentationUnsupportedReason(original, CrossoverGuestAbilityCatalog.Key(4, item), target.Slot) is null;
    }

    internal string? ItemTargetUnsupportedReason(ushort item, int target)
    {
        BattleNativeAction original = OriginalItem(item);
        return OriginItemUnsupportedReason(original) ?? ItemOriginStatusUnsupportedReason(original, target) ?? PresentationUnsupportedReason(original, CrossoverGuestAbilityCatalog.Key(4, item), target);
    }

    public string? ItemUnsupportedReason(ushort item)
    {
        BattleNativeAction original = OriginalItem(item);
        if (OriginItemUnsupportedReason(original) is { } unsupported)
        {
            return unsupported;
        }
        string? reason = null;
        foreach (var actor in host.Actors.Where(actor => original.TargetFlags is byte flags && CanSelectTarget(flags, actor.Actor.Slot)))
        {
            string? current = ItemOriginStatusUnsupportedReason(original, actor.Actor.Slot) ?? PresentationUnsupportedReason(original, CrossoverGuestAbilityCatalog.Key(4, item), actor.Actor.Slot);
            if (current is null)
            {
                return null;
            }
            reason = current;
        }
        return reason ?? "No valid original item target is available.";
    }

    public Ff7BattleGuestItemEffect DescribeItem(ushort item)
    {
        return DescribeOriginItem(OriginalItem(item), kit.StatusPassives);
    }

    internal static string? OriginItemUnsupportedReason(BattleNativeAction original)
    {
        if (original.Command != 4 || original.PresentationOnly || original.DispatchCommand is not (null or 4) || original.AttackType is not (4 or 5 or 32))
        {
            return "The native item requires a combat or progression operation that the recovery bridge cannot represent.";
        }
        if (original.HitCount != 1 || original.TargetFlags is not byte flags || (flags & 2) != 0 || (flags & 0x30) != 0x10 || (flags & 1) != 0 && original.AttackType != 5)
        {
            return "The native item requires a target group or selection process outside the single-recipient recovery bridge.";
        }
        if (original.Element != 0 || (original.Statuses1 & 0x04008000) != 0)
        {
            return "The native item requires elemental or action-control semantics outside the recovery bridge.";
        }
        if (original.AttackType == 32 && original.Power > 16)
        {
            return "The native fractional recovery exceeds the host recovery profile.";
        }
        if (original.AttackType != 5 && original.Hit != 255)
        {
            return "The native item's hit chance requires its source random transaction.";
        }
        if (original.AttackType != 5 && original.StatusAttack is > 0 and <= 100 && (original.Statuses0 != 0 || original.Statuses1 != 0))
        {
            return "The native item's status recovery requires its source random transaction.";
        }
        if (original.AttackType != 5 && original.StatusAttack > 100 && (original.Statuses0 & 1) != 0)
        {
            return "The native item mixes revival with a different recovery formula.";
        }
        if (original.AttackType != 5 && original.Power == 0 && ItemRecoveryStatuses(original) == 0)
        {
            return "The native item has no recovery operation in the host status profile.";
        }
        return null;
    }

    private string? ItemOriginStatusUnsupportedReason(BattleNativeAction original, int target)
    {
        Ff7BattleGuestVitalsBinding? binding = host.GuestVitalsBindings.SingleOrDefault(binding => binding.HostSlot == target);
        return binding is null ? null : OriginItemStatusUnsupportedReason(original, binding.OriginStatuses, binding.OriginBattleStatuses);
    }

    internal static string? OriginItemStatusUnsupportedReason(BattleNativeAction original, ushort savedStatuses, uint battleStatuses)
    {
        if ((savedStatuses & 0x40) != 0)
        {
            return "The recipient's original Zombie status requires recovery behavior outside the host profile.";
        }
        if (original.AttackType == 5 || original.StatusAttack <= 100)
        {
            return null;
        }
        uint common = ItemRecoveryStatuses(original);
        ushort unprojectedSaved = (ushort)(original.Statuses0 & ~Ff7GuestBattleStatuses.ToOrigin(common));
        uint unprojectedBattle = original.Statuses1 & ~Ff7GuestBattleStatuses.ToOriginBattle(common);
        return (unprojectedSaved & savedStatuses) != 0 || (unprojectedBattle & battleStatuses) != 0 ? "The recipient has an original status that this item cannot remove through the host status profile." : null;
    }

    private static uint ItemRecoveryStatuses(BattleNativeAction original)
    {
        return original.StatusAttack > 100 ? Ff7GuestBattleStatuses.ToHostStatuses((ushort)(original.Statuses0 & Ff7GuestBattleStatuses.SupportedOrigin), original.Statuses1) : 0;
    }

    internal static Ff7BattleGuestItemEffect DescribeOriginItem(BattleNativeAction original, uint statusPassives)
    {
        if (OriginItemUnsupportedReason(original) is { } unsupported)
        {
            throw new NotSupportedException(unsupported);
        }
        if (original.AttackType == 5)
        {
            return new(0, ReviveDivisor: (statusPassives & 2) != 0 ? (byte)4 : (byte)8);
        }
        uint removed = ItemRecoveryStatuses(original);
        bool medData = (original.AttackFlags & 3) == 2 && (statusPassives & 2) != 0;
        if (original.AttackType == 32)
        {
            return new(0, removed, MaximumHitPointFraction: original.Power, FractionRecoveryLimit: (original.AttackFlags & 8) != 0 ? (ushort)60000 : (ushort)9999, RecoveryMultiplier: medData ? (byte)2 : (byte)1);
        }
        int maximum = (original.AttackFlags & 8) != 0 ? 60_000 : 9_999;
        return new(checked((ushort)Math.Min(maximum, 50 * original.Power * (medData ? 2 : 1))), removed);
    }

    public void ReserveItem(Ff7BattleGuestItemSelection selection)
    {
        if (encounter.Items is not { } inventory || selection.RequestId != host.RequestId || selection.ActorSlot != ActorSlot || selection.SubmittedAt != host.Tick || ItemStock(selection.ItemId) == 0)
        {
            throw new InvalidDataException("The guest item does not belong to the current original inventory and host turn.");
        }
        encounter.Items = inventory.Reserve(selection);
    }

    public void CancelItem(int target, ushort item)
    {
        encounter.Items = (encounter.Items ?? throw new InvalidDataException("The guest has no origin inventory owner.")).Complete(ActorSlot, target, item, refund: true);
    }

    public void PrepareItem(Ff7BattleGuestPhysicalHit value)
    {
        if (prepared is not null || Invocation is not null || value.PreparedContact is not { Invocation.GuestCommand: 4 })
        {
            throw new InvalidDataException("The guest item requires one prepared host contact.");
        }
        prepared = value;
    }

    private BattleNativeAction OriginalItem(ushort item)
    {
        return assets.NativeActions.SingleOrDefault(action => action.Command == 4 && action.ActionId == item) ?? throw new InvalidDataException("The selected source inventory item has no original battle action record.");
    }

    private void BeginItem(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        if (Invocation is not null || prepared is not { PreparedContact: { } receipt } hit || receipt.Invocation != invocation || encounter.Items is not { } inventory || inventory.PendingFor(ActorSlot) is not { } cost || !SupportsItem(invocation.GuestAbility, target) || cost.Selection.ItemId != invocation.GuestAbility || cost.Selection.TargetSlot != target.Slot)
        {
            throw new InvalidDataException("The guest item requires one reserved origin unit and one prepared host receipt.");
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        NativeBattleGraphicsModel model = attack.Visual?.Model ?? throw new InvalidDataException("The guest item has no loaded original caster.");
        BattleNativeAction original = OriginalItem(invocation.GuestAbility);
        byte flags = receipt.Receipt.Damage.Missed ? (byte)1 : original.AttackType == 5 ? (byte)4 : (byte)0;
        byte healing = receipt.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0;
        NativeBattleContactPresentation contactPresentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, healing, original.AttackType, 255);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(target.Slot == ActorSlot ? (byte)0 : (byte)3, contactPresentation.Sequence, contactPresentation.Flags2, contactPresentation.HitFlags3, (ushort)(receipt.Receipt.Target.Statuses & 1), Math.Abs(receipt.Receipt.Damage.Damage), 0);
        CrossoverNativeMagicPresentationState program = fireProgram.Begin(model with { Index = 0 }, target.Slot, encounter.CrtRandom, contact, original, encounter.GlobalRandom, CreateArena(placement));
        encounter.Items = inventory.Complete(ActorSlot, target.Slot, invocation.GuestAbility, refund: false);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, hit, null, program, false) { ItemCost = cost };
        prepared = null;
    }
}
