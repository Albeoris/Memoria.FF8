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

    private void AccumulateNativeReturnDamage(BattleCombatant recipient, BattleCombatant? source, int magnitude)
    {
        if (!recipient.IsPlayer || source?.NativeIndex is not (>= 3 and < 7) || magnitude == 0 || recipient.NativePlayerStatRules is not { NativeStatusPassiveFlags: uint flags } || (flags & 8) == 0)
        {
            return;
        }
        ushort before = recipient.NativeReturnDamage ?? throw new NotSupportedException("Return Damage requires the actual accumulated participant WORD1D27BDC; migrate its unknown value explicitly.");
        recipient.NativeReturnDamage = unchecked((ushort)(before + magnitude));
    }

    private static bool IsNativeReturnDamageAction(BattleNativeAction? action)
    {
        return action is { Command: 0, ActionId: 5 };
    }

    private static void ValidateNativeAiCommandStage(BattleCombatant actor, BattleAbility ability, byte? stage)
    {
        if (stage is not null && (stage > 4 || actor.NativeAi is null || ability != BattleAbility.EnemyAbility))
        {
            throw new InvalidDataException("Native AI command completion requires its actual originating callback0..4 and installed actor program.");
        }
    }

    private void CompleteNativeAiReturnDamage(BattleCombatant actor, BattleActionPresentation command)
    {
        if (command.NativeAiCallbackStage is not byte stage)
        {
            if (actor.NativeAi is not null && command.Ability == BattleAbility.EnemyAbility && command.NativeQueuedCommand is not null && !IsNativeReturnDamageAction(command.NativeAction))
            {
                throw new NotSupportedException("Native AI command completion lacks its original callback stage for483EF0; migrate the queued invocation explicitly.");
            }
            return;
        }
        //483EF0's draw-follow-up gate BYTE1D28E0C is clear for these original interpreter-produced commands.
        //247 is the separately owned reflected follow-up; it must not aggregate a second Return Damage command.
        if (command.NativeQueuedCommand == 247 || (NativeSavedStatusBits(actor) & 1) != 0)
        {
            return;
        }
        BattleCombatant[] party = combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 3 && candidate != actor).OrderBy(candidate => candidate.NativeIndex).ToArray();
        if (party.Any(candidate => candidate.NativeReturnDamage is null))
        {
            throw new NotSupportedException("Native483EF0 requires each retained party Return Damage WORD, including KO rows; unknown old values cannot be replaced with zero.");
        }
        ushort total = 0;
        foreach (BattleCombatant member in party)
        {
            total = unchecked((ushort)(total + member.NativeReturnDamage!.Value));
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
        //The original command's header is already published. This synchronous0/5 follows it before the interpreter resumes.
        QueueNativeCommand(actor, 0, 5, 0, checked((ushort)(1 << actor.NativeIndex)));
    }

    private bool TryResolveNativeReturnDamage(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleNativeAction native)
    {
        if (!IsNativeReturnDamageAction(native))
        {
            return false;
        }
        ushort accumulated = recipient.NativeReturnDamage ?? throw new NotSupportedException("Native0/5 requires its actual retained Return Damage WORD.");
        recipient.NativeReturnDamage = 0;
        int damage = Math.Min(9999, accumulated >> 2);
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
