using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleCombatant
{
    //48C620 clears WORD1D27BDC for all seven physical rows at fresh battle initialization.
    internal ushort? NativeReturnDamage { get; set; } = 0;
}

public sealed partial class BattleSession
{
    //487DF0's current synchronous callback owner; queued copies retain factual provenance only.
    private byte? nativeAiCallbackStage;

    private void AccumulateNativeReturnDamage(BattleCombatant recipient, BattleCombatant source, int magnitude)
    {
        if (!recipient.IsPlayer || recipient.Slot is null || source.IsPlayer || magnitude == 0 ||
            recipient.NativePlayerStatRules is not { NativeStatusPassiveFlags: uint flags } || (flags & 8) == 0)
        {
            return;
        }
        ushort before = recipient.NativeReturnDamage ?? throw new NotSupportedException("Return Damage requires the retained participant WORD1D27BDC.");
        recipient.NativeReturnDamage = unchecked((ushort)(before + magnitude));
    }

    private void PrepareNativeAiReturnDamage(BattleCombatant actor, BattleAttackCommand command)
    {
        if (actor.IsPlayer || command.NativeAiCallbackStage is not byte stage || command.NativeQueuedCommand == 247 ||
            command.NativeGraphic is not null || command.NativeAction?.PresentationOnly == true || IsNativeReturnDamageAction(command.NativeAction) ||
            (NativeSavedStatusBits(actor) & 1) != 0)
        {
            return;
        }

        //487DF0 calls 483EF0 after each admitted common command, before the interpreter resumes.
        //Its three original participant words generalize to the actual occupied allied rows.
        BattleCombatant[] party = allies.Where(member => member.Slot is not null).OrderBy(member => member.Slot!.Value.Position).ToArray();
        ushort total = 0;
        foreach (BattleCombatant member in party)
        {
            ushort word = member.NativeReturnDamage ?? throw new NotSupportedException("Native483EF0 requires the retained Return Damage WORD for every occupied party row.");
            total = unchecked((ushort)(total + word));
        }
        actor.NativeReturnDamage = total;
        foreach (BattleCombatant member in party)
        {
            member.NativeReturnDamage = 0;
        }
        if (stage == 3 || total == 0)
        {
            return;
        }
        BattleNativeAction native = NativeReturnDamageAction();
        ProduceExpandedAiCallbackCommand(new(actor.Id, actor.Id, SelectBattleTarget(actor, eligibility: BattleTargetEligibility.SavedStatus), BattleAbility.EnemyAbility, native.ActionId)
        {
            NativeQueuedCommand = native.Command,
            NativeAction = native,
            NativeSequence = 0,
            NativeAiCallbackStage = stage
        });
    }

    private static bool IsNativeReturnDamageAction(BattleNativeAction? action)
    {
        return action is { Command: 0, ActionId: 5 };
    }

    private void ValidateNativeAiCommandStage(BattleCombatant actor, BattleAbility ability, byte? stage)
    {
        if (stage == 1 && ability == BattleAbility.Magic && HasEncounterPolicy(actor))
        {
            return;
        }
        if (stage is not null && (stage > 4 || actor.NativeAi is null || ability != BattleAbility.EnemyAbility))
        {
            throw new InvalidDataException("Native AI command completion requires its actual originating callback0..4 and installed actor program.");
        }
    }

    private bool TryResolveNativeReturnDamage(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleNativeAction native)
    {
        if (!IsNativeReturnDamageAction(native))
        {
            return false;
        }
        ushort accumulated = recipient.NativeReturnDamage ?? throw new NotSupportedException("Native0/5 requires its actual retained Return Damage WORD.");
        recipient.NativeReturnDamage = 0;
        int damage = Math.Min(definition.Tuning.MaximumDamage, accumulated >> 2);
        SetNativeContactDamage(damage);
        FinalizeNativeContactPresentation();
        RecordNativeActionMemory(actor, recipient, command);
        ApplyDamage(actor, recipient, damage, "Return Damage");
        return true;
    }

    private static BattleNativeAction NativeReturnDamageAction()
    {
        //48FE20 executable command0/5 has no kernel record: flags3, zero status/element/presentation, WORD>>2.
        return new(0, 5, -1, -1) { Name = "Return Damage", MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }
}
