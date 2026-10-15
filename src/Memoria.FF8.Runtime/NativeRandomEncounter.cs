using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldEncounterDefinition(byte Rate, ImmutableArray<ushort> Formations);
public sealed record NativeWorldEncounterDefinition(byte Rate, ImmutableArray<ushort> Formations);

/// <summary>Original process globals for47CA90 and541C80. World seeds are explicit4022C0 clock reads.</summary>
public sealed record NativeRandomEncounterState([property: JsonRequired] ushort FieldStep, [property: JsonRequired] ushort FieldDanger, [property: JsonRequired] byte FieldIndex, [property: JsonRequired] byte FieldOffset, [property: JsonRequired] byte FieldFormationIndex, [property: JsonRequired] ushort LastFieldFormation, [property: JsonRequired] ushort WorldStep, [property: JsonRequired] byte WorldDanger, [property: JsonRequired] byte WorldFormationIndex, [property: JsonRequired] byte WorldFormationOffset, [property: JsonRequired] ushort LastWorldFormation, [property: JsonRequired] bool WorldInitialized)
{
    public static NativeRandomEncounterState FromColdProcess() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);

    public NativeRandomEncounterState InitializeWorld(byte formationClockRead)
    {
        return this with { WorldFormationIndex = formationClockRead, WorldFormationOffset = formationClockRead, WorldInitialized = true };
    }

    public void Validate()
    {
        if (FieldStep > 256 || WorldStep > 256)
        {
            throw new InvalidDataException("Native encounter step counters must remain at or below256 after their callback.");
        }
    }
}

public readonly record struct NativeFieldEncounterInput(bool AcceptedMovement, short NativeMovementSpeed, byte AbilityFlags, bool BattleOff, bool MovieControlled, byte PendingScene, ushort FieldMode, bool ReturnTransition = false);
public readonly record struct NativeWorldEncounterInput(int MovementMagnitude, int EntityType, byte GroundType, byte AbilityFlags, bool OrdinaryControl, bool TransitionPending);
public readonly record struct NativeRandomEncounterResult(NativeRandomEncounterState State, ushort? Formation);

public readonly record struct NativeWorldRandomEncounterResult(NativeRandomEncounterState State, ushort? Formation, byte RandomCursor, byte RandomOffset);

public static class NativeRandomEncounter
{
    public static NativeRandomEncounterResult AdvanceField(NativeRandomEncounterState state, NativeFieldEncounterDefinition definition, ReadOnlySpan<byte> random, NativeFieldEncounterInput input)
    {
        state.Validate();
        ValidateTables(random, definition.Formations, 4);
        if (!input.AcceptedMovement || input.BattleOff || input.MovieControlled || input.ReturnTransition || input.PendingScene is 1 or 7 || input.FieldMode is 2 or 3 or 4 || (input.AbilityFlags & 8) != 0)
        {
            return new(state, null);
        }
        ushort step = unchecked((ushort)(state.FieldStep + ((input.AbilityFlags & 4) != 0 ? definition.Rate >> 1 : definition.Rate)));
        if (step <= 256)
        {
            return new(state with { FieldStep = step }, null);
        }
        ushort danger = unchecked((ushort)(state.FieldDanger + input.NativeMovementSpeed / 1348));
        byte index = unchecked((byte)(state.FieldIndex + 1));
        byte offset = unchecked((byte)(state.FieldOffset + (index == 0 ? 13 : 0)));
        NativeRandomEncounterState next = state with { FieldStep = (byte)step, FieldDanger = danger, FieldIndex = index, FieldOffset = offset };
        if (unchecked((byte)(random[index] + offset)) >= danger)
        {
            return new(next, null);
        }
        byte formationIndex = unchecked((byte)(state.FieldFormationIndex + 1));
        byte roll = random[formationIndex];
        int slot = 0;
        if (roll >= 128 || definition.Formations[0] == state.LastFieldFormation)
        {
            slot = 1;
            if (roll >= 192 || definition.Formations[1] == state.LastFieldFormation)
            {
                slot = 2;
                if (roll >= 240 || definition.Formations[2] == state.LastFieldFormation)
                {
                    slot = 3;
                }
            }
        }
        ushort formation = definition.Formations[slot];
        return new(next with { FieldDanger = 0, FieldFormationIndex = formationIndex, LastFieldFormation = formation }, formation);
    }

    public static NativeWorldRandomEncounterResult AdvanceWorld(NativeRandomEncounterState state, NativeWorldEncounterDefinition? definition, ReadOnlySpan<byte> random, ReadOnlySpan<byte> weights, NativeWorldEncounterInput input, byte randomCursor, byte randomOffset)
    {
        state.Validate();
        if (!input.OrdinaryControl || input.TransitionPending || (input.AbilityFlags & 8) != 0 || (uint)input.EntityType >= 10 && input.EntityType != 128 || input.MovementMagnitude == 0 || input.GroundType is 27 or 28 || definition is null)
        {
            return new(state, null, randomCursor, randomOffset);
        }
        if (!state.WorldInitialized)
        {
            throw new NotSupportedException("Native world encounter initialization requires its actual4022C0 clock reads.");
        }
        ValidateTables(random, definition.Formations, 8);
        int totalWeight = 0;
        foreach (byte weight in weights)
        {
            totalWeight += weight;
        }
        if (weights.Length != 8 || totalWeight < 255)
        {
            throw new InvalidDataException("Native world formation weights require their original eight-byte table.");
        }
        ushort step = unchecked((ushort)(state.WorldStep + ((input.AbilityFlags & 4) != 0 ? 4 : 16)));
        if (step <= 256)
        {
            return new(state with { WorldStep = step }, null, randomCursor, randomOffset);
        }
        byte danger = unchecked((byte)(state.WorldDanger + (input.MovementMagnitude >> 3)));
        byte index = unchecked((byte)(randomCursor + 1));
        byte offset = unchecked((byte)(randomOffset + (index == 0 ? 13 : 0)));
        NativeRandomEncounterState next = state with { WorldStep = 0, WorldDanger = danger };
        if (unchecked((byte)(random[index] - offset)) >= definition.Rate + danger)
        {
            return new(next, null, index, offset);
        }
        byte formationIndex = state.WorldFormationIndex;
        byte formationOffset = state.WorldFormationOffset;
        ushort formation = 0;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            formationIndex = unchecked((byte)(formationIndex + 1));
            if (formationIndex == 0)
            {
                formationOffset = unchecked((byte)(formationOffset + 13));
            }
            int roll = unchecked((byte)(random[formationIndex] - formationOffset));
            int slot = 0;
            while (roll > weights[slot])
            {
                roll -= weights[slot];
                slot++;
            }
            formation = definition.Formations[slot];
            if (formation != state.LastWorldFormation)
            {
                break;
            }
        }
        return new(next with { WorldDanger = 0, WorldFormationIndex = formationIndex, WorldFormationOffset = formationOffset, LastWorldFormation = formation }, formation, index, offset);
    }

    private static void ValidateTables(ReadOnlySpan<byte> random, ImmutableArray<ushort> formations, int count)
    {
        if (random.Length != 256 || formations.IsDefault || formations.Length != count)
        {
            throw new InvalidDataException("Native encounter callbacks require their complete original random and formation tables.");
        }
    }
}


