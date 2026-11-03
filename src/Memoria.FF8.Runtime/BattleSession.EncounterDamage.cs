using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record EncounterHpMutation(BattleCombatant? Actor, BattleCombatant Target, int Amount, string Source, bool CounterEligible, bool PublishDamage, EncounterHpCause Cause, NativeContactWorking? Contact, bool Secondary, BattleActionPresentation? Action, bool Healing = false);

    private sealed class EncounterHitTransaction(ImmutableArray<EncounterDamageActor> actors, ImmutableArray<EncounterDamageRoute> routes)
    {
        public ImmutableArray<EncounterDamageActor> Actors { get; } = actors;
        public ImmutableArray<EncounterDamageRoute> Routes { get; } = routes;
        public List<EncounterHpMutation> Mutations { get; } = [];
        public List<(int Position, NativeContactWorking Contact)> Contacts { get; } = [];
        public int ContactPosition { get; set; }
    }

    private EncounterHitTransaction? encounterHitTransaction;
    private bool committingEncounterHp;

    private bool HasEncounterDamageRoutes => definition.EncounterMod is { DamageRoutes.Count: > 0 };

    private ImmutableArray<EncounterDamageActor> CaptureEncounterDamageActors()
    {
        return combatants.Select(actor => new EncounterDamageActor(actor.Id, Math.Max(0, actor.CurrentHp), actor.IsAlive)).ToImmutableArray();
    }

    private ImmutableArray<EncounterDamageRoute> ResolveEncounterDamageRoutes()
    {
        if (definition.EncounterMod is not { } mod)
        {
            return [];
        }
        ImmutableArray<EncounterDamageRoute>.Builder routes = ImmutableArray.CreateBuilder<EncounterDamageRoute>();
        foreach (EncounterCompiledDamageRoute route in mod.DamageRoutes.Values.OrderBy(route => route.Id, StringComparer.Ordinal))
        {
            string source = ResolveEncounterRole(route.SourceActor) ?? throw new InvalidDataException("Encounter damage source has no allocation: " + route.SourceActor);
            ImmutableArray<string> recipients = route.Recipients.Select(ResolveEncounterRole).Where(id => id is not null).Select(id => id!).ToImmutableArray();
            if (!recipients.IsEmpty)
            {
                routes.Add(new(route.Id, source, recipients));
            }
        }
        return routes.ToImmutable();
    }

    private void BeginEncounterDamageHit()
    {
        if (!HasEncounterDamageRoutes)
        {
            return;
        }
        if (encounterHitTransaction is not null)
        {
            throw new InvalidOperationException("Encounter damage groups cannot nest inside an unpublished HP transaction.");
        }
        encounterHitTransaction = new(CaptureEncounterDamageActors(), ResolveEncounterDamageRoutes());
    }

    private void SetEncounterContactPosition(int position)
    {
        if (encounterHitTransaction is { } transaction)
        {
            transaction.ContactPosition = position;
        }
    }

    private bool RetainEncounterContact(NativeContactWorking contact)
    {
        if (encounterHitTransaction is not { } transaction)
        {
            return false;
        }
        transaction.Contacts.Add((transaction.ContactPosition, contact));
        return true;
    }

    private bool TryApplyEncounterDamage(BattleCombatant? actor, BattleCombatant target, int amount, string source, bool counterEligible, bool publishDamage, EncounterHpCause cause)
    {
        if (committingEncounterHp || !HasEncounterDamageRoutes)
        {
            return false;
        }
        EncounterHpMutation mutation = new(actor, target, amount, source, counterEligible, publishDamage, cause, nativeContactWorking, nativeContactWorking?.CommittingSecondary == true, resolvingNativeAction ?? ActiveAction);
        if (encounterHitTransaction is { } transaction)
        {
            transaction.Mutations.Add(mutation);
            if (nativeContactWorking is { CommittingSecondary: false } contact)
            {
                contact.PrimaryCommitted = true;
            }
            return true;
        }
        if (cause != EncounterHpCause.Damage || amount <= 0)
        {
            return false;
        }
        ImmutableArray<EncounterHpReceipt> receipts = EncounterDamageRouting.Plan([new(0, actor?.Id, target.Id, amount, cause)], CaptureEncounterDamageActors(), ResolveEncounterDamageRoutes());
        if (!receipts.Any(receipt => receipt.RouteId is not null))
        {
            return false;
        }
        List<NativeBattleContactPacket> extra = [];
        CommitEncounterHpMutation(mutation, receipts, extra);
        if (NativeGraphics is not null)
        {
            foreach (NativeBattleContactPacket packet in extra)
            {
                QueueNativeContactPacket(packet);
            }
        }
        return true;
    }

    private bool TryDeferEncounterHealing(BattleCombatant target, int amount, string source)
    {
        if (committingEncounterHp || encounterHitTransaction is not { } transaction)
        {
            return false;
        }
        transaction.Mutations.Add(new(null, target, amount, source, false, false, EncounterHpCause.Healing, nativeContactWorking, nativeContactWorking?.CommittingSecondary == true, resolvingNativeAction ?? ActiveAction, true));
        return true;
    }

    private int CompleteEncounterDamageHit(ImmutableArray<NativeBattleContactPacket>.Builder packets)
    {
        if (encounterHitTransaction is not { } transaction)
        {
            return 0;
        }
        try
        {
            ImmutableArray<EncounterHpReceipt> receipts = EncounterDamageRouting.Plan(transaction.Mutations.Select((mutation, index) => new EncounterHpIntent(index, mutation.Actor?.Id, mutation.Target.Id, Math.Max(0, mutation.Amount), mutation.Cause)), transaction.Actors, transaction.Routes);
            int extraCount = receipts.Count(receipt => receipt.RouteId is not null && receipt.RecipientId != receipt.OriginalTargetId && receipt.AssignedDamage > 0);
            if (packets.Count + extraCount > NativeBattleActionPacketsState.ContactCapacity)
            {
                throw new InvalidDataException("Encounter routed contacts exceed the native contact bank capacity before HP publication.");
            }
            List<NativeBattleContactPacket> extra = [];
            for (int index = 0; index < transaction.Mutations.Count; index++)
            {
                CommitEncounterHpMutation(transaction.Mutations[index], receipts.Where(receipt => receipt.ContactId == index).ToImmutableArray(), extra);
            }
            foreach ((int position, NativeContactWorking contact) in transaction.Contacts)
            {
                contact.PrimaryStatusSnapshot = NativeContactStatusMirror(contact.Recipient);
                NativeBattleContactPacket packet = BuildNativeContactPacket(contact);
                packets[position] = packet;
                ObserveDiagnosticCounterResolvedContact(contact, packet);
            }
            packets.AddRange(extra);
            return extra.Count;
        }
        finally
        {
            encounterHitTransaction = null;
        }
    }

    private void CommitEncounterHpMutation(EncounterHpMutation mutation, ImmutableArray<EncounterHpReceipt> receipts, List<NativeBattleContactPacket> extra)
    {
        NativeContactWorking? previousContact = nativeContactWorking;
        BattleActionPresentation? previousAction = resolvingNativeAction;
        bool previousCommit = committingEncounterHp;
        nativeContactWorking = mutation.Contact;
        resolvingNativeAction = mutation.Action;
        committingEncounterHp = true;
        bool previousSecondary = mutation.Contact?.CommittingSecondary == true;
        if (mutation.Contact is { } contact)
        {
            contact.CommittingSecondary = mutation.Secondary;
        }
        try
        {
            if (mutation.Healing)
            {
                ApplyHealing(mutation.Target, mutation.Amount, mutation.Source);
                RefreshNativePresentationStatus(mutation.Target);
                return;
            }
            bool routed = receipts.Any(receipt => receipt.RouteId is not null);
            if (!routed)
            {
                if (mutation.Cause != EncounterHpCause.Damage || mutation.Target.CurrentHp != 0 || (NativeSavedStatusBits(mutation.Target) & 1) == 0)
                {
                    ApplyDamageCore(mutation.Actor, mutation.Target, mutation.Amount, mutation.Source, mutation.CounterEligible, mutation.PublishDamage);
                }
                RefreshNativePresentationStatus(mutation.Target);
                return;
            }
            if (mutation.Contact is { } originalContact)
            {
                if (mutation.Secondary && originalContact.Secondary is { } secondary)
                {
                    originalContact.Secondary = secondary with { Sequence = 0, Flags2 = 0, HitFlags3 = 0, Amount6 = 0, ExtendedDamageAmount = null };
                }
                else
                {
                    originalContact.Magnitude = 0;
                    originalContact.Sequence = 0;
                    originalContact.Flags = 0;
                    originalContact.HitFlags = 0;
                    originalContact.Finalized = true;
                }
            }
            // The original keeps its hit-only native callback, with zero HP loss and zero return-damage magnitude.
            ApplyDamageCore(mutation.Actor, mutation.Target, 0, mutation.Source, mutation.CounterEligible, false);
            RefreshNativePresentationStatus(mutation.Target);
            foreach (EncounterHpReceipt receipt in receipts.Where(receipt => receipt.RecipientId != receipt.OriginalTargetId && receipt.AssignedDamage > 0))
            {
                BattleCombatant recipient = GetActor(receipt.RecipientId);
                NativeContactWorking? routedContact = mutation.Contact is { } primary ? new(primary.Actor, recipient, primary.Action, primary.SpecialSource)
                {
                    Sequence = primary.Action.DamagePresentation,
                    AttackClass = primary.AttackClass,
                    Magnitude = receipt.AssignedDamage,
                    Finalized = true
                } : null;
                nativeContactWorking = routedContact;
                if (routedContact is not null)
                {
                    StoreNativeAttackClass(recipient, routedContact.AttackClass);
                    NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(routedContact.Sequence, 0, 0, routedContact.AttackClass, routedContact.SpecialSource);
                    routedContact.Sequence = presentation.Sequence;
                    routedContact.Flags = presentation.Flags2;
                    routedContact.HitFlags = presentation.HitFlags3;
                }
                if (recipient.CurrentHp != 0 || (NativeSavedStatusBits(recipient) & 1) == 0)
                {
                    ApplyDamageCore(mutation.Actor, recipient, receipt.AssignedDamage, mutation.Source, mutation.CounterEligible, mutation.PublishDamage);
                }
                RefreshNativePresentationStatus(recipient);
                if (routedContact is not null)
                {
                    extra.Add(BuildNativeContactPacket(routedContact));
                }
                else if (NativeGraphics is not null)
                {
                    (ushort saved, uint battle) = NativeContactStatusMirror(recipient);
                    NativeBattleContactHalf half = NativeBattleContactHalf.FromScratch(checked((byte)recipient.NativeIndex), recipient.IsAlive ? (byte)0 : (byte)3, recipient.IsAlive ? (byte)0 : (byte)8, 0, saved, receipt.AssignedDamage, battle);
                    extra.Add(new(half, new(255, 0, 0, 0, 0, 0, 0)));
                }
            }
        }
        finally
        {
            if (mutation.Contact is { } original)
            {
                original.CommittingSecondary = previousSecondary;
            }
            nativeContactWorking = previousContact;
            resolvingNativeAction = previousAction;
            committingEncounterHp = previousCommit;
        }
    }

    private void RequireEncounterDamagePublicationBoundary()
    {
        if (encounterHitTransaction is not null || committingEncounterHp)
        {
            throw new InvalidOperationException("Capture must wait for the atomic encounter HP/contact transaction to be published.");
        }
    }
}
