namespace Memoria.FF8.Runtime;

public sealed record EncounterBindingContext(string Profile, int? FieldId = null, int? RequestedEncounter = null, ushort? RequestFlags = null);

public sealed record EncounterBindingResult(string Alias, string OriginalActorId, byte CastingSequence, byte TerminalAbility, byte[] TauntAbilities, bool StoryRequestVerified);

/// <summary>Resolves semantic encounter identities against selected resource contracts.</summary>
public static class EncounterBindingResolver
{
    public const string Disc1Edea = "ff8.disc1.edea";

    public static int ResolveEncounterId(string alias)
    {
        return alias == Disc1Edea ? 136 : throw new InvalidDataException($"Encounter alias '{alias}' is not supported.");
    }

    public static EncounterBindingResult Resolve(string alias, BattleDefinition definition, EncounterBindingContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        if (alias != Disc1Edea)
        {
            throw new InvalidDataException($"Encounter alias '{alias}' is not supported.");
        }
        if (!string.Equals(context.Profile, "Classic", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Encounter '{alias}' requires the supported Classic resource profile; selected profile is '{context.Profile}'.");
        }
        if (definition.EncounterId != 136 || definition.StageId != 147)
        {
            throw new InvalidDataException($"Encounter '{alias}' requires formation 136 and stage 147; selected formation/stage is {definition.EncounterId}/{definition.StageId}.");
        }
        bool hasContext = context.FieldId.HasValue || context.RequestedEncounter.HasValue || context.RequestFlags.HasValue;
        if (hasContext && (context.FieldId != 768 || context.RequestedEncounter != 136 || context.RequestFlags != 18))
        {
            throw new InvalidDataException($"Encounter '{alias}' requires the complete story request field 768, encounter 136, flags 18; supplied context is {context.FieldId}/{context.RequestedEncounter}/{context.RequestFlags}.");
        }
        BattleCombatantDefinition[] originals = definition.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot == 0 && actor.ModelId == 88).ToArray();
        if (originals.Length != 1)
        {
            throw new InvalidDataException($"Encounter '{alias}' requires exactly one original actor at scene slot 0 with model 88; found {originals.Length}.");
        }
        BattleCombatantDefinition original = originals[0];
        NativeBattleAiDefinition ai = original.NativeAi ?? throw new InvalidDataException($"Encounter '{alias}' original actor '{original.Id}' lacks native lifecycle AI.");
        if (ai.Scripts.Length != 5 || !IsTerminalCallback(ai.Scripts[3]))
        {
            throw new InvalidDataException($"Encounter '{alias}' original actor '{original.Id}' has an incompatible terminal callback; expected self-targeted ability 11 followed by return.");
        }
        RequireAbility(ai, 11, 264, 17, "terminal");
        RequireAbility(ai, 8, 205, 13, "first taunt");
        RequireAbility(ai, 9, 206, 14, "second taunt");
        RequireAbility(ai, 10, 207, 15, "third taunt");
        if (!HasTauntAnchors(ai.Scripts[1]))
        {
            throw new InvalidDataException($"Encounter '{alias}' has incompatible native taunt branches; the opening, one-time party-count and low-HP anchors are ambiguous.");
        }
        return new(alias, original.Id, 11, 11, [8, 9, 10], hasContext);
    }

    private static bool IsTerminalCallback(byte[] code)
    {
        return code.Length >= 5 && code[0] == 4 && code[1] == 200 && code[2] == 12 && code[3] == 11 && code.AsSpan(4).IndexOfAnyExcept((byte)0) < 0;
    }

    private static bool HasTauntAnchors(byte[] code)
    {
        return Matches(code, 0, [2, 220, 200, 0, 0, 0, 18, 0])
            && Matches(code, 8, [14, 220, 1, 4, 200, 12, 8])
            && Matches(code, 26, [2, 221, 200, 0, 0, 0, 22, 0])
            && Matches(code, 34, [2, 6, 200, 4, 2, 0, 11, 0])
            && Matches(code, 42, [14, 221, 1, 4, 200, 12, 9, 0])
            && Matches(code, 56, [2, 0, 0, 1, 10, 0, 19, 0])
            && Matches(code, 64, [2, 6, 200, 4, 2, 0, 8, 0])
            && Matches(code, 72, [4, 200, 12, 10, 0]);
    }

    private static bool Matches(byte[] code, int offset, ReadOnlySpan<byte> expected)
    {
        return offset <= code.Length - expected.Length && code.AsSpan(offset, expected.Length).SequenceEqual(expected);
    }

    private static void RequireAbility(NativeBattleAiDefinition ai, int index, ushort action, byte sequence, string role)
    {
        if (ai.Abilities.Length <= index || ai.Abilities[index] is not { Command: 8 } ability || ability.Id != action || ability.Sequence != sequence)
        {
            throw new InvalidDataException($"Encounter '{Disc1Edea}' has an incompatible {role} ability {index}; expected command 8, action {action}, sequence {sequence}.");
        }
    }
}
