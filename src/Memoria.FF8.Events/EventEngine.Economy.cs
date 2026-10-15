using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeEconomy? Economy => services.Economy;

    private string? UnsupportedEconomyAliases()
    {
        return EventMemory.Overlaps(72, 8) ? "Native gil event-bank words overlap a separate VM memory alias; migrate this save explicitly" : null;
    }

    private string? UnsupportedNativeEconomy(Fiber fiber, JsmOperation operation)
    {
        bool command = operation.Opcode is Jsm.Opcode.ADDITEM or Jsm.Opcode.ADDGIL or Jsm.Opcode.ADDPASTGIL;
        int width = NativeMemoryWidth(operation.Opcode);
        bool bank = width != 0 && operation.Parameter < 80 && (long)operation.Parameter + width > 72;
        if (!command && !bank)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || services.Economy is not NativeEconomy economy || !services.CanStoreEconomy)
        {
            return "Native item/gil effects require authentic economy initialization and a persistent current native service";
        }
        economy.Validate();
        if (UnsupportedEconomyAliases() is string aliases)
        {
            return aliases;
        }
        if (bank && (operation.Parameter < 72 || (long)operation.Parameter + width > 80))
        {
            return "Native gil projection cannot read or write unknown surrounding event-bank bytes";
        }
        int count = operation.Opcode == Jsm.Opcode.ADDITEM ? 2 : command || operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L ? 1 : 0;
        if (fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact))
        {
            return "Native economy update requires its exact original DWORD operands before mutation";
        }
        return null;
    }

    private int[] ExecuteNativeEconomy(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeEconomy economy = services.Economy!;
        int[] args = fiber.Stack.Take(opcode == Jsm.Opcode.ADDITEM ? 2 : 1).Reverse().ToArray();
        NativeEconomy next = opcode == Jsm.Opcode.ADDITEM ? economy.AddItem(args[1], args[0], out _) : economy.AddGil(args[0], opcode == Jsm.Opcode.ADDPASTGIL);
        PublishNativeEconomy(fiber, next);
        return Pop(fiber, args.Length);
    }

    private bool TryReadNativeEconomyMemory(JsmOperation operation, out int value)
    {
        value = 0;
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.Economy is null || width == 0 || operation.Parameter < 72 || (long)operation.Parameter + width > 80)
        {
            return false;
        }
        value = services.Economy.ReadEventBank(operation.Parameter, width);
        return true;
    }

    private bool TryWriteNativeEconomyMemory(Fiber fiber, JsmOperation operation, out int[] operands)
    {
        operands = [];
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.Economy is null || operation.Opcode is not (Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L) || operation.Parameter < 72 || (long)operation.Parameter + width > 80)
        {
            return false;
        }
        PublishNativeEconomy(fiber, services.Economy.WriteEventBank(operation.Parameter, width, fiber.Stack.Peek()));
        operands = Pop(fiber, 1);
        return true;
    }

    private void PublishNativeEconomy(Fiber fiber, NativeEconomy economy)
    {
        try
        {
            services.SetEconomy(economy);
        }
        catch (NotSupportedException exception)
        {
            throw NativeOwnerPublicationFault(fiber, exception);
        }
    }

    private NativeEconomy? PrepareNativeGilBeforeVm(NativeEconomy? prospective)
    {
        if (prospective is not NativeEconomy economy)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || !services.CanStoreEconomy || UnsupportedEconomyAliases() is not null)
        {
            throw new NotSupportedException("Native field gil copy requires current canonical event-bank ownership before the callback.");
        }
        // Native4767B0 ->529FF0 copies after salary sampling, before actor VM dispatch.
        // The same callback runs during field movies; world callbacks do not call529FF0.
        return economy.CopyGilToEventBank();
    }
}
