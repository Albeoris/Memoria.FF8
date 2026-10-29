namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed class NativeContactWorking(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction action, byte specialSource)
    {
        public BattleCombatant Actor { get; } = actor;
        public BattleCombatant Recipient { get; } = recipient;
        public byte PrimaryIndex { get; } = checked((byte)recipient.NativeIndex);
        public byte Sequence { get; set; } = action.DamagePresentation;
        public byte Flags { get; set; }
        public byte HitFlags { get; set; }
        public int Magnitude { get; set; }
        public bool Finalized { get; set; }
        public bool FormulaStarted { get; set; }
        public bool PrimaryCommitted { get; set; }
        public bool DiagnosticPreliminary { get; init; }
        public (int Before, int After)? DiagnosticHp { get; set; }
        public BattleDiagnosticCounterPrimary? DiagnosticPrimary { get; set; }
        public (ushort Saved, uint Battle)? PrimaryStatusSnapshot { get; set; }
        public byte AttackClass { get; set; } = (byte)(action.AttackFlags & 3);
        public byte SpecialSource { get; } = specialSource;
        public NativeBattleContactHalf? Secondary { get; set; }
        public bool CommittingSecondary { get; set; }
    }

    private NativeContactWorking? nativeContactWorking;

    private static NativeBattleContactPacket CreateNativeReflectedContact(BattleCombatant recipient, BattleNativeAction native)
    {
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(native.DamagePresentation, 0x31, 4, (byte)(native.AttackFlags & 3), 255);
        RefreshNativePresentationStatus(recipient);
        (ushort saved, uint battle) = NativeContactStatusMirror(recipient);
        NativeBattleContactHalf primary = new(checked((byte)recipient.NativeIndex), presentation.Sequence, presentation.Flags2, presentation.HitFlags3, saved, 0, battle);
        return new(primary, new(255, 0, 0, 0, 0, 0, 0));
    }

    private void ResolveInstalledNativeHit(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native)
    {
        ResolveInstalledNativeHit(actor, recipient, command, action, native, 255);
    }

    private void ResolveInstalledNativeHit(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native, byte specialSource)
    {
        NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, command, action, native, specialSource);
        if (NativeGraphics is not null)
        {
            QueueNativeContactPacket(packet);
        }
    }

    private NativeBattleContactPacket ResolveInstalledNativeHitWithContact(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native, byte specialSource, bool preliminary = false)
    {
        DecrementNativeRemainingHits(recipient);
        bool? deferred = native.Command switch
        {
            237 => true,
            238 or 239 or 249 => false,
            241 => native.ActionId != 65532,
            _ => preliminary ? true : nativeActionScratch.DeferredEffectsE00
        };
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = deferred };
        NativeContactWorking? previous = nativeContactWorking;
        NativeContactWorking contact = new(actor, recipient, preliminary ? native with { AttackFlags = 3, DamagePresentation = 0 } : native, specialSource) { DiagnosticPreliminary = preliminary };
        byte attackClass = preliminary || native.Command is 0 or 14 or 238 or 246 or 252 || native.Command == 241 && native.ActionId is 65530 or 65532 ? (byte)3 : native.Command is 1 or 12 or 28 or 243 or 248 or 251 or 253 ? (byte)0 : (byte)(native.AttackFlags & 3);
        if (native.Command == 6 && command.NativeDraw is not null && !command.DrawCast)
        {
            //48FE20 Stock writes no B99; its zero contact keeps the target's previous attack class.
            attackClass = recipient.LastNativeAction?.AttackType ?? throw new NotSupportedException("Native Stock Draw requires its retained B99 attack class.");
        }
        else
        {
            StoreNativeAttackClass(recipient, attackClass);
        }
        contact.AttackClass = attackClass;
        nativeContactWorking = contact;
        try
        {
            if (preliminary)
            {
                if (native.Command is 14 or 238)
                {
                    contact.Flags = 1;
                }
                FinalizeNativeContactPresentation();
                ApplyDamage(actor, recipient, 0, action.Name, counterEligible: native.Command != 14, publishDamage: false);
            }
            else
            {
                ResolveInstalledNativeHitCore(actor, recipient, command, action, native);
            }
            if (!contact.PrimaryCommitted && recipient.IsActive)
            {
                //Even a formula miss reaches494410 with magnitude0 and its original counter/action metadata.
                FinalizeNativeContactPresentation();
                ApplyDamage(actor, recipient, 0, action.Name, publishDamage: false);
            }
            FinalizeNativeContactPresentation();
            (ushort saved, uint battle) = contact.PrimaryStatusSnapshot ?? NativeContactStatusMirror(recipient);
            NativeBattleContactHalf primary = NativeBattleContactHalf.FromScratch(contact.PrimaryIndex, contact.Sequence, contact.Flags, contact.HitFlags, saved, contact.Magnitude, battle);
            if (contact.Magnitude > short.MaxValue && (contact.HitFlags & 1) == 0)
            {
                primary = primary with { ExtendedDamageAmount = contact.Magnitude };
            }
            NativeBattleContactHalf secondary = contact.Secondary ?? new(255, 0, 0, 0, 0, 0, 0);
            if (contact.Secondary is not null)
            {
                (ushort secondarySaved, uint secondaryBattle) = NativeContactStatusMirror(actor);
                secondary = secondary with { SavedStatuses4 = secondarySaved, BattleStatuses8 = secondaryBattle };
            }
            NativeBattleContactPacket packet = new(primary, secondary);
            ObserveDiagnosticCounterResolvedContact(contact, packet);
            return packet;
        }
        finally
        {
            nativeContactWorking = previous;
        }
    }

    private static (ushort Saved, uint Battle) NativeContactStatusMirror(BattleCombatant actor)
    {
        return (actor.NativePresentationStatus.Saved ?? throw new NotSupportedException("Native contact requires its47E2D0 WORD B92 mirror."), actor.NativePresentationStatus.Battle ?? throw new NotSupportedException("Native contact requires its47E2D0 DWORD B1C mirror."));
    }

    private static void RefreshNativePresentationStatus(BattleCombatant actor)
    {
        ushort saved = NativeSavedStatusBits(actor);
        uint battle = NativeBattleStatusBits(actor);
        if (!actor.IsPlayer && actor.NativeTemplate?.NativeStatusAllocation is { } allocation)
        {
            //47E2D0 excludes original DAT247 innate Zombie/Float from the B92/B1C presentation mirrors.
            saved &= (ushort)~(allocation.Saved & 0x40);
            battle &= ~(allocation.Battle & 0x2000);
        }
        actor.NativePresentationStatus = new(saved, battle);
    }

    private void MarkNativeContactFlags(byte flags, byte hitFlags)
    {
        if (nativeContactWorking is { } contact)
        {
            contact.Flags |= flags;
            contact.HitFlags |= hitFlags;
        }
    }

    private void SetNativeContactSequence(byte sequence)
    {
        if (nativeContactWorking is { } contact)
        {
            contact.Sequence = sequence;
        }
    }

    private void SetNativeContactDamage(int signedDamage)
    {
        if (nativeContactWorking is not { } contact)
        {
            return;
        }
        contact.Magnitude = Math.Abs(signedDamage);
        if (signedDamage < 0)
        {
            contact.HitFlags |= 1;
        }
        else if (signedDamage > 0)
        {
            contact.HitFlags &= 0xFE;
        }
    }

    private void NoteNativeContactCommit(BattleCombatant recipient)
    {
        if (nativeContactWorking is { CommittingSecondary: false } contact && contact.Recipient == recipient)
        {
            contact.PrimaryCommitted = true;
        }
    }

    private void MarkNativeContactStatusResult(BattleCombatant recipient, BattleNativeAction action, bool successful)
    {
        if (nativeContactWorking is { } contact && contact.Recipient == recipient && action.Power == 0 && action.AttackType is not (5 or 6))
        {
            MarkNativeContactFlags(successful ? (byte)1 : (byte)0, successful ? (byte)0 : (byte)4);
        }
    }

    private void FinalizeNativeContactPresentation()
    {
        if (nativeContactWorking is not { Finalized: false } contact)
        {
            return;
        }
        //4922B0 clears rear-facing after its formula (including misses), before494410 captures the contact statuses.
        if (contact.FormulaStarted && contact.AttackClass == 0)
        {
            RemoveNativeStatuses(contact.Recipient, 0, 0x800000);
        }
        NativeBattleContactPresentation result = NativeBattleContactPresentations.Complete(contact.Sequence, contact.Flags, contact.HitFlags, contact.AttackClass, contact.SpecialSource);
        contact.Sequence = result.Sequence;
        contact.Flags = result.Flags2;
        contact.HitFlags = result.HitFlags3;
        contact.Finalized = true;
    }

    private void MarkNativeContactDeath(BattleCombatant target)
    {
        if (nativeContactWorking is not { } contact)
        {
            return;
        }
        byte flags = target.IsPlayer ? (byte)0xC : (target.NativeCombatantFlags & 0x20) == 0 ? (byte)8 : (byte)0;
        if (contact.CommittingSecondary && contact.Secondary is { } secondary)
        {
            contact.Secondary = secondary with { Flags2 = (byte)(secondary.Flags2 | flags), Sequence = !target.IsPlayer && flags != 0 ? (byte)3 : secondary.Sequence };
        }
        else if (target == contact.Recipient)
        {
            FinalizeNativeContactPresentation();
            contact.Flags |= flags;
            if (!target.IsPlayer && flags != 0)
            {
                contact.Sequence = 3;
            }
        }
    }

    private void BeginNativeContactSecondary(BattleCombatant actor, int damage, bool healing)
    {
        if (nativeContactWorking is { } contact)
        {
            contact.PrimaryStatusSnapshot = NativeContactStatusMirror(contact.Recipient);
            contact.Secondary = NativeBattleContactHalf.FromScratch(checked((byte)actor.NativeIndex), 0, 0, healing ? (byte)1 : (byte)0, 0, damage, 0);
            contact.CommittingSecondary = true;
        }
    }

    private void EndNativeContactSecondary()
    {
        if (nativeContactWorking is { } contact)
        {
            RefreshNativePresentationStatus(contact.Actor);
            contact.CommittingSecondary = false;
        }
    }
}
