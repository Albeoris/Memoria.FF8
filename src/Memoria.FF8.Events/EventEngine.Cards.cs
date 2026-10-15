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
        int operandCount = opcode == Jsm.Opcode.SETCARD ? 2 : 1;
        if (fiber.Stack.Count < operandCount)
        {
            return $"{opcode} requires its original DWORD card ID and consumed operands";
        }
        if (services.CardInventory is not NativeCardInventory cards || opcode is Jsm.Opcode.GETCARD or Jsm.Opcode.SETCARD && !services.CanStoreCardInventory)
        {
            return $"{opcode} requires the authentic native card inventory and its required service capability; the snapshot is missing required native state";
        }
        cards.Validate();
        if (EventMemory.Overlaps(1408, 115))
        {
            return $"{opcode} overlaps a separate VM memory record; card byte ownership must be established by the saved native state";
        }
        if (fiber.Stack.Capture().Take(operandCount).Any(value => !value.IsExact))
        {
            return $"{opcode} requires its exact consumed DWORD operands";
        }
        int cardId = fiber.Stack.Peek();
        bool validId = opcode == Jsm.Opcode.WHERECARD ? cardId < NativeCardInventory.CardCount : (uint)cardId < NativeCardInventory.CardCount;
        return validId ? null : $"{opcode} ID is outside the original 110 card records; native out-of-bounds memory is unsupported";
    }

    private int ExecuteWhereCard(Fiber fiber)
    {
        int cardId = fiber.Stack.Peek();
        int result = services.CardInventory!.Location(cardId);
        Pop(fiber, 1);
        fiber.Results[0] = result;
        return cardId;
    }

    private int[] ExecuteSetCard(Fiber fiber)
    {
        int[] operands = fiber.Stack.Take(2).ToArray();
        NativeCardInventory next = services.CardInventory!.Transfer(operands[0], operands[1], out int result);
        StoreCardInventory(fiber, Jsm.Opcode.SETCARD, next);
        Pop(fiber, 2);
        fiber.Results[0] = result;
        return operands;
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
        StoreCardInventory(fiber, Jsm.Opcode.GETCARD, next);
        int cardId = Pop(fiber, 1)[0];
        fiber.Results[0] = result;
        return cardId;
    }

    private void StoreCardInventory(Fiber fiber, Jsm.Opcode opcode, NativeCardInventory next)
    {
        try
        {
            services.SetCardInventory(next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native card inventory service rejected the update; PC and operand preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode.ToString(), message);
            throw new EventScriptException(message, exception);
        }
    }
}
