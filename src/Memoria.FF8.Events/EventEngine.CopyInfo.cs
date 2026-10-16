using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeCopiedPosition(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long FiberSequence, long Frame, int SourceNativeObjectId, int FixedX, int FixedY, int FixedZ, ushort Triangle);

public sealed partial class EventEngine
{
    private (ActorState Source, FieldFixedPosition Position) PrepareNativeCopyInfo(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(fiber.ObjectId) || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || (uint)fiber.Stack.Peek() >= program!.NativeObjects.Count)
        {
            throw new NotSupportedException("COPYINFO requires its original full DWORD native-object index and authenticated retained bodies.");
        }
        ActorState source = actors[program.NativeObjects[fiber.Stack.Peek()]];
        FieldFixedPosition raw = Scene.NativeActorObjectIds.Contains(source.ObjectId) ? ReadNativePlayerContactPosition(source) ?? throw new NotSupportedException("COPYINFO source has no preserved raw DWORD XYZ.") : throw new NotSupportedException("COPYINFO source is not an owned physical actor body.");
        if (source.NativeMovementBaseSpeed is null || source.MovementSpeed is < short.MinValue or > short.MaxValue || !float.IsFinite(source.Direction) || source.Direction != MathF.Truncate(source.Direction) || source.Direction is < byte.MinValue or > byte.MaxValue || source.WalkmeshTriangle is < short.MinValue or > ushort.MaxValue || new[] { raw.X, raw.Y, raw.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
        {
            throw new NotSupportedException("COPYINFO requires preserved source BYTE577, WORD506/510 and bounded raw XYZ.");
        }
        ActorState destination = actors[fiber.ObjectId];
        if (destination.NativeScriptMovement is not null || destination.NativeJump is not null || destination.NativeLadder is not null || destination.NativeLadderMode3 is not null || destination.MovementActorId is not null || destination.MovementTarget is not null || partyJoin?.Targets.Any(target => target.ObjectId == destination.ObjectId) == true || partySplit?.Targets.Any(target => target.ObjectId == destination.ObjectId) == true)
        {
            throw new NotSupportedException("COPYINFO replacement of an existing physical movement body requires its shared placement contract.");
        }
        return (source, raw);
    }

    private string? UnsupportedNativeCopyInfo(Fiber fiber)
    {
        try
        {
            _ = PrepareNativeCopyInfo(fiber);
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeCopyInfo(Fiber fiber)
    {
        (ActorState source, FieldFixedPosition raw) = PrepareNativeCopyInfo(fiber);
        ActorState destination = actors[fiber.ObjectId];
        int sourceId = fiber.Stack.Peek();
        ushort triangle = unchecked((ushort)source.WalkmeshTriangle);
        byte facing = checked((byte)source.Direction);
        short speed = checked((short)source.MovementSpeed);
        NativeCopiedPosition copied = new(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, Frame, sourceId, raw.X, raw.Y, raw.Z, triangle);
        int[] args = Pop(fiber, 1);
        //51F0C0 writes400/404/408,577,506,510 only; base512,575/576,
        //model, animation, physical targets, counters and results are retained.
        destination.X = raw.X / 4096f;
        destination.Y = raw.Y / 4096f;
        destination.Z = raw.Z / 4096f;
        destination.Direction = facing;
        destination.WalkmeshTriangle = triangle;
        destination.MovementSpeed = speed;
        destination.NativeJoinedPosition = null;
        destination.NativeSplitPosition = null;
        destination.NativeCopiedPosition = copied;
        if (Scene.NativePartyTraceVersion == 1 && IsNativePartyPlayerBody(destination))
        {
            Scene.NativePlayerPosition = raw;
        }
        ClearNativeDoorCurrentContacts();
        return args;
    }

    private void ValidateNativeCopiedPositions(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors.Where(actor => actor.NativeCopiedPosition is not null))
        {
            NativeCopiedPosition copied = actor.NativeCopiedPosition!;
            EventScript? script = program!.Objects[actor.ObjectId].Scripts.FirstOrDefault(script => script.Id == copied.ScriptId);
            if (NativeRuntimeProfile is not { IsVerified: true } || copied.Version != 1 || copied.FieldId != saved.FieldId || copied.ObjectId != actor.ObjectId || copied.Frame is < 1 || copied.Frame > saved.Frame || copied.FiberSequence < 1 || script is null || !script.ContainsOperation(copied.Operation) || program.Operations[copied.Operation].Opcode != Jsm.Opcode.COPYINFO || (uint)copied.SourceNativeObjectId >= program.NativeObjects.Count || !saved.Scene.NativeActorObjectIds.Contains(program.NativeObjects[copied.SourceNativeObjectId]) || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || saved.Scene.NativeActorLayoutVersion != 1 || actor.X != copied.FixedX / 4096f || actor.Y != copied.FixedY / 4096f || actor.Z != copied.FixedZ / 4096f || actor.WalkmeshTriangle != copied.Triangle || new[] { copied.FixedX, copied.FixedY, copied.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
            {
                throw new InvalidDataException("Saved COPYINFO placement lost its original instruction, retained raw source or exact copied coordinates.");
            }
        }
    }
}
