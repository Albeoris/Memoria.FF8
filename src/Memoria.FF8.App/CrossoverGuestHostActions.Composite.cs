using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private ImmutableArray<Ff7BattleGuestCompositeContact> preparedComposite = [];

    public Ff7BattleGuestCompositeAction? DescribeCompositeAction(ushort ability)
    {
        if (HandlesOperation(ability))
        {
            return null;
        }
        BattleNativeAction original = NativeAbility(ability);
        Ff7BattleGuestCompositeTargetPolicy policy = (original.TargetFlags.GetValueOrDefault() & 2) != 0 ? Ff7BattleGuestCompositeTargetPolicy.RandomSelected : Ff7BattleGuestCompositeTargetPolicy.Selected;
        return new([new(DescribeMagic(ability), original.HitCount, policy)]);
    }

    public IFf7BattleGuestPreparationTransaction BeginCompositePreparation(ushort ability) => new SourcePreparation(this);

    public string? PrepareCompositeUnsupportedReason(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts)
    {
        if (contacts.Count == 0 || contacts.Where((contact, index) => contact.Ordinal != index || contact.HitDescription.PreparedContact is null).Any())
        {
            return "The native process requires an ordered, prepared contact stream.";
        }
        if (attack.Visual is not { } visual)
        {
            return "The original caster model is not ready.";
        }
        try
        {
            Ff7BattleGuestInvocation invocation = contacts[0].HitDescription.PreparedContact!.Invocation;
            BattleNativeAction original = NativeAbility(invocation.GuestAbility);
            foreach (Ff7BattleGuestCompositeContact contact in contacts)
            {
                if (SourceStatusTargetUnsupportedReason(original, contact.HitDescription.PreparedContact!.Invocation.TargetSlot) is { } unsupported)
                {
                    return unsupported;
                }
            }
            CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(host.VisualPlacement(ActorSlot).Position), host.VisualPlacement(ActorSlot).Yaw);
            CrossoverNativeMagicPresentationState state = BeginCompositePresentation(visual.Model, invocation, original, contacts, placement);
            return fireProgram.TryPreflight(state, () => ReadAbilityAnchor(placement, state.HostTarget), () => ReadAbilityTransform(placement, state.HostTarget), CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, placement.Matrix), null, out string? reason,
                readGroupForeign: readForeignGeometry is null && state.BoundTargets.Any(target => encounter.GuestAt(target.HostSlot) is null) ? null : slot => ReadAbilityForeign(placement, slot)) ? null : reason;
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return error.Message;
        }
    }

    public void PrepareComposite(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts)
    {
        if (prepared is not null || !preparedComposite.IsEmpty || !preparedGroup.IsEmpty || Invocation is not null || contacts.Count == 0)
        {
            throw new InvalidOperationException("The native process already owns a prepared invocation.");
        }
        preparedComposite = contacts.ToImmutableArray();
        prepared = preparedComposite[0].HitDescription;
    }

    private CrossoverNativeMagicPresentationState BeginCompositePresentation(NativeBattleGraphicsModel source, Ff7BattleGuestInvocation invocation, BattleNativeAction original, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts, CrossoverGunbladeHostPlacement placement)
    {
        int[] slots = contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).Distinct().Order().ToArray();
        ImmutableArray<CrossoverNativeMagicTarget> targets = CrossoverNativeMagicTarget.Bind(ActorSlot, slots, (slot, index) => NativeContact(contacts.First(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot == slot), index));
        ImmutableArray<CrossoverNativeMagicTarget> stream = contacts.Select(contact =>
        {
            int slot = contact.HitDescription.PreparedContact!.Invocation.TargetSlot;
            byte index = targets.Single(target => target.HostSlot == slot).Contact.ActorIndex;
            return new CrossoverNativeMagicTarget(slot, NativeContact(contact, index));
        }).ToImmutableArray();
        return fireProgram.Begin(source with { Index = 0 }, stream[0].HostSlot, encounter.CrtRandom, stream[0].Contact, original, encounter.GlobalRandom, CreateArena(placement)) with { Targets = targets, Contacts = stream };

        NativeBattleContactHalf NativeContact(Ff7BattleGuestCompositeContact contact, byte index)
        {
            Ff7BattleGuestPreparedContact preparedContact = contact.HitDescription.PreparedContact!;
            byte flags = PreparedContactFlags(original, preparedContact);
            NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, preparedContact.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0, original.AttackType, 255);
            return NativeBattleContactHalf.FromScratch(index, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, (ushort)(preparedContact.Receipt.Target.Statuses & 1), Math.Abs(preparedContact.Receipt.Damage.Damage), 0);
        }
    }

    private sealed class SourcePreparation : IFf7BattleGuestPreparationTransaction
    {
        private readonly CrossoverGuestHostActions owner;
        private readonly NativeCharacterVitals working;
        private readonly CrossoverDrawCastReceipt? draw;
        private bool committed;

        public SourcePreparation(CrossoverGuestHostActions owner)
        {
            this.owner = owner;
            working = owner.encounter.Working.DeepCopy();
            draw = owner.preparedDrawCast;
        }

        public void Commit() => committed = true;

        public void Dispose()
        {
            if (committed)
            {
                return;
            }
            owner.encounter.Working = working;
            owner.preparedDrawCast = draw;
            owner.prepared = null;
            owner.preparedComposite = [];
            owner.preparedGroup = [];
        }
    }
}
