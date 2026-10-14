using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private ActorState? ResolveNativeActorInfoBody(Fiber fiber, bool party, out int bankAddress, out byte nativeActor)
    {
        bankAddress = -1;
        nativeActor = 0;
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native actor information requires authenticated field body allocation.");
        }
        ActorState actor;
        if (party)
        {
            if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || Scene.NativeActorLayoutVersion != 1 || Scene.NativeActorObjectIds.Count is < 1 or > 255 || Scene.NativeActorObjectIds.Any(id => (uint)id >= actors.Count) || Scene.NativeActorObjectIds.Distinct().Count() != Scene.NativeActorObjectIds.Count)
            {
                throw new NotSupportedException("PGETINFO requires its exact original DWORD operand and native compact body table.");
            }
            long address = 194L + fiber.Stack.Peek();
            if (address is < 0 or >= 248)
            {
                throw new NotSupportedException("PGETINFO indexed byte lies outside the retained native event bank.");
            }
            bankAddress = (int)address;
            int index;
            if (bankAddress is >= 194 and <= 196 && Scene.PartyBindingMemory is { Version: 1 } memory)
            {
                index = memory.At(bankAddress - 194);
            }
            else if (TryReadNativeMusicByte(bankAddress, out int music))
            {
                index = music;
            }
            else if (TryReadSoundVolumeByte(bankAddress, out int volume))
            {
                index = volume;
            }
            else
            {
                throw new NotSupportedException("PGETINFO requires an executed owner for its indexed native event-bank byte.");
            }
            if (variables.TryGetValue(bankAddress, out int alias) && alias != index)
            {
                throw new NotSupportedException("PGETINFO indexed byte differs from its canonical native owner.");
            }
            nativeActor = checked((byte)index);
            if (nativeActor >= Scene.NativeActorObjectIds.Count)
            {
                // Native51EEB0 still reads base+612*index. No retained body supplies
                // those bytes; preserve unknown results rather than select another actor.
                return null;
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
            _ = ResolveNativeActorInfoBody(fiber, operation.Opcode == Jsm.Opcode.PGETINFO, out _, out _);
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeActorInfo(Fiber fiber, bool party)
    {
        ActorState? actor = ResolveNativeActorInfoBody(fiber, party, out int bankAddress, out byte nativeActor);
        int rawOperand = party ? fiber.Stack.Peek() : 0;
        NativeScalarValue[] values;
        if (actor is null)
        {
            values = new[] { 0, 1, 2, 4, 5, 6 }.Select(register => new NativeScalarValue([], ActorInfoOrigin: new NativeActorInfoReadOrigin(1, FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, rawOperand, bankAddress, nativeActor, Scene.NativeActorObjectIds.Count, register))).ToArray();
        }
        else
        {
            FieldFixedPosition position = ReadNativeScriptPosition(actor)!;
            values = [position.X / 4096, position.Y / 4096, position.Z / 4096, (byte)actor.Direction, (ushort)actor.WalkmeshTriangle, (short)actor.MovementSpeed];
        }
        foreach (NativeScalarValue value in values)
        {
            value.Validate();
        }
        int[] args = party ? Pop(fiber, 1) : [];
        // Native51EE30/51EEB0 use signed division and preserve R3/R7.
        int[] registers = [0, 1, 2, 4, 5, 6];
        for (int index = 0; index < registers.Length; index++)
        {
            fiber.Results[registers[index]] = values[index];
        }
        return args;
    }
}
