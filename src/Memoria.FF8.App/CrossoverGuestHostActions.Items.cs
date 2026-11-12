using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    public bool HasOriginItems => encounter.Items is not null;
    public IReadOnlyList<ushort> ItemIds => kit.Commands.Contains((byte)4) && HasOriginItems ? [1, 7, 9, 16] : [];
    public int ItemStock(ushort item) => ItemIds.Contains(item) ? encounter.Items!.Slots.SingleOrDefault(stock => stock.ItemId == item)?.Count ?? 0 : 0;
    public string ItemName(ushort item) => OriginalItem(item).Name ?? throw new InvalidDataException("The original item has no localized source name.");

    public bool SupportsItem(ushort item, Ff7BattleGuestAttackTarget target)
        => ItemIds.Contains(item) && OriginalItem(item).TargetFlags is byte flags && CanSelectTarget(flags, target.Slot) && PresentationUnsupportedReason(OriginalItem(item), CrossoverGuestAbilityCatalog.Key(4, item), target.Slot) is null;

    internal string? ItemTargetUnsupportedReason(ushort item, int target) => PresentationUnsupportedReason(OriginalItem(item), CrossoverGuestAbilityCatalog.Key(4, item), target);

    public string? ItemUnsupportedReason(ushort item)
    {
        string? reason = null;
        foreach (var actor in host.Actors.Where(actor => OriginalItem(item).TargetFlags is byte flags && CanSelectTarget(flags, actor.Actor.Slot)))
        {
            string? current = PresentationUnsupportedReason(OriginalItem(item), CrossoverGuestAbilityCatalog.Key(4, item), actor.Actor.Slot);
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

    internal static Ff7BattleGuestItemEffect DescribeOriginItem(BattleNativeAction original, uint statusPassives)
    {
        ushort item = original.ActionId;
        if (item == 7)
        {
            return new(0, ReviveDivisor: (statusPassives & 2) != 0 ? (byte)4 : (byte)8);
        }
        if (item == 9)
        {
            uint removed = Ff7GuestBattleStatuses.ToHostStatuses((ushort)(original.Statuses0 & Ff7GuestBattleStatuses.SupportedOrigin), original.Statuses1);
            return new(0, removed, MaximumHitPointFraction: original.Power);
        }
        if (item == 16)
        {
            uint removed = Ff7GuestBattleStatuses.FromOrigin((ushort)(original.Statuses0 & Ff7GuestBattleStatuses.SupportedOrigin));
            if ((original.Statuses1 & 1) != 0)
            {
                removed |= Ff7NativeBattleActionResolver.Sleep;
            }
            return new(0, removed);
        }
        bool medData = (original.AttackFlags & 3) == 2 && (statusPassives & 2) != 0;
        return new(checked((ushort)(50 * original.Power * (medData ? 2 : 1))));
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
        BattleNativeAction? original = assets.NativeActions.SingleOrDefault(action => action.Command == 4 && action.ActionId == item);
        bool supported = item switch
        {
            1 => original is { MagicId: 50, AttackType: 4, Power: 4, TargetFlags: 20, Hit: 255, StatusAttack: 0, Statuses0: 0, Statuses1: 0 },
            7 => original is { MagicId: 5, AttackType: 5, Power: 0, TargetFlags: 21, Hit: 255, StatusAttack: 200, Statuses0: 1, Statuses1: 0 },
            9 => original is { AttackType: 32, Power: 16, TargetFlags: 20, AttackFlags: 0xE2, Hit: 255, StatusAttack: 200, Statuses0: 0x7E, Statuses1: 0x0100560D },
            16 => original is { MagicId: 67, AttackType: 4, Power: 0, TargetFlags: 20, Hit: 255, StatusAttack: 200, Statuses0: 0x7E, Statuses1: 0x5201 },
            _ => false
        };
        return supported ? original! : throw new NotSupportedException("The guest item requires an admitted original battle item record.");
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
        CrossoverNativeMagicPresentationState program = fireProgram.Begin(model with { Index = 0 }, target.Slot, encounter.CrtRandom, contact, original, encounter.GlobalRandom);
        encounter.Items = inventory.Complete(ActorSlot, target.Slot, invocation.GuestAbility, refund: false);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, hit, null, program, false) { ItemCost = cost };
        prepared = null;
    }
}
