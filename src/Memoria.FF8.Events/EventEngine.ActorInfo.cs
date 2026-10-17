using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<NativeActorInfoArenaDefinition>? ReadNativeActorInfoArena { get; set; }

    private ActorState? ResolveNativeIndexedPartyBody(int operand, out int bankAddress, out byte nativeActor)
    {
        bankAddress = -1;
        nativeActor = 0;
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeActorObjectIds.Count is < 1 or > 255 || Scene.NativeActorObjectIds.Any(id => (uint)id >= actors.Count) || Scene.NativeActorObjectIds.Distinct().Count() != Scene.NativeActorObjectIds.Count)
        {
            throw new NotSupportedException("Native indexed party lookup requires its authenticated compact body table.");
        }
        long address = 194L + operand;
        if (address is < 0 or >= 248)
        {
            throw new NotSupportedException("Native indexed party byte lies outside the retained event bank.");
        }
        bankAddress = (int)address;
        NativeEventMemoryOwnership.Validate(EventMemory, MemoryRegions());
        _ = ResolveMemoryOwner(bankAddress, 1);
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
            throw new NotSupportedException("Native indexed party lookup requires an executed owner for its event-bank byte.");
        }
        nativeActor = checked((byte)index);
        return nativeActor < Scene.NativeActorObjectIds.Count ? actors[Scene.NativeActorObjectIds[nativeActor]] : null;
    }

    private ActorState? ResolveNativeActorInfoBody(Fiber fiber, bool party, out int bankAddress, out byte nativeActor)
    {
        bankAddress = -1;
        nativeActor = 0;
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native actor information requires authenticated field body allocation.");
        }
        ActorState? actor;
        if (party)
        {
            if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
            {
                throw new NotSupportedException("PGETINFO requires its exact original DWORD operand.");
            }
            actor = ResolveNativeIndexedPartyBody(fiber.Stack.Peek(), out bankAddress, out nativeActor);
            if (actor is null)
            {
                // Native51EEB0 still reads base+612*index outside the retained table.
                return null;
            }
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
            ActorState? actor = ResolveNativeActorInfoBody(fiber, operation.Opcode == Jsm.Opcode.PGETINFO, out _, out byte nativeActor);
            if (actor is null)
            {
                _ = TryReadNativeArenaActorInfo(nativeActor, out _);
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
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
            if (!TryReadNativeArenaActorInfo(nativeActor, out values))
            {
                values = new[] { 0, 1, 2, 4, 5, 6 }.Select(register => new NativeScalarValue([], ActorInfoOrigin: new NativeActorInfoReadOrigin(1, FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, rawOperand, bankAddress, nativeActor, Scene.NativeActorObjectIds.Count, register))).ToArray();
            }
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

    private bool TryReadNativeArenaActorInfo(byte nativeActor, out NativeScalarValue[] values)
    {
        values = [];
        if (program is null || ReadNativeActorInfoArena?.Invoke() is not { } arena)
        {
            return false;
        }
        arena.Validate();
        if (arena.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || program.NativeObjects.Count > arena.MaximumDeclaredRecords)
        {
            return false;
        }
        int prefix = 0;
        foreach (int objectId in program.NativeObjects.Skip(program.NativeActorCount))
        {
            EventObject obj = program.Objects[objectId];
            if (obj.IsDoor)
            {
                prefix = checked(prefix + 416);
            }
            else if (obj.IsArea)
            {
                prefix = checked(prefix + 396);
            }
            else if (obj.BackgroundModuleIndex is not null)
            {
                prefix = checked(prefix + 436);
            }
            else
            {
                return false;
            }
        }
        int[] offsets = [400, 404, 408, 577, 506, 510];
        int[] widths = [4, 4, 4, 1, 2, 2];
        int[] raw = new int[offsets.Length];
        for (int index = 0; index < offsets.Length; index++)
        {
            if (!arena.TryReadZero(prefix, nativeActor, offsets[index], widths[index], out raw[index]))
            {
                return false;
            }
        }
        values = [raw[0] / 4096, raw[1] / 4096, raw[2] / 4096, (byte)raw[3], (ushort)raw[4], (short)raw[5]];
        return true;
    }
}
