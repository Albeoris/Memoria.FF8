using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeLastIn(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || !HasCanonicalPartySlots(Scene) || services.CharacterVitals is null || services.WorldControls is null || services.InputConfiguration is null || !services.CanStoreNativeEventOwners || !services.CanStoreEventMemory || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native LASTIN requires its exact DWORD operand and atomic saved flags, options, reserve-party and event-memory owners";
        }
        if (UnsupportedJunctionBackupAliases() is string alias)
        {
            return alias;
        }
        try
        {
            _ = PrepareNativeLastIn(fiber.Stack.Peek());
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private (NativeWorldControlState Controls, NativeCharacterVitals Vitals, NativeInputConfiguration Input, NativeEventMemory Memory) PrepareNativeLastIn(int argument)
    {
        NativeWorldControlState controls = services.WorldControls!;
        NativeCharacterVitals current = services.CharacterVitals!;
        NativeInputConfiguration input = services.InputConfiguration!;
        controls.Validate();
        input.Validate();
        //51E910 and52D050 share the same52CBE0 complement construction.
        NativeCharacterVitals reserved = PrepareNativeReserveParty(current);
        NativeEventMemory memory = argument == 0 ? EventMemory : EventMemory.Write(243, 1, NativeScalarValue.Exact(255));
        NativeScalarValue sealedByte = memory.Read(243, 1);
        if (!sealedByte.IsExact)
        {
            throw new NotSupportedException("Native LASTIN zero-operand branch requires the retained exact source BYTE243.");
        }
        byte restrictions = unchecked((byte)sealedByte.RequireExact());
        NativeCharacterVitals next = reserved.RebuildParty();
        return (controls with { EventFlags104 = controls.EventFlags104 | 0x800u }, next, input with { Options = (input.Options & 0x00FFFFFFu) | ((uint)restrictions << 24) }, memory);
    }

    private int[] ExecuteNativeLastIn(Fiber fiber)
    {
        var prepared = PrepareNativeLastIn(fiber.Stack.Peek());
        try
        {
            services.SetNativeEventOwners(prepared.Controls, characterVitals: prepared.Vitals, eventMemory: prepared.Memory, inputConfiguration: prepared.Input);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native LASTIN owner publication rejected; PC and operand preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, nameof(Jsm.Opcode.LASTIN), message);
            throw new EventScriptException(message, exception);
        }
        //Native return2 continues this callback, without refreshing retained actor bindings.
        return Pop(fiber, 1);
    }
}
