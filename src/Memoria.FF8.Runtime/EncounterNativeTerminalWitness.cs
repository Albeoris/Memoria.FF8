namespace Memoria.FF8.Runtime;

/// <summary>Recognizes the actual native terminal marker, independently of additional enemies' lives.</summary>
public static class EncounterNativeTerminalWitness
{
    public static bool Matches(string encounter, string originalActorId, string actorId, NativeBattleActionHeaderSlot? header, BattleModelOwner actor, byte bodySequence, bool nativeKo, bool nativeSuspended)
    {
        if (encounter != EncounterBindingResolver.Disc1Edea || actorId != originalActorId || !nativeKo || !nativeSuspended || header is null || header.Source != actor || header.Command != 8 || header.Sequence != bodySequence)
        {
            return false;
        }
        ushort actionId = header.Action;
        return (actionId, bodySequence) is (207, 15) or (264, 17);
    }
}

public sealed partial class BattleSession
{
    private void ObserveEncounterNativeTerminal(BattleCombatant actor, byte sequence, NativeBattleGraphicsState graphics)
    {
        if (definition.EncounterMod is not { NativeEvents: not null } || graphics.ActionGraphics is not { } action || nativeActionPackets.ActiveHeader != action.Header)
        {
            return;
        }
        ObserveEncounterNativeTerminal(actor, sequence, nativeActionPackets.ReadHeader(action.Header), CreatePresentationModelOwner(actor));
    }

    private void ObserveEncounterNativeTerminal(BattleCombatant actor, byte sequence, NativeBattleActionHeaderSlot? header, BattleModelOwner owner)
    {
        if (definition.EncounterMod is not { NativeEvents: not null } mod || !EncounterNativeTerminalWitness.Matches(mod.Encounter, mod.OriginalActorId, actor.Id, header, owner, sequence, (NativeSavedStatusBits(actor) & 1) != 0, (actor.NativeCombatantFlags & 0x40) != 0))
        {
            return;
        }
        encounterPolicies ??= new(mod.GenerationIdentity, System.Collections.Immutable.ImmutableDictionary<string, EncounterActionCursor>.Empty);
        encounterPolicies = encounterPolicies with { TerminalStarted = true, TerminalReached = true };
        CleanupEncounterPoliciesAtTerminal();
    }

    private void ValidateEncounterNativeTerminalWitness(BattleEncounterPolicyState? state)
    {
        if (state?.TerminalReached != true)
        {
            return;
        }
        if (!state.TerminalStarted || definition.EncounterMod is not { Encounter: EncounterBindingResolver.Disc1Edea, NativeEvents: not null } mod || !byId.TryGetValue(mod.OriginalActorId, out BattleCombatant? original) || (NativeSavedStatusBits(original) & 1) == 0 || (original.NativeCombatantFlags & 0x40) == 0 || FindNativeOriginalGraphicsBinding(original.Id) is null)
        {
            throw new InvalidDataException("The captured native encounter terminal witness requires its original bound actor, native KO/suspension flags and an admitted terminal owner.");
        }
    }
}
