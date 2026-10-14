using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedCharacterVitals(Fiber fiber, JsmOperation operation)
    {
        NativeCharacterVitals? state = services.CharacterVitals;
        if (operation.Opcode == Jsm.Opcode.DYING)
        {
            if (state is null)
            {
                return "DYING requires authentic New Game character HP/status and raw combat-party provenance; no health defaults are inferred";
            }
            state.Validate();
            return state.CombatParty.Where(slot => slot != 255).Any(slot => !state.Records[slot].HitPoints.IsZero && !state.Records[slot].HitPoints.IsPositive) ? "DYING has both zero and positive HP alternatives; native branch cannot be selected" : null;
        }
        if (operation.Opcode is not (Jsm.Opcode.ADDPARTY or Jsm.Opcode.SUBPARTY or Jsm.Opcode.SETPARTY))
        {
            return null;
        }
        if (state is null)
        {
            return "Native party changes require authentic saved character rows and combat slots";
        }
        state.Validate();
        int count = operation.Opcode == Jsm.Opcode.SETPARTY ? 3 : 1;
        if (fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact))
        {
            return "Native party changes require their complete exact DWORD operands";
        }
        if (operation.Opcode == Jsm.Opcode.ADDPARTY && !Scene.Party.Contains(unchecked((byte)fiber.Stack.Peek())) && fiber.Stack.Peek() < 0)
        {
            return "ADDPARTY negative DWORD addresses an unowned native membership row";
        }
        if (operation.Opcode == Jsm.Opcode.ADDPARTY && !Scene.Party.Contains(unchecked((byte)fiber.Stack.Peek())) && fiber.Stack.Peek() >= 8 && (services.WorldControls is null || !services.CanStoreNativeEventOwners))
        {
            return "ADDPARTY guest flags require their current event-word owner and atomic publication";
        }
        if (operation.Opcode == Jsm.Opcode.ADDPARTY && fiber.Stack.Count > 0 && !Scene.Party.Contains(unchecked((byte)fiber.Stack.Peek())) && Scene.Party.Contains(255) && Array.IndexOf(state.CombatParty, (byte)255) < 0)
        {
            return "ADDPARTY has no original empty combat slot; native overflow branch is unsupported";
        }
        if (operation.Opcode == Jsm.Opcode.ADDPARTY)
        {
            if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
            {
                return "ADDPARTY requires its exact original character DWORD";
            }
            if (!Scene.Party.Contains(unchecked((byte)fiber.Stack.Peek())))
            {
                try
                {
                    _ = PrepareAddedPartyCharacter(fiber.Stack.Peek(), Scene.Party.Contains(255));
                }
                catch (NotSupportedException exception)
                {
                    return exception.Message;
                }
            }
        }
        else
        {
            try
            {
                _ = operation.Opcode == Jsm.Opcode.SETPARTY ? PrepareSetCombatParty(fiber.Stack.Take(3).Reverse().ToArray()) : PrepareRemovedCombatCharacter(unchecked((byte)fiber.Stack.Peek()));
            }
            catch (NotSupportedException exception)
            {
                return exception.Message;
            }
        }
        return null;
    }

    private NativeCharacterVitals PrepareAddedPartyCharacter(int character, bool insert)
    {
        NativeCharacterVitals state = services.CharacterVitals!;
        byte[] slots = state.CombatParty.ToArray();
        if (insert)
        {
            int empty = Array.IndexOf(slots, (byte)255);
            byte row = state.FindPhysicalRow(unchecked((byte)character));
            if (empty < 0)
            {
                throw new NotSupportedException("ADDPARTY insertion requires its native empty combat slot and actual identity lookup.");
            }
            slots[empty] = row;
        }
        if (character >= 8)
        {
            // Native51E160 sets the full-DWORD masked-shift bit and returns without rebuilding.
            return state with { CombatParty = slots };
        }
        // Native51E160 performs membership and rebuild even with a full field
        // party, provided the requested character was not already present.
        return (state.WithPartyMemberFlags(character) with { CombatParty = slots }).RebuildParty();
    }

    private int[] ExecuteAddPartyCharacter(Fiber fiber)
    {
        int character = fiber.Stack.Peek();
        if (!Scene.Party.Contains(unchecked((byte)character)))
        {
            int empty = Scene.Party.IndexOf(255);
            NativeCharacterVitals next = PrepareAddedPartyCharacter(character, empty >= 0);
            if (character >= 8)
            {
                NativeWorldControlState controls = services.WorldControls!;
                CommitNativePartyOwners(fiber, next, controls with { EventFlags104 = controls.EventFlags104 | (1u << (character - 8)) });
            }
            else
            {
                CommitSavedCharacters(fiber, next, Jsm.Opcode.ADDPARTY);
            }
            if (empty >= 0)
            {
                Scene.Party[empty] = unchecked((byte)character);
            }
        }
        return Pop(fiber, 1);
    }

    private NativeCharacterVitals PrepareSetCombatParty(int[] fieldParty)
    {
        NativeCharacterVitals state = services.CharacterVitals!;
        if (state.DreamEnabled)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                if (fieldParty[slot] == 255)
                {
                    state = state.ResetGuardianForcesAtRow(state.FindPhysicalRow(unchecked((byte)Scene.Party[slot])));
                }
            }
            byte[] mapped = fieldParty.Select(character => state.FindPhysicalRow(unchecked((byte)character))).ToArray();
            return (state with { CombatParty = [mapped[1], mapped[0], mapped[2]] }).RebuildParty().RepairDyingParty();
        }
        // Original51E400 writes combat slots in field order1,0,2, using BYTEs.
        return (state with { CombatParty = [unchecked((byte)fieldParty[1]), unchecked((byte)fieldParty[0]), unchecked((byte)fieldParty[2])] }).RebuildParty().RepairDyingParty();
    }

    private int[] ExecuteSetParty(Fiber fiber)
    {
        int[] operands = fiber.Stack.Take(3).Reverse().ToArray();
        CommitSavedCharacters(fiber, PrepareSetCombatParty(operands), Jsm.Opcode.SETPARTY);
        Scene.Party.Clear();
        Scene.Party.AddRange(operands.Select(character => (int)unchecked((byte)character)));
        return Pop(fiber, 3);
    }

    private NativeCharacterVitals PrepareRemovedCombatCharacter(byte character)
    {
        NativeCharacterVitals state = services.CharacterVitals!;
        byte[] slots = state.CombatParty.ToArray();
        int slot = Array.FindIndex(slots, index => index != 255 && state.Records[index].CharacterId == character);
        if (slot >= 0)
        {
            if (state.DreamEnabled)
            {
                state = state.ResetGuardianForcesAtRow(slots[slot]);
            }
            slots[slot] = 255;
        }
        return (state with { CombatParty = slots }).RebuildParty().RepairDyingParty();
    }

    private void CommitNativePartyOwners(Fiber fiber, NativeCharacterVitals next, NativeWorldControlState controls)
    {
        try
        {
            services.SetNativeEventOwners(controls, characterVitals: next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native party owner publication rejected; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, nameof(Jsm.Opcode.ADDPARTY), message);
            throw new EventScriptException(message, exception);
        }
    }
}
