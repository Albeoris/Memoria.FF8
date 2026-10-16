using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Kernel29 byte15, copied to BYTE1CF8B23 and used by482F70/482F80.</summary>
public sealed record NativeBattleSupportRules([property: JsonRequired] byte Cooldown);

/// <summary>Battle-local482F70/482F80 owners, independent of gameplay or graphics frame counts.</summary>
public sealed record NativeBattleSupportState([property: JsonRequired] bool EntryInitialized, [property: JsonRequired] ushort Cooldown, [property: JsonRequired] byte GilgameshUsed)
{
    [JsonRequired]
    public byte? SelectedAction { get; init; }
    [JsonRequired]
    public ushort? SelectedTargets { get; init; }
}

public sealed partial class BattleSession
{
    private NativeBattleSupportState? nativeSupport;

    private void InitializeNativeSupportState()
    {
        nativeSupport = definition.NativeSupportRules is null ? null : new(false, 0, 0);
    }

    private void RestoreNativeSupport(NativeBattleSupportState? saved)
    {
        if ((saved is null) != (definition.NativeSupportRules is null) || saved is { GilgameshUsed: > 1 } || saved is { EntryInitialized: true } && saved.Cooldown > definition.NativeSupportRules!.Cooldown || saved is { EntryInitialized: false } && (saved.Cooldown != 0 || saved.GilgameshUsed != 0 || saved.SelectedAction is not null || saved.SelectedTargets is not null))
        {
            throw new InvalidDataException("Native support callback state differs from its original battle-local owners.");
        }
        nativeSupport = saved;
    }

    private void ValidateNativeSupportEntryOwner()
    {
        if (nativeSupport is { } support && NativeGraphics is { } graphics && support.EntryInitialized != graphics.EntryReady)
        {
            throw new InvalidDataException("Native support initialization differs from its original entry callback completion.");
        }
    }

    private BattleCombatant NativeSupportActor()
    {
        return combatants.Where(actor => actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.NativeIndex).FirstOrDefault() ?? throw new NotSupportedException("Native486080 requires an allocated physical party actor for support invocation.");
    }

    private byte NativeSupportFlags(byte mask)
    {
        if ((definition.NativeSavedBattleFlagsKnownMask & mask) != mask)
        {
            throw new NotSupportedException("Native automatic support requires its actual saved1CFE97A flags.");
        }
        return CurrentNativeSavedBattleFlags();
    }

    private void InitializeNativeSupportCallbacks()
    {
        if (nativeSupport is not { EntryInitialized: false })
        {
            return;
        }
        byte flags = NativeSupportFlags(0x0A);
        //47CCB0 case3 calls482E00,4831F0,482F70 in this order.
        if ((flags & 2) != 0 && NativeOdinTargetsEligible() && NextRandomByte() <= 32)
        {
            QueueNativeAutomaticGuardian(0);
        }
        byte gilgameshUsed = 0;
        if ((flags & 8) != 0 && NextRandomByte() <= 8)
        {
            QueueNativeAutomaticGuardian(checked((byte)(NextRandomByte() / 64 + 7)));
            gilgameshUsed = 1;
        }
        nativeSupport = nativeSupport! with { EntryInitialized = true, Cooldown = definition.NativeSupportRules!.Cooldown, GilgameshUsed = gilgameshUsed };
    }

    private bool NativeOdinTargetsEligible()
    {
        for (int slot = 3; slot < 7; slot++)
        {
            BattleCombatant? actor = combatants.SingleOrDefault(actor => actor.NativeIndex == slot);
            //48C5C0 marks an unallocated physical monster slot KO;482E00 never reads its resistance.
            if (actor is null || (NativeSavedStatusBits(actor) & 1) != 0)
            {
                continue;
            }
            if (actor.MutableNativeStatusResistances is not { Length: 40 } resistances)
            {
                throw new NotSupportedException("Native Odin admission requires the monster's actual40-byte resistance bank.");
            }
            //1D27E28 is enemy0 actor+168, resistance bank+24 (base+144).
            if (resistances[24] >= 200)
            {
                return false;
            }
        }
        return true;
    }

    private void QueueNativeAutomaticGuardian(byte id)
    {
        NativeBattleSupportState state = nativeSupport ?? throw new InvalidOperationException("Native automatic support lacks its battle-local selector owner.");
        nativeSupport = state with { SelectedAction = id };
        QueueNativeCallback(NativeSupportActor(), 7);
    }

    private void ExpandNativeAutomaticGuardian(BattleCombatant actor)
    {
        byte id = nativeSupport?.SelectedAction ?? throw new InvalidDataException("Native stage7 lacks its actual selector BYTE1D28E14.");
        BattleNativeAction action = FindInstalledNativeAction(245, id);
        byte flags = action.TargetFlags ?? throw new InvalidDataException("Native independent GF row lacks its original target flags.");
        ushort mask = NativeAutomaticActionTargetMask(flags);
        QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AutomaticGuardian, 245, id, mask);
        if (id == 1)
        {
            QueueNativePlayerReaction(actor, BattleNativePlayerReaction.PhoenixRevival, 0, 8, 0xC007);
        }
    }

    private bool TryQueueNativePhoenixRecovery()
    {
        if (nativeSupport is null)
        {
            return false;
        }
        //486450 first waits for pending death/revival callbacks;483270 requires an allocated KO, non-Petrify party row and a capable enemy.
        if (!combatants.Any(actor => !actor.IsPlayer && actor.NativeIndex is >= 3 and < 7 && (NativeSavedStatusBits(actor) & 5) == 0) || !combatants.Any(actor => actor.IsPlayer && actor.IsActive && (NativeSavedStatusBits(actor) & 5) == 1))
        {
            return false;
        }
        // NG-005: Original483270 excludes encounter317
        if ((NativeSupportFlags(4) & 4) == 0 || definition.EncounterId == 317 || NextRandomByte() > 64)
        {
            return false;
        }
        QueueNativeAutomaticGuardian(1);
        return true;
    }

    private static BattleNativeAction NativePhoenixRevivalAction()
    {
        //48FE20 command0/8 sets element0, status attack255, saved status1 and battle status0 before491940.
        //This executable command has no kernel record; empty immutable records express that source explicitly.
        return new(0, 8, -1, -1) { MagicId = 0, AttackType = 5, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 255, Statuses0 = 1, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }
}
