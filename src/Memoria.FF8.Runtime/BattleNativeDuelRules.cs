using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeDuelChoice(byte MoveSetId, byte ActionId, string Name, bool Learned, bool Finisher, ImmutableArray<ushort> Buttons);

public static class NativeBattleDuelRules
{
    public static byte InitialMoveSet(BattleNativeLimitDefinition rules, byte crisis)
    {
        Validate(rules);
        if (crisis is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(crisis));
        }
        return rules.DuelTimersAndStarts[2 * (crisis - 1) + 1];
    }

    public static short Timer(BattleNativeLimitDefinition rules, byte crisis)
    {
        _ = InitialMoveSet(rules, crisis);
        return (short)(4 * rules.DuelTimersAndStarts[2 * (crisis - 1)]);
    }

    public static byte ActionId(BattleNativeLimitDefinition rules, byte moveSet)
    {
        Validate(rules);
        int offset = 4 * moveSet;
        return offset < rules.DuelMoveSets.Length ? rules.DuelMoveSets[offset] : throw new InvalidDataException("Native Duel move set lies outside kernel23.");
    }

    public static ImmutableArray<BattleNativeDuelChoice> Choices(BattleNativeLimitDefinition rules, byte moveSet, ushort learned, IReadOnlyList<BattleNativeAction> actions)
    {
        _ = ActionId(rules, moveSet);
        List<BattleNativeDuelChoice> result = [];
        for (int choice = 1; choice <= 3; choice++)
        {
            byte nextSet = rules.DuelMoveSets[4 * moveSet + choice];
            byte id = ActionId(rules, nextSet);
            BattleNativeAction action = actions.Single(candidate => candidate.Command == 241 && candidate.ActionId == id);
            if (action.RawRecord.Length != 32)
            {
                throw new InvalidDataException("Native Duel move requires its complete kernel22 record.");
            }
            List<ushort> buttons = [];
            for (int button = 0; button < 5; button++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(action.RawRecord.AsSpan().Slice(16 + 2 * button));
                if (word != ushort.MaxValue)
                {
                    buttons.Add((ushort)(word & 0xF0FF));
                }
            }
            bool finisher = (action.RawRecord[17] & 1) != 0;
            result.Add(new(nextSet, id, action.Name ?? $"Duel {id}", (learned & (1 << id)) != 0, finisher, buttons.ToImmutableArray()));
        }
        return result.ToImmutableArray();
    }

    /// <summary>4AF840 compares the entire raw pulse suffix; manual matching intentionally does not test the learned flag.</summary>
    public static bool Matches(BattleNativeDuelChoice choice, ImmutableArray<ushort> ring, byte cursor)
    {
        if (ring.IsDefault || ring.Length != 8 || cursor >= 8 || choice.Buttons.IsDefaultOrEmpty || choice.Buttons.Length > 5)
        {
            throw new InvalidDataException("Native Duel input requires the full eight-WORD ring and its current cursor.");
        }
        for (int index = choice.Buttons.Length - 1; index >= 0; index--)
        {
            if (ring[cursor] != choice.Buttons[index])
            {
                return false;
            }
            cursor = (byte)((cursor - 1) & 7);
        }
        return true;
    }

    private static void Validate(BattleNativeLimitDefinition rules)
    {
        if (rules.DuelTimersAndStarts.IsDefault || rules.DuelTimersAndStarts.Length != 8 || rules.DuelMoveSets.IsDefaultOrEmpty || rules.DuelMoveSets.Length % 4 != 0)
        {
            throw new InvalidDataException("Native Duel requires its original crisis timer/start table and four-byte move sets.");
        }
    }
}
