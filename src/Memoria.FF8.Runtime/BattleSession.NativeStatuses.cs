namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal static ushort NativeSavedStatusBits(BattleCombatant actor)
    {
        ushort bits = actor.NativeStatus0;
        foreach (BattleStatusState effect in actor.Effects)
        {
            bits |= effect.Definition.Kind switch
            {
                BattleStatusKind.Poison => (ushort)2,
                BattleStatusKind.Blind => (ushort)8,
                BattleStatusKind.Silence => (ushort)16,
                _ => (ushort)0
            };
        }
        return bits;
    }

    private static uint NativeBattleStatusBits(BattleCombatant actor)
    {
        uint bits = actor.NativeStatus1;
        foreach (BattleStatusState effect in actor.Effects)
        {
            bits |= effect.Definition.Kind switch
            {
                BattleStatusKind.Sleep => 1u,
                BattleStatusKind.Haste => 2u,
                BattleStatusKind.Slow => 4u,
                BattleStatusKind.Regen => 16u,
                BattleStatusKind.Protect => 32u,
                BattleStatusKind.Shell => 64u,
                _ => 0u
            };
        }
        return bits;
    }

    private static NativeBattleStatusSnapshot CaptureNativeStatus(BattleCombatant actor)
    {
        return new(actor.NativeStatus0, actor.NativeStatus1, actor.NativeStatusTimers.ToArray()) { Resistances = actor.MutableNativeStatusResistances.ToArray(), Presentation = actor.NativePresentationStatus };
    }

    private static int GetNativeStatusResistance(BattleCombatant actor, int index)
    {
        if (actor.NativeStatusResistances.Count != 40 || (uint)index >= 40)
        {
            throw new NotSupportedException("Native status resistance requires its complete original forty-byte owner and valid index.");
        }
        return actor.NativeStatusResistances[index];
    }

    private static void SetNativeAiStatus(BattleCombatant actor, int status, int value)
    {
        if (status is < 0 or >= 48)
        {
            throw new NotSupportedException("Native status operation requires one of the original WORD/DWORD bits.");
        }
        if (status < 16)
        {
            ushort mask = (ushort)(1u << status);
            actor.NativeStatus0 = value == 1 ? (ushort)(actor.NativeStatus0 | mask) : (ushort)(actor.NativeStatus0 & ~mask);
        }
        else
        {
            uint mask = 1u << (status - 16);
            actor.NativeStatus1 = value == 1 ? actor.NativeStatus1 | mask : actor.NativeStatus1 & ~mask;
        }
        if (value != 1)
        {
            BattleStatusKind? projected = status switch
            {
                1 => BattleStatusKind.Poison,
                3 => BattleStatusKind.Blind,
                4 => BattleStatusKind.Silence,
                16 => BattleStatusKind.Sleep,
                17 => BattleStatusKind.Haste,
                18 => BattleStatusKind.Slow,
                20 => BattleStatusKind.Regen,
                21 => BattleStatusKind.Protect,
                22 => BattleStatusKind.Shell,
                _ => null
            };
            if (projected is { } kind)
            {
                actor.Effects.RemoveAll(effect => effect.Definition.Kind == kind);
            }
        }
    }

    private static void RestoreNativeStatus(BattleCombatant actor, NativeBattleStatusSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            throw new InvalidDataException("Battle snapshot requires its explicitly migrated complete native status bank.");
        }
        if (snapshot.Timers is null || snapshot.Timers.Length != 14)
        {
            throw new InvalidDataException("Native battle statuses require all fourteen original signed timer WORDs.");
        }
        if (snapshot.Resistances is null || snapshot.Resistances.Length is not (0 or 40) || snapshot.Resistances.Length != actor.MutableNativeStatusResistances.Length)
        {
            throw new InvalidDataException("Battle snapshot requires its explicitly migrated native status resistance bank.");
        }
        (snapshot.Presentation ?? throw new InvalidDataException("Native B92/B1C mirror owner is missing; migrate the standalone snapshot explicitly.")).Validate();
        actor.NativePresentationStatus = snapshot.Presentation;
        actor.NativeStatus0 = snapshot.Saved;
        actor.NativeStatus1 = snapshot.Battle;
        snapshot.Timers.CopyTo(actor.NativeStatusTimers, 0);
        snapshot.Resistances.CopyTo(actor.MutableNativeStatusResistances, 0);
    }

    private static void RemoveNativeStatuses(BattleCombatant recipient, ushort savedMask, uint battleMask)
    {
        //48F140 preserves timed bits whose sentinel marks a permanent installed effect.
        for (int bit = 0; bit < 14; bit++)
        {
            if ((recipient.NativeStatus1 & (1u << bit)) != 0 && recipient.NativeStatusTimers[bit] == -1111)
            {
                battleMask &= ~(1u << bit);
            }
        }
        recipient.NativeStatus0 &= (ushort)~savedMask;
        recipient.NativeStatus1 &= ~battleMask;
        for (int bit = 0; bit < 14; bit++)
        {
            if ((battleMask & (1u << bit)) != 0)
            {
                recipient.NativeStatusTimers[bit] = -1111;
            }
        }
        recipient.Effects.RemoveAll(effect => effect.Definition.Kind switch
        {
            BattleStatusKind.Poison => (savedMask & 2) != 0,
            BattleStatusKind.Blind => (savedMask & 8) != 0,
            BattleStatusKind.Silence => (savedMask & 16) != 0,
            BattleStatusKind.Sleep => (battleMask & 1) != 0,
            BattleStatusKind.Haste => (battleMask & 2) != 0,
            BattleStatusKind.Slow => (battleMask & 4) != 0,
            BattleStatusKind.Regen => (battleMask & 16) != 0,
            BattleStatusKind.Protect => (battleMask & 32) != 0,
            BattleStatusKind.Shell => (battleMask & 64) != 0,
            _ => false
        });
    }
}
