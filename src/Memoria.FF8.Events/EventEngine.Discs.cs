using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeDisc(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.DISC)
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || !services.CanStoreDiscProcess || services.DiscProcess is null || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
            {
                return "DISC requires its original exact operand and the authentic NewGame disc owner.";
            }
            services.DiscProcess.Validate();
        }
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.DiscProcess is not null && (operation.Opcode == Jsm.Opcode.DISC || width > 0 && operation.Parameter <= 204 && (long)operation.Parameter + width > 204) && (UnsupportedNativeDiscAliases() is not null))
        {
            return "Native disc access requires unaliased ownership of event BYTE204 before execution.";
        }
        if (services.DiscProcess is not null && width > 0 && operation.Parameter <= 204 && (long)operation.Parameter + width > 204)
        {
            return width == 1 && operation.Parameter == 204 && operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B ? null : "Native disc memory requires the exact owned BYTE204 read; other overlapping access is unsupported.";
        }
        return null;
    }

    private string? UnsupportedNativeDiscAliases()
    {
        return variables.Keys.Any(address => address <= 204 && (long)address + 4 > 204) || services.NativeVariableValues?.Any(pair => pair.Key <= 204 && (long)pair.Key + pair.Value.Width > 204) == true ? "Native disc access overlaps a second event BYTE204 owner." : null;
    }

    private int[] AssignNativeDisc(Fiber fiber)
    {
        byte disc = unchecked((byte)fiber.Stack.Peek());
        try
        {
            services.SetDiscProcess(services.DiscProcess!.Assign(disc));
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native disc service rejected the assignment; PC and operand preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, Jsm.Opcode.DISC.ToString(), message);
            throw new EventScriptException(message, exception);
        }
        return Pop(fiber, 1);
    }
}
