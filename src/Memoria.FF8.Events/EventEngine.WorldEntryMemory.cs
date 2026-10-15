using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedWorldEntryMemory(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        bool clear = operation.Opcode == Jsm.Opcode.CLEAR;
        if (!clear && !NativeWorldEntryMemory.Overlaps(operation.Parameter, width) || NativeRuntimeProfile is not { IsVerified: true } && services.WorldEntryMemory is null)
        {
            return null;
        }
        bool writing = operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        if (clear)
        {
            return services.CanStoreWorldEntryMemory ? null : "Native CLEAR requires an explicit world-entry byte publication service";
        }
        if (services.WorldEntryMemory is not { } memory)
        {
            return "Native world-entry memory requires its executed NewGame/CLEAR selected byte owner";
        }
        memory.Validate();
        if (operation.Parameter < NativeWorldEntryMemory.FirstAddress || (long)operation.Parameter + width > NativeWorldEntryMemory.FirstAddress + NativeWorldEntryMemory.ByteCount)
        {
            return "Native world-entry access crosses its represented bytes256..267 into unknown neighboring memory";
        }
        if (writing && (!services.CanStoreWorldEntryMemory || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact))
        {
            return "Native world-entry store requires its original exact DWORD operand and explicit byte publication service";
        }
        if (services.NativeVariableValues?.Any(pair => NativeWorldEntryMemory.Overlaps(pair.Key, pair.Value.Width)) == true)
        {
            return "Native world-entry selected byte owner overlaps uncertain scalar memory";
        }
        return null;
    }

    private bool TryReadWorldEntryMemory(JsmOperation operation, out int value)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.WorldEntryMemory is { } memory && NativeWorldEntryMemory.Overlaps(operation.Parameter, width))
        {
            int raw = memory.ReadBytes(operation.Parameter, width);
            value = operation.Opcode == Jsm.Opcode.PSHSM_B ? unchecked((sbyte)raw) : operation.Opcode == Jsm.Opcode.PSHSM_W ? unchecked((short)raw) : raw;
            return true;
        }
        value = 0;
        return false;
    }

    private bool TryWriteWorldEntryMemory(Fiber fiber, JsmOperation operation, out int[] operands)
    {
        operands = [];
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.WorldEntryMemory is not { } memory || !NativeWorldEntryMemory.Overlaps(operation.Parameter, width))
        {
            return false;
        }
        int value = fiber.Stack.Peek();
        NativeWorldEntryMemory next = memory.WriteBytes(operation.Parameter, width, value);
        services.SetWorldEntryMemory(next);
        next.ProjectVariables(variables);
        operands = Pop(fiber, 1);
        return true;
    }

    private void ClearWorldEntryMemory()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } && services.WorldEntryMemory is null)
        {
            return;
        }
        NativeWorldEntryMemory cleared = services.WorldEntryMemory?.ClearFieldBytes() ?? NativeWorldEntryMemory.CreateCleared();
        services.SetWorldEntryMemory(cleared);
        cleared.ProjectVariables(variables);
    }

    private void ValidateWorldEntryMemoryCheckpoint(Checkpoint saved)
    {
        if (saved.WorldEntryMemory is null && (NativeRuntimeProfile is { IsVerified: true } || services.WorldEntryMemory is not null) || saved.WorldEntryMemory is not null && !services.CanStoreWorldEntryMemory)
        {
            throw new InvalidDataException("Saved native world-entry bytes require their explicit current producer and publication service.");
        }
        saved.WorldEntryMemory?.ValidateVariables(saved.Variables, saved.NativeVariableValues);
    }
}
