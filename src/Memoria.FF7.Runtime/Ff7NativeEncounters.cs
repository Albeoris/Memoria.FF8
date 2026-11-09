namespace Memoria.FF7.Runtime;

public sealed record Ff7EncounterState(byte MovementCounter = 0, ushort Danger = 0, byte RollIndex = 0, byte RollOffset = 0, byte FormationIndex = 0, ushort LastFormation = 0);
public sealed record Ff7EncounterResult(Ff7EncounterState State, ushort? Formation, byte Initiative, bool CountStep);

public static class Ff7NativeEncounters
{
    public static Ff7EncounterResult Step(Ff7EncounterState state, Memoria.FF7.Formats.PcFieldEncounterTable table, ReadOnlySpan<byte> random, int moveSpeed, bool disabled, int likelihood = 16, int preemptive = 16)
    {
        if (random.Length != 256 || likelihood is < 0 or > 32 || preemptive is < 0 or > 255)
        {
            throw new InvalidDataException("FFVII encounter randomness or equipment modifiers are invalid.");
        }
        state = state with { MovementCounter = unchecked((byte)(state.MovementCounter + 32)) };
        if (state.MovementCounter != 0)
        {
            return new(state, null, 0, false);
        }
        if (!table.Enabled || disabled)
        {
            return new(state, null, 0, true);
        }
        state = state with { Danger = unchecked((ushort)(state.Danger + (ushort)moveSpeed / table.Rate)) };
        byte roll = NextRoll(ref state, random);
        byte initiative = roll < (preemptive & 127) ? (byte)4 : (byte)0;
        roll = NextRoll(ref state, random);
        if (roll >= (state.Danger * likelihood >> 12))
        {
            return new(state, null, initiative, true);
        }
        int specialRoll = NextFormation(ref state, random) >> 2;
        int cumulative = 0;
        for (int index = 0; index < table.Special.Count; index++)
        {
            int frequency = table.Special[index].Frequency;
            if (frequency >= 32)
            {
                frequency -= 64;
            }
            cumulative = unchecked((byte)(cumulative + ((preemptive & 128) != 0 && index != 2 ? frequency >> 1 : frequency)));
            if (specialRoll < cumulative)
            {
                return new(state, table.Special[index].Formation, index < 2 ? (byte)0 : initiative, true);
            }
        }
        ushort formation = SelectStandard(table, ref state, random);
        if (formation == state.LastFormation)
        {
            formation = SelectStandard(table, ref state, random);
        }
        state = state with { LastFormation = formation };
        return new(state, formation, initiative, true);
    }

    private static byte NextRoll(ref Ff7EncounterState state, ReadOnlySpan<byte> random)
    {
        byte index = unchecked((byte)(state.RollIndex + 1));
        byte offset = index == 0 ? unchecked((byte)(state.RollOffset + 13)) : state.RollOffset;
        state = state with { RollIndex = index, RollOffset = offset };
        return unchecked((byte)(random[index] - offset));
    }

    private static byte NextFormation(ref Ff7EncounterState state, ReadOnlySpan<byte> random)
    {
        byte index = unchecked((byte)(state.FormationIndex + 1));
        state = state with { FormationIndex = index };
        return random[index];
    }

    private static ushort SelectStandard(Memoria.FF7.Formats.PcFieldEncounterTable table, ref Ff7EncounterState state, ReadOnlySpan<byte> random)
    {
        int roll = NextFormation(ref state, random) >> 2;
        int cumulative = 0;
        ushort formation = table.Standard[5].Formation;
        for (int index = 0; index < 5; index++)
        {
            int frequency = table.Standard[index].Frequency;
            if (frequency >= 32)
            {
                frequency -= 64;
            }
            cumulative = unchecked((byte)(cumulative + frequency));
            if (roll < cumulative)
            {
                formation = table.Standard[index].Formation;
                break;
            }
        }
        return formation;
    }
}
