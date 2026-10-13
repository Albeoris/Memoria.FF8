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
        if (state is null || operation.Opcode is not (Jsm.Opcode.ADDPARTY or Jsm.Opcode.SUBPARTY or Jsm.Opcode.SETPARTY))
        {
            return null;
        }
        state.Validate();
        if (state.JunctionResetEnabled)
        {
            return "Native party changes with junction-reset flag1CFE978 mask10000 require the actual junction/stat contract";
        }
        if (operation.Opcode == Jsm.Opcode.ADDPARTY && fiber.Stack.Count > 0 && !Scene.Party.Contains(fiber.Stack.Peek()) && (fiber.Stack.Peek() is < 0 or >= 8 || !state.Records.Any(record => record.CharacterId == fiber.Stack.Peek())))
        {
            return "ADDPARTY has no proved native saved character mapping for its original operand";
        }
        if (state.Records.Any(record => !record.HitPoints.IsZero && !record.HitPoints.IsPositive))
        {
            return "Native party stat/HP repair contains zero and positive alternatives; original branch is unresolved";
        }
        if (operation.Opcode == Jsm.Opcode.SETPARTY && fiber.Stack.Count >= 3 && fiber.Stack.Take(3).Any(character => character != 255 && character is < 0 or >= 8))
        {
            return "SETPARTY guest-character combat mapping requires its original saved-record identity contract";
        }
        if (operation.Opcode == Jsm.Opcode.ADDPARTY && fiber.Stack.Count > 0 && !Scene.Party.Contains(unchecked((byte)fiber.Stack.Peek())) && Array.IndexOf(state.CombatParty, (byte)255) < 0)
        {
            return "ADDPARTY has no original empty combat slot; native overflow branch is unsupported";
        }
        return null;
    }

    private void AddCombatPartyCharacter(int character)
    {
        if (services.CharacterVitals is not NativeCharacterVitals state)
        {
            return;
        }
        byte[] slots = state.CombatParty.ToArray();
        slots[Array.IndexOf(slots, (byte)255)] = checked((byte)Array.FindIndex(state.Records, record => record.CharacterId == character));
        services.SetCharacterVitals((state with { CombatParty = slots }).RebuildParty());
    }

    private void SetCombatParty(int[] fieldParty)
    {
        if (services.CharacterVitals is NativeCharacterVitals state)
        {
            // Original51E400 writes combat slots in field order1,0,2.
            services.SetCharacterVitals((state with { CombatParty = [(byte)fieldParty[1], (byte)fieldParty[0], (byte)fieldParty[2]] }).RebuildParty().RepairDyingParty());
        }
    }

    private void RemoveCombatPartyCharacter(byte character)
    {
        if (services.CharacterVitals is NativeCharacterVitals state)
        {
            byte[] slots = state.CombatParty.ToArray();
            int slot = Array.FindIndex(slots, index => index != 255 && state.Records[index].CharacterId == character);
            if (slot >= 0)
            {
                slots[slot] = 255;
            }
            services.SetCharacterVitals((state with { CombatParty = slots }).RebuildParty().RepairDyingParty());
        }
    }
}
