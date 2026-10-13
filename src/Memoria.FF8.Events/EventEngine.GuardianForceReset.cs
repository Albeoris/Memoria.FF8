using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedGuardianForceReset(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.RESETGF)
        {
            return null;
        }
        if (fiber.Stack.Count == 0)
        {
            return "RESETGF requires its original character operand";
        }
        if (services.CharacterVitals is not NativeCharacterVitals state)
        {
            return "RESETGF requires authentic saved-character GF assignments, junction bytes and New Game HP provenance";
        }
        state.Validate();
        return null;
    }

    private int[] ExecuteGuardianForceReset(Fiber fiber)
    {
        int argument = fiber.Stack.Peek();
        NativeCharacterVitals reset = services.CharacterVitals!.ResetGuardianForces(argument);
        try
        {
            services.SetCharacterVitals(reset);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native junction service rejected RESETGF; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, Jsm.Opcode.RESETGF.ToString(), message);
            throw new EventScriptException(message, exception);
        }
        return Pop(fiber, 1);
    }
}
