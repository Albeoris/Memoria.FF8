using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private ActorState ResolveNativeActorInfoBody(Fiber fiber, bool party)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native actor information requires authenticated field body allocation.");
        }
        ActorState actor;
        if (party)
        {
            if (fiber.Stack.Count == 0 || (uint)fiber.Stack.Peek() >= 3 || Scene.NativeActorLayoutVersion != 1 || Scene.PartyBindingMemory is not { Version: 1 } memory)
            {
                throw new NotSupportedException("PGETINFO requires its original saved party slot and native body table.");
            }
            int slot = fiber.Stack.Peek();
            int nativeActor = memory.At(slot);
            if ((uint)nativeActor >= Scene.NativeActorObjectIds.Count || variables.TryGetValue(194 + slot, out int alias) && alias != nativeActor)
            {
                throw new NotSupportedException("PGETINFO cannot substitute a runtime follower for the saved party body.");
            }
            actor = actors[Scene.NativeActorObjectIds[nativeActor]];
        }
        else
        {
            actor = actors[fiber.ObjectId];
        }
        if (ReadNativeScriptPosition(actor) is null || actor.NativeMovementBaseSpeed is null || actor.MovementSpeed is < short.MinValue or > short.MaxValue || !float.IsFinite(actor.Direction) || actor.Direction != MathF.Truncate(actor.Direction) || actor.Direction is < byte.MinValue or > byte.MaxValue || actor.WalkmeshTriangle is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new NotSupportedException("Native actor information requires its exact fixed position, BYTE facing and current WORD triangle/speed.");
        }
        return actor;
    }

    private string? UnsupportedNativeActorInfo(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.GETINFO or Jsm.Opcode.PGETINFO))
        {
            return null;
        }
        try
        {
            _ = ResolveNativeActorInfoBody(fiber, operation.Opcode == Jsm.Opcode.PGETINFO);
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeActorInfo(Fiber fiber, bool party)
    {
        ActorState actor = ResolveNativeActorInfoBody(fiber, party);
        FieldFixedPosition position = ReadNativeScriptPosition(actor)!;
        int[] args = party ? Pop(fiber, 1) : [];
        // Native51EE30/51EEB0 use signed division, preserve R3/R7 and read
        // the saved B194 body for PGETINFO, independent of FOLLOWOFF.
        fiber.Results[0] = position.X / 4096;
        fiber.Results[1] = position.Y / 4096;
        fiber.Results[2] = position.Z / 4096;
        fiber.Results[4] = (byte)actor.Direction;
        fiber.Results[5] = (ushort)actor.WalkmeshTriangle;
        fiber.Results[6] = (short)actor.MovementSpeed;
        return args;
    }
}
