using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedSetHitPoints(Fiber fiber)
    {
        if (fiber.Stack.Count < 2)
        {
            return "SETHP requires its original physical-row DWORD and HP WORD operands";
        }
        if (fiber.Stack.HasUncertainty)
        {
            return "SETHP requires exact operands; uncertain values are not replaced by a representative";
        }
        if (services.CharacterVitals is not NativeCharacterVitals state)
        {
            return "SETHP requires the authentic eight saved character records; no HP defaults are inferred";
        }
        try
        {
            _ = state.WithSavedHitPoints(fiber.Stack.ElementAt(1), fiber.Stack.Peek());
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteSetHitPoints(Fiber fiber)
    {
        NativeCharacterVitals next = services.CharacterVitals!.WithSavedHitPoints(fiber.Stack.ElementAt(1), fiber.Stack.Peek());
        try
        {
            services.SetCharacterVitals(next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native character service rejected SETHP; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "SETHP", message);
            throw new EventScriptException(message, exception);
        }
        return Pop(fiber, 2);
    }
}
