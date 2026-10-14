using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeHold(Fiber fiber)
    {
        NativeScalarValue[] operands = fiber.Stack.Capture();
        if (operands.Length < 3 || !operands[2].IsExact || !operands[1].IsExact)
        {
            return "Native HOLD needs three stack slots but reads only its first two exact DWORD operands; its unused third is opaque";
        }
        if (services.CharacterVitals is not NativeCharacterVitals state)
        {
            return "Native HOLD requires its actual saved character identities and membership WORDs";
        }
        try
        {
            _ = state.WithHoldFlags(operands[2].RequireExact(), operands[1].RequireExact() != 0);
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeHold(Fiber fiber, out NativeScalarValue[]? scalarArguments)
    {
        NativeScalarValue[] operands = fiber.Stack.Capture().Take(3).Reverse().ToArray();
        NativeCharacterVitals next = services.CharacterVitals!.WithHoldFlags(operands[0].RequireExact(), operands[1].RequireExact() != 0);
        CommitSavedCharacters(fiber, next, Jsm.Opcode.HOLD);
        string[] expressions = new string[3];
        for (int index = 2; index >= 0; index--)
        {
            fiber.Stack.PopValue();
            expressions[index] = fiber.StackExpressions.Pop();
        }
        fiber.LastExpressions = expressions;
        scalarArguments = operands.All(value => value.IsExact) ? null : operands;
        return scalarArguments is null ? operands.Select(value => value.RequireExact()).ToArray() : [];
    }
}
