namespace Memoria.FF8.Runtime;

/// <summary>Recognizes the actual native terminal marker, independently of additional enemies' lives.</summary>
public static class EncounterNativeTerminalWitness
{
    public static bool Matches(EncounterNativeEventBinding binding, string actorId, int modelId, NativeBattleActionHeaderSlot? header, BattleModelOwner actor, byte bodySequence, bool nativeKo, bool nativeSuspended)
    {
        if (actorId != binding.ActorId || modelId != binding.ModelId || !nativeKo || !nativeSuspended || header is null || header.Source != actor || header.Command != 8 || header.Sequence != bodySequence)
        {
            return false;
        }
        ushort actionId = header.Action;
        return (actionId, bodySequence) is (207, 15) or (264, 17);
    }
}

public sealed partial class BattleSession
{
    private void ObserveEncounterNativeTerminal(BattleCombatant actor, byte sequence, NativeBattleActionHeaderSlot? header, BattleModelOwner owner)
    {
        if (definition.EncounterMod is not { NativeEvents: { } binding } mod || !EncounterNativeTerminalWitness.Matches(binding, actor.Id, actor.ModelId, header, owner, sequence, (NativeSavedStatusBits(actor) & 1) != 0, (actor.NativeCombatantFlags & 0x40) != 0))
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
        if (!state.TerminalStarted || definition.EncounterMod is not { NativeEvents: { } binding } mod || !byId.TryGetValue(binding.ActorId, out BattleCombatant? original) || (NativeSavedStatusBits(original) & 1) == 0 || (original.NativeCombatantFlags & 0x40) == 0 || !HasEncounterNativeTerminalBinding(mod, original))
        {
            throw new InvalidDataException("The captured native encounter terminal witness requires its original bound actor, native KO/suspension flags and an admitted terminal owner.");
        }
    }

    private bool HasEncounterNativeTerminalBinding(EncounterCompiledDefinition mod, BattleCombatant original)
    {
        return mod.NativeEvents is { } binding && binding.ActorId == original.Id && binding.ModelId == original.ModelId &&
            original.Slot is { Side: BattleSide.Enemies };
    }

    private bool HasPendingExpandedEncounterNativeTerminal()
    {
        if (encounterPolicies is not { TerminalStarted: true, TerminalReached: false } ||
            definition.EncounterMod?.NativeEvents is not { } binding || !byId.TryGetValue(binding.ActorId, out BattleCombatant? actor) ||
            actor.ModelId != binding.ModelId)
        {
            return false;
        }
        byte command = 0;
        ushort action = 0;
        byte sequence = 0;
        PrepareNativeAiAbility(actor, binding.LowHpAbility, ref command, ref action, ref sequence);
        return ActiveAction is { NativeAiCallbackStage: 1 } active && Matches(active.ActorId, active.TargetId, active.NativeQueuedCommand, active.ActionId, active.NativeSequence) ||
            actions.Any(queued => queued.NativeAiCallbackStage == 1 && Matches(queued.ActorId, queued.TargetId, queued.NativeQueuedCommand, queued.ActionId, queued.NativeSequence));

        bool Matches(string source, string target, byte? nativeCommand, int actionId, byte? nativeSequence)
        {
            return source == binding.ActorId && target == binding.ActorId && nativeCommand == command && actionId == action && nativeSequence == sequence;
        }
    }
}
