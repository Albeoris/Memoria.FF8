using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedGetCard(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            return "GETCARD requires its original DWORD card ID";
        }
        if (services.CardInventory is not NativeCardInventory cards || !services.CanStoreCardInventory)
        {
            return "GETCARD requires the authentic native card inventory and a persistent inventory service; recreate the journal";
        }
        cards.Validate();
        return (uint)fiber.Stack.Peek() >= NativeCardInventory.CardCount ? "GETCARD ID is outside the original 110 card records; native out-of-bounds memory is unsupported" : null;
    }

    private int ExecuteGetCard(Fiber fiber)
    {
        NativeCardInventory next = services.CardInventory!.Acquire(fiber.Stack.Peek(), out int result);
        try
        {
            services.SetCardInventory(next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native card inventory service rejected the update; PC and operand preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "GETCARD", message);
            throw new EventScriptException(message, exception);
        }
        int cardId = Pop(fiber, 1)[0];
        fiber.Results[0] = result;
        return cardId;
    }
}
