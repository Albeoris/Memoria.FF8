namespace Memoria.FF8.Runtime;

/// <summary>Recognizes the actual native terminal marker, independently of additional enemies' lives.</summary>
public static class EncounterNativeTerminalWitness
{
    public static bool Matches(string encounter, string originalActorId, string actorId, NativeBattleActionHeaderSlot? header, byte physicalActor, byte bodySequence, bool nativeKo, bool nativeSuspended)
    {
        if (encounter != EncounterBindingResolver.Disc1Edea || actorId != originalActorId || !nativeKo || !nativeSuspended || header is null || header.ReadByte(0) != physicalActor || header.ReadByte(1) != 8 || header.ReadByte(2) != bodySequence)
        {
            return false;
        }
        ushort actionId = NativeBattleActionGraphics.Word(header, 4);
        return (actionId, bodySequence) is (207, 15) or (264, 17);
    }
}

public sealed partial class BattleSession
{
    private void ObserveEncounterNativeTerminal(BattleCombatant actor, byte sequence, NativeBattleGraphicsState graphics)
    {
        if (definition.EncounterMod is not { NativeEvents: not null } mod || graphics.ActionGraphics is not { } action || action.ActorIndex != actor.NativeIndex || nativeActionPackets.ActiveHeaderIndex != action.HeaderIndex || !EncounterNativeTerminalWitness.Matches(mod.Encounter, mod.OriginalActorId, actor.Id, nativeActionPackets.Headers[action.HeaderIndex], action.ActorIndex, sequence, (NativeSavedStatusBits(actor) & 1) != 0, (actor.NativeCombatantFlags & 0x40) != 0))
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
