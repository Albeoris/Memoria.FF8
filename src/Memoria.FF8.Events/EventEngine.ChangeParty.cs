using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeChangeParty(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || !HasCanonicalPartySlots(Scene) || fiber.Stack.Count < 2 || fiber.Stack.Capture().Take(2).Any(value => !value.IsExact))
        {
            return "Native CHANGEPARTY requires its exact slot/character DWORD operands and current three saved field-party bytes";
        }
        //52CBE0 checks the low identity BYTE before51E350 dereferences the
        //full DWORD slot. An already-present character is an immediate no-op.
        if (Scene.Party.Contains(unchecked((byte)fiber.Stack.Peek())))
        {
            return null;
        }
        if ((uint)fiber.Stack.ElementAt(1) >= 3)
        {
            return "Native CHANGEPARTY full DWORD slot addresses outside its owned field-party bank";
        }
        if (services.CharacterVitals is not { } state)
        {
            return "Native CHANGEPARTY requires authentic saved character identities and raw combat slots";
        }
        try
        {
            _ = PrepareChangedCombatParty(fiber.Stack.ElementAt(1), unchecked((byte)fiber.Stack.Peek()), state);
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private NativeCharacterVitals PrepareChangedCombatParty(int fieldSlot, byte character, NativeCharacterVitals state)
    {
        state.Validate();
        byte[] combat = state.CombatParty.ToArray();
        int combatSlot = Array.FindIndex(combat, row => row != 255 && state.Records[row].CharacterId == Scene.Party[fieldSlot]);
        if (combatSlot < 0)
        {
            combatSlot = Array.IndexOf(combat, (byte)255);
            if (combatSlot < 0)
            {
                combatSlot = fieldSlot;
            }
        }
        //51E350 uses52CC50(old identity),52CC00(FF),then the original slot;
        //52CC20 maps the new BYTE identity to its actual physical saved row.
        combat[combatSlot] = state.FindPhysicalRow(character);
        return (state with { CombatParty = combat }).RebuildParty();
    }

    private int[] ExecuteNativeChangeParty(Fiber fiber)
    {
        byte character = unchecked((byte)fiber.Stack.Peek());
        if (!Scene.Party.Contains(character))
        {
            int fieldSlot = fiber.Stack.ElementAt(1);
            CommitSavedCharacters(fiber, PrepareChangedCombatParty(fieldSlot, character, services.CharacterVitals!), Jsm.Opcode.CHANGEPARTY);
            Scene.Party[fieldSlot] = character;
        }
        //Native return2 continues this callback. Retained actor bindings and
        //JUNCTION backup188..193 are independent and remain untouched.
        return Pop(fiber, 2);
    }
}
