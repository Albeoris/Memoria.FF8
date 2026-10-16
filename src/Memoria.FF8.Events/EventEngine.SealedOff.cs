using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSealedOff(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services.CharacterVitals is null || services.WorldControls is null || services.InputConfiguration is null || !services.CanStoreNativeEventOwners || !services.CanStoreEventMemory || fiber.Stack.Count == 0)
        {
            return "Native SEALEDOFF requires its consumed low BYTE and atomic saved options, character and event-memory owners";
        }
        try
        {
            _ = PrepareNativeSealedOff(checked((byte)fiber.Stack.PeekValue().ToWidth(1).RequireExact()));
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private (NativeCharacterVitals Vitals, NativeInputConfiguration Input, NativeEventMemory Memory) PrepareNativeSealedOff(byte mask)
    {
        NativeWorldControlState controls = services.WorldControls!;
        NativeCharacterVitals current = services.CharacterVitals!;
        NativeInputConfiguration input = services.InputConfiguration!;
        controls.Validate();
        current.Validate();
        input.Validate();
        byte previous = checked((byte)EventMemory.Read(243, 1).RequireExact());
        //51E9C0 always publishes the new mask, even with DWORD104 bit800 clear.
        byte restrictions = unchecked((byte)(previous & ~mask));
        NativeEventMemory memory = EventMemory.Write(243, 1, NativeScalarValue.Exact(restrictions));
        return (current.RebuildParty(), input with { Options = (input.Options & 0x00FFFFFFu) | ((uint)restrictions << 24) }, memory);
    }

    private int[] ExecuteNativeSealedOff(Fiber fiber, out NativeScalarValue[]? scalarArguments)
    {
        NativeScalarValue operand = fiber.Stack.PeekValue();
        byte mask = checked((byte)operand.ToWidth(1).RequireExact());
        var prepared = PrepareNativeSealedOff(mask);
        try
        {
            services.SetNativeEventOwners(services.WorldControls!, characterVitals: prepared.Vitals, eventMemory: prepared.Memory, inputConfiguration: prepared.Input);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native SEALEDOFF owner publication rejected; PC and operand preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, nameof(Jsm.Opcode.SEALEDOFF), message);
            throw new EventScriptException(message, exception);
        }
        PopNativeScalar(fiber);
        scalarArguments = operand.IsExact ? null : [operand];
        //Native return2 preserves R0 and runs the successor in this callback.
        return operand.IsExact ? [operand.RequireExact()] : [];
    }
}
