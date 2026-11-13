using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private ImmutableArray<Ff7BattleGuestPhysicalHit> preparedGroup = [];

    public string? PrepareGroupUnsupportedReason(IReadOnlyList<Ff7BattleGuestPhysicalHit> values)
    {
        if (values.Count == 0 || values.Any(hit => hit.PreparedContact is null))
        {
            return "The native group requires prepared contacts for its selected targets.";
        }
        Ff7BattleGuestInvocation invocation = values[0].PreparedContact!.Invocation;
        if (attack.Visual is not { } visual)
        {
            return "The original caster model is not ready.";
        }
        try
        {
            BattleNativeAction original = NativeAbility(invocation.GuestAbility);
            ImmutableArray<CrossoverNativeMagicTarget> targets = PresentationTargets(values, original);
            CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(host.VisualPlacement(ActorSlot).Position), host.VisualPlacement(ActorSlot).Yaw);
            CrossoverNativeMagicPresentationState state = fireProgram.Begin(visual.Model with { Index = 0 }, invocation.TargetSlot, encounter.CrtRandom, targets[0].Contact, original, encounter.GlobalRandom, CreateArena(placement)) with { Targets = targets };
            return fireProgram.TryPreflight(state, () => ReadAbilityAnchor(placement, invocation.TargetSlot), () => ReadAbilityTransform(placement, invocation.TargetSlot), CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, placement.Matrix), null, out string? reason,
                readGroupForeign: slot => CrossoverNativeForeignTarget.Read(host, slot, (readForeignGeometry ?? throw new NotSupportedException("The host group requires posed actor geometry."))(slot), placement.Matrix)) ? null : reason;
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return error.Message;
        }
    }

    public void PrepareGroup(IReadOnlyList<Ff7BattleGuestPhysicalHit> values)
    {
        if (prepared is not null || !preparedGroup.IsEmpty || Invocation is not null || values.Count == 0 || values.Any(hit => hit.PreparedContact is null))
        {
            throw new InvalidOperationException("The guest group already has an active prepared receipt.");
        }
        preparedGroup = values.ToImmutableArray();
        prepared = preparedGroup[0];
    }

    private ImmutableArray<CrossoverNativeMagicTarget> PresentationTargets(IReadOnlyList<Ff7BattleGuestPhysicalHit> values, BattleNativeAction original)
    {
        int[] slots = values.Select(value => value.PreparedContact!.Invocation.TargetSlot).ToArray();
        return CrossoverNativeMagicTarget.Bind(ActorSlot, slots, (slot, index) =>
        {
            Ff7BattleGuestPreparedContact receipt = values.Single(value => value.PreparedContact!.Invocation.TargetSlot == slot).PreparedContact!;
            byte flags = receipt.Receipt.Damage.Missed ? (byte)1 : original.AttackType is 5 or 6 ? (byte)4 : (byte)0;
            NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, receipt.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0, original.AttackType, 255);
            NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(index, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, (ushort)(receipt.Receipt.Target.Statuses & 1), Math.Abs(receipt.Receipt.Damage.Damage), 0);
            return contact;
        });
    }
}
