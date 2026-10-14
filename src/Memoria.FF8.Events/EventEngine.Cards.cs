using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedCardMemoryAccess(JsmOperation operation)
    {
        if (services.CardInventory is null)
        {
            return null;
        }
        int width = NativeMemoryWidth(operation.Opcode);
        if (width != 0 && operation.Parameter <= 1522 && (long)operation.Parameter + width > 1408)
        {
            return "Native card memory aliases require their canonical byte ownership; direct VM memory access is unsupported before mutation";
        }
        return null;
    }

    private string? UnsupportedCardOperand(Fiber fiber, Jsm.Opcode opcode)
    {
        if (fiber.Stack.Count == 0)
        {
            return $"{opcode} requires its original DWORD card ID";
        }
        if (services.CardInventory is not NativeCardInventory cards || opcode == Jsm.Opcode.GETCARD && !services.CanStoreCardInventory)
        {
            return $"{opcode} requires the authentic native card inventory and its required service capability; recreate the journal";
        }
        cards.Validate();
        if (variables.Keys.Any(address => address is >= 1405 and <= 1522) || services.NativeVariableValues?.Any(pair => pair.Key <= 1522 && (long)pair.Key + pair.Value.Width > 1408) == true)
        {
            return $"{opcode} overlaps a separate VM memory record; card byte ownership must be established by the authentic journal";
        }
        if (!fiber.Stack.PeekValue().IsExact)
        {
            return $"{opcode} requires its exact consumed DWORD card ID";
        }
        return (uint)fiber.Stack.Peek() >= NativeCardInventory.CardCount ? $"{opcode} ID is outside the original 110 card records; native out-of-bounds memory is unsupported" : null;
    }

    private int ExecuteHowManyCard(Fiber fiber)
    {
        int cardId = fiber.Stack.Peek();
        int result = services.CardInventory!.AvailableCount(cardId);
        Pop(fiber, 1);
        fiber.Results[0] = result;
        return cardId;
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
