using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeDuelChoice(byte MoveSetId, byte ActionId, string Name, bool Learned, bool Finisher, ImmutableArray<ushort> Buttons);

public static class NativeBattleDuelRules
{
    public static BattleNativeDuelState AcknowledgeWidgetReady(BattleNativeDuelState state)
    {
        return state with
        {
            Phase = BattleNativeDuelPhase.Ready,
            Selection1D28DEF = 254,
            WidgetPhase = 0,
            WidgetRevealQ12 = 0,
            Ui = new(BattleTimedInputUiPhase.Opening),
            InputRing = Enumerable.Repeat((ushort)0, 8).ToImmutableArray(),
            InputCursor = 0,
            PendingPulse = 0
        };
    }

    public static BattleNativeDuelState AdvanceWidget(BattleNativeDuelState state, bool frame33, ushort rawPulse, bool automatic, IReadOnlyList<BattleNativeDuelChoice> choices, Func<ushort> nextUiRandom)
    {
        if (state.WidgetClosed || state.Phase == BattleNativeDuelPhase.Opening)
        {
            return state;
        }
        byte latch = frame33 ? (byte)4 : (byte)Math.Max(0, state.PublicationLatch - 1);
        state = state with { PublicationLatch = latch };
        switch (state.WidgetPhase)
        {
            case 0:
                return frame33 ? state with { WidgetPhase = 1 } : state;
            case 1:
                return Reveal(state, 1024);
            case 2:
                return Reveal(state, checked((ushort)(state.WidgetRevealQ12 + 1024)));
            case 3:
                state = state with { WidgetPhase = 4, AutomaticCountdown = 120 };
                break;
            case 4:
                break;
            case 9:
                return state;
            case 11:
                return frame33 ? state with { WidgetPhase = 12, ClosingCountdown = 24, WidgetRevealQ12 = 0 } : state with { WidgetRevealQ12 = 0 };
            case 12:
                short closing = unchecked((short)(state.ClosingCountdown - 1));
                return closing < 0 && frame33 ? state with { WidgetPhase = 13, ClosingCountdown = closing, Ui = new(BattleTimedInputUiPhase.Closed) } : state with { ClosingCountdown = closing };
            default:
                throw new InvalidDataException("Duel widget has an unsupported callback phase.");
        }
        if (latch != 0)
        {
            state = state with { RemainingTimer = checked((short)(state.RemainingTimer - 1)), AutomaticCountdown = unchecked((short)(state.AutomaticCountdown - 1)) };
            if (state.RemainingTimer <= 0)
            {
                return RequestWidgetClose(state);
            }
        }
        if (automatic)
        {
            if (state.AutomaticCountdown != 30)
            {
                return state;
            }
            BattleNativeDuelChoice[] learned = choices.Where(choice => choice.Learned).ToArray();
            if (learned.Length == 0)
            {
                return Select(state, choices[0]);
            }
            int index = nextUiRandom() / (0x8000 / learned.Length);
            if (index >= learned.Length)
            {
                throw new NotSupportedException("Native Duel's automatic draw reached an uninitialized choice byte.");
            }
            return Select(state, learned[index]);
        }
        if (rawPulse != 0)
        {
            byte cursor = (byte)((state.InputCursor + 1) & 7);
            state = state with { InputCursor = cursor, InputRing = state.InputRing.SetItem(cursor, rawPulse) };
        }
        BattleNativeDuelChoice? match = choices.FirstOrDefault(choice => Matches(choice, state.InputRing, state.InputCursor));
        return match is null ? state : Select(state, match);
    }

    public static BattleNativeDuelState RequestWidgetClose(BattleNativeDuelState state)
    {
        return state with { Selection1D28DEF = 255, Phase = BattleNativeDuelPhase.Finisher, WidgetPhase = 11, Ui = new(BattleTimedInputUiPhase.Closing) };
    }

    private static BattleNativeDuelState Reveal(BattleNativeDuelState state, ushort value)
    {
        bool complete = value == 4096;
        return state with { WidgetPhase = complete ? (byte)3 : (byte)2, WidgetRevealQ12 = value, Ui = complete ? new(BattleTimedInputUiPhase.Active) : new(BattleTimedInputUiPhase.Opening, value / 1024) };
    }

    private static BattleNativeDuelState Select(BattleNativeDuelState state, BattleNativeDuelChoice choice)
    {
        return state with
        {
            MoveSetId = choice.MoveSetId,
            Selection1D28DEF = choice.MoveSetId,
            Phase = choice.Finisher ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect,
            WidgetPhase = choice.Finisher ? (byte)11 : (byte)9,
            WidgetRevealQ12 = choice.Finisher ? state.WidgetRevealQ12 : (ushort)0,
            Ui = choice.Finisher ? new(BattleTimedInputUiPhase.Closing) : state.Ui
        };
    }

    public static BattleNativeAction Control(ushort actionId, ushort effect)
    {
        if (actionId == 65530 ? effect is < 6 or > 9 : actionId != 65532 || effect != 0)
        {
            throw new InvalidDataException("Native Duel control requires FFFA with its original finisher6..9 or FFFC with effect0.");
        }
        return new(241, actionId, -1, -1) { PresentationSecondarySource = actionId == 65532, MagicId = effect, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }

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
