using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeDress(Fiber fiber)
    {
        if (fiber.Stack.Count < 2)
        {
            return "SETDRESS requires its original character and costume operands";
        }
        if (services.CharacterAppearance is not NativeCharacterAppearance appearance || services.CharacterVitals is not NativeCharacterVitals vitals)
        {
            return "SETDRESS requires authentic New Game character costume provenance; no appearance defaults are inferred";
        }
        appearance.Validate();
        byte character = unchecked((byte)fiber.Stack.ElementAt(1));
        return vitals.FindPhysicalRow(character) != 255 ? null : "SETDRESS has no current native saved-record identity for this character";
    }

    private int[] ExecuteNativeDress(Fiber fiber)
    {
        int[] operands = fiber.Stack.Take(2).Reverse().ToArray();
        NativeCharacterAppearance next = services.CharacterAppearance!.SetDressAtRow(services.CharacterVitals!.FindPhysicalRow(unchecked((byte)operands[0])), unchecked((byte)operands[1]));
        try
        {
            services.SetCharacterAppearance(next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native appearance service rejected SETDRESS; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "SETDRESS", message);
            throw new EventScriptException(message, exception);
        }
        return Pop(fiber, 2);
    }
}
