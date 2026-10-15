using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<byte, NativeSteamDiscMount>? ReadSteamDiscMount { get; set; }
    private readonly Dictionary<(long Sequence, int Operation), NativeSteamDiscMount> preparedDiscMounts = [];

    private string? UnsupportedNativeDiscJump(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.DISCJUMP)
        {
            return null;
        }
        try
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Initialization || !fiber.NativeResultFrameEntered && !fiber.NativeDoorDefaultFrame || SelectScheduledFiber(fiber.ObjectId) != fiber || NativeActorRequestSlot(fiber) is < 0 or > 7)
            {
                return "DISCJUMP requires its executed current physical-slot owner and active issuing slot";
            }
            if (fiber.Stack.Count < 5 || fiber.Stack.Capture().Take(5).Any(value => !value.IsExact) || services.DiscProcess is null || ReadSteamDiscMount is null)
            {
                return "DISCJUMP requires five exact original operands, the canonical disc owner and verified Steam marker provider";
            }
            services.DiscProcess.Validate();
            if (UnsupportedNativeDiscAliases() is string aliases)
            {
                return aliases;
            }
            if (UnsupportedWorldControlAliases() is string controls)
            {
                return controls;
            }
            NativeSteamDiscMount mount = ReadSteamDiscMount(services.DiscProcess.SavedDisc204);
            mount.Validate(services.DiscProcess.SavedDisc204);
            preparedDiscMounts[(fiber.Sequence, fiber.Position)] = mount;
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeDiscJump(Fiber fiber, int parameter)
    {
        NativeSteamDiscMount mount = preparedDiscMounts[(fiber.Sequence, fiber.Position - 1)];
        int[] operands = fiber.Stack.Take(5).Reverse().ToArray();
        NativeDiscJumpTransition request = new(1, NativeDiscJumpTransition.CurrentRule, FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, checked((byte)NativeActorRequestSlot(fiber)), unchecked((ushort)operands[4]), mount);
        request.Validate(services.DiscProcess!.SavedDisc204);
        FieldTransition transition = new(unchecked((ushort)operands[0]), unchecked((short)operands[1]), unchecked((short)operands[2]), unchecked((ushort)parameter), unchecked((byte)operands[4]), unchecked((short)operands[3]), request);
        //521B70 consumes low WORDs and returns1. The outgoing field never resumes
        //past this polling instruction before the mode6/kind9 destination entry.
        try
        {
            services.RequestField(transition);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native DISCJUMP transition service rejected the request; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "DISCJUMP", message);
            throw new EventScriptException(message, exception);
        }
        Pop(fiber, 5);
        preparedDiscMounts.Remove((fiber.Sequence, fiber.Position - 1));
        fiber.Position--;
        transitionRequested = true;
        return operands;
    }
}
