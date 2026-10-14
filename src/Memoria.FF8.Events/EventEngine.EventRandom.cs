using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeEventRandomState? EventRandom => services.EventRandom;

    private string? UnsupportedEventRandomAliases()
    {
        return variables.Keys.Any(address => address < 182 && (long)address + 4 > 116) || services.NativeVariableValues?.Any(pair => pair.Key < 182 && (long)pair.Key + pair.Value.Width > 116) == true ? "Native packed draw-point bank overlaps a separate memory alias" : null;
    }

    private string? UnsupportedNativeEventRandom(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        bool bank = width != 0 && operation.Parameter < 182 && (long)operation.Parameter + width > 116;
        if (operation.Opcode != Jsm.Opcode.RND && !bank)
        {
            return null;
        }
        if (services.EventRandom is not NativeEventRandomState random || !services.CanStoreEventRandom)
        {
            return "Native RND and depletion memory require the actual current native process owner before mutation";
        }
        random.Validate();
        if (bank && (random.DrawPoints is null || operation.Parameter < 116 || (long)operation.Parameter + width > 182))
        {
            return "Native draw-point memory requires its executed bank initialization and cannot consume unknown surrounding bytes";
        }
        if (UnsupportedEventRandomAliases() is string alias)
        {
            return alias;
        }
        if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact))
        {
            return "Native packed draw-point store requires its exact consumed scalar before mutation";
        }
        return null;
    }

    private void ExecuteNativeRandom(Fiber fiber)
    {
        NativeEventRandomState next = services.EventRandom!.Draw(out int result);
        PublishNativeEventRandom(fiber, next);
        fiber.Results[0] = result & 255;
    }

    private bool TryReadNativeDrawPointMemory(JsmOperation operation, out int value)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        value = 0;
        if (services.EventRandom?.DrawPoints is not NativeDrawPointMemory bank || width == 0 || operation.Parameter < 116 || (long)operation.Parameter + width > 182)
        {
            return false;
        }
        value = bank.Read(operation.Parameter, width);
        return true;
    }

    private bool TryWriteNativeDrawPointMemory(Fiber fiber, JsmOperation operation, out int[] args)
    {
        args = [];
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.EventRandom is not { DrawPoints: NativeDrawPointMemory bank } random || operation.Opcode is not (Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L) || operation.Parameter < 116 || (long)operation.Parameter + width > 182)
        {
            return false;
        }
        PublishNativeEventRandom(fiber, random with { DrawPoints = bank.Write(operation.Parameter, width, fiber.Stack.Peek()) });
        args = Pop(fiber, 1);
        return true;
    }

    private void PublishNativeEventRandom(Fiber fiber, NativeEventRandomState random)
    {
        try
        {
            services.SetEventRandom(random);
        }
        catch (NotSupportedException exception)
        {
            throw NativeOwnerPublicationFault(fiber, exception);
        }
    }

    private NativeEventRandomState? PrepareNativeEventRandomBeforeVm()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return null;
        }
        NativeEventRandomState random = services.EventRandom ?? throw new NotSupportedException("Native field callback requires its current event random process before mutation.");
        random.Validate();
        if (!services.CanStoreEventRandom || !services.CanStoreNativeEventOwners || services.WorldControls is null || UnsupportedEventRandomAliases() is not null)
        {
            throw new NotSupportedException("Native field random callback requires canonical memory and atomic current-owner publication.");
        }
        // NativeBSS produces CF10=0 only at cold process creation; do not substitute UCON or salary flags.
        if ((random.FieldCallbackFlagsCF10 & 1) != 0)
        {
            throw new NotSupportedException("Native CF10 bit1 suspends the entire529FF0 VM callback; this unproduced process branch remains unsupported.");
        }
        if (Scene.NativeSalaryMovementUnits is not (0 or 3 or 5))
        {
            throw new NotSupportedException("Native draw-point aging requires the actual previous-body movement scalar.");
        }
        return random.AdvanceMovement(Scene.NativeSalaryMovementUnits.Value);
    }
}
