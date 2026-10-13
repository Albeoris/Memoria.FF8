using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Buffers.Binary;

namespace Memoria.FF8.Events;

public enum NativeWorldTransitionOrigin
{
    Script,
    Gateway
}

public sealed record NativeWorldTransitionRequest(int Version, int SourceFieldId, long SourceFrame, NativeWorldTransitionOrigin Origin, WorldTransition Transition, NativeWorldControlState ControlsAtDeparture, NativeWorldControlState ControlsAtIssue, NativeWorldControlState ControlsAtLoader, InfGateway? Gateway = null);
public sealed record NativeWorldJumpWait(int Version, int ObjectId, int ScriptId, int Operation, bool Suppressed, int[] Operands);

public sealed partial class EventEngine
{
    private readonly Dictionary<long, (NativeWorldTransitionRequest Request, Action Commit)> preparedWorldTransitions = [];

    /// <summary>Prepares resources and an atomic one-shot packet/control commit without changing the field VM. Null refuses the original instruction/proposal.</summary>
    public Func<NativeWorldTransitionRequest, Action?>? PrepareWorldTransition { get; set; }

    private NativeWorldTransitionRequest CreateWorldTransitionRequest(WorldTransition transition, NativeWorldTransitionOrigin origin, InfGateway? gateway = null)
    {
        if (UnsupportedWorldControlAliases() is string aliases)
        {
            throw new NotSupportedException(aliases);
        }
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("World entry requires canonical native control flags with proved provenance; original field state preserved.");
        controls.Validate();
        NativeWorldControlState loader = controls.WithWorldEntryMenuFlag();
        InfGateway? copiedGateway = gateway is null ? null : gateway with { NativeRecord = gateway.NativeRecord?.ToArray() };
        return new NativeWorldTransitionRequest(1, FieldId, Frame, origin, transition, controls, origin == NativeWorldTransitionOrigin.Script ? loader : controls, loader, copiedGateway);
    }

    private Action PrepareWorldTransitionCommit(NativeWorldTransitionRequest request)
    {
        if (PrepareWorldTransition is null)
        {
            throw new NotSupportedException("World entry requires its original resource/packet preparation service; original field state preserved.");
        }
        return PrepareWorldTransition(request) ?? throw new NotSupportedException("World entry resources, mode or original packet are outside the certified backend; original field state preserved.");
    }

    private string? UnsupportedWorldJump(Fiber fiber)
    {
        if (fiber.Stack.Count < 3)
        {
            return "WORLDMAPJUMP requires its three original DWORD operands";
        }
        if (Scene.MapJumpDisabled)
        {
            return null;
        }
        int[] operands = fiber.Stack.Take(3).Reverse().ToArray();
        try
        {
            NativeWorldTransitionRequest request = CreateWorldTransitionRequest(new WorldTransition((short)operands[0], (short)operands[1], (short)operands[2]), NativeWorldTransitionOrigin.Script);
            Action commit = PrepareWorldTransitionCommit(request);
            preparedWorldTransitions[fiber.Sequence] = (request, commit);
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or IOException or ArgumentException or ArithmeticException)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteWorldJump(Fiber fiber)
    {
        bool suppressed = Scene.MapJumpDisabled;
        if (!suppressed)
        {
            (NativeWorldTransitionRequest request, Action commit) = preparedWorldTransitions[fiber.Sequence];
            preparedWorldTransitions.Remove(fiber.Sequence);
            try
            {
                commit();
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or IOException or ArgumentException or ArithmeticException or InvalidOperationException)
            {
                fiber.Position--;
                string message = $"Field{FieldId} WORLDMAPJUMP{fiber.Position}: prepared atomic commit rejected the request; original PC and operands preserved.";
                LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "WORLDMAPJUMP", message);
                throw new EventScriptException(message, exception);
            }
            Scene.WorldTransitionRequest = request;
            transitionRequested = true;
        }
        int[] operands = Pop(fiber, 3);
        fiber.Position--;
        Scene.WorldJumpWaits.Add(fiber.Sequence, new NativeWorldJumpWait(1, fiber.ObjectId, fiber.Script.Id, fiber.Position, suppressed, operands));
        fiber.WaitReason = WaitReason.WorldJump;
        return operands;
    }

    private static WorldTransition ReadInfWorldPacket(InfGateway gateway)
    {
        if (gateway.NativeIndex is < 0 or > 11 || gateway.NativeRecord is not { Length: 32 } bytes)
        {
            throw new NotSupportedException("World INF gateway requires its original index and complete32-byte record; original field state preserved.");
        }
        int field = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(18));
        if (field >= 72 || gateway.Destination.FieldId != field || gateway.Start.X != BinaryPrimitives.ReadInt16LittleEndian(bytes) || gateway.Start.Y != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)) || gateway.Start.Z != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)) || gateway.End.X != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) || gateway.End.Y != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(8)) || gateway.End.Z != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(10)) || gateway.Destination.X != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(12)) || gateway.Destination.Y != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(14)) || gateway.Destination.Triangle != BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(16)) || gateway.Destination.Direction != bytes[28])
        {
            throw new NotSupportedException("World INF gateway metadata differs from its original packet words, byte or signed geometry; original field state preserved.");
        }
        return new WorldTransition((short)field, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(16)), bytes[28]);
    }

    private void ValidateWorldTransitionCheckpoint(Checkpoint saved, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        foreach ((long sequence, NativeWorldJumpWait wait) in saved.Scene.WorldJumpWaits)
        {
            if (wait is null || wait.Version != 1 || wait.Operands is not { Length: 3 } || !bySequence.TryGetValue(sequence, out Fiber? owner) || owner.Complete || owner.ObjectId != wait.ObjectId || owner.Script.Id != wait.ScriptId || owner.Position != wait.Operation || owner.WaitReason != WaitReason.WorldJump || (uint)wait.Operation >= program!.Operations.Count || program.Operations[wait.Operation].Opcode != Jsm.Opcode.WORLDMAPJUMP)
            {
                throw new InvalidDataException("Saved native world-jump wait does not preserve its original owner, issuing PC and three consumed operands.");
            }
        }
        if (bySequence.Values.Any(fiber => fiber.WaitReason == WaitReason.WorldJump && !saved.Scene.WorldJumpWaits.ContainsKey(fiber.Sequence)))
        {
            throw new InvalidDataException("Saved native world-jump wait has no first-entry state.");
        }
        NativeWorldJumpWait[] issued = saved.Scene.WorldJumpWaits.Values.Where(wait => !wait.Suppressed).ToArray();
        if (saved.Scene.WorldTransitionRequest is not NativeWorldTransitionRequest request)
        {
            if (issued.Length != 0)
            {
                throw new InvalidDataException("Saved issued world-jump wait has no native transition request.");
            }
            return;
        }
        if (request.Version != 1 || request.SourceFieldId != saved.FieldId || request.SourceFrame != saved.Frame || !Enum.IsDefined(request.Origin) || !saved.TransitionRequested || request.Transition is null || request.ControlsAtDeparture is null || request.ControlsAtIssue is null || request.ControlsAtLoader is null || saved.WorldControls is null || saved.Variables.Keys.Any(address => address is >= 102 and <= 105 or >= 206 and <= 209))
        {
            throw new InvalidDataException("Saved world request lacks its original field/frame, transition or canonical control provenance.");
        }
        request.ControlsAtDeparture.Validate();
        request.ControlsAtIssue.Validate();
        request.ControlsAtLoader.Validate();
        NativeWorldControlState expectedLoader = request.ControlsAtDeparture.WithWorldEntryMenuFlag();
        NativeWorldControlState expectedIssue = request.Origin == NativeWorldTransitionOrigin.Script ? expectedLoader : request.ControlsAtDeparture;
        if (request.ControlsAtIssue != expectedIssue || request.ControlsAtLoader != expectedLoader || saved.WorldControls != expectedLoader)
        {
            throw new InvalidDataException("Saved world request changed its native issue/loader flag stages or canonical controls.");
        }
        if (request.Origin == NativeWorldTransitionOrigin.Script)
        {
            if (request.Gateway is not null || issued.Length != 1 || request.Transition != new WorldTransition((short)issued[0].Operands[0], (short)issued[0].Operands[1], (short)issued[0].Operands[2]))
            {
                throw new InvalidDataException("Saved scripted world request differs from its original three low-WORD packet operands.");
            }
        }
        else
        {
            if (issued.Length != 0 || request.Gateway is not InfGateway gateway)
            {
                throw new InvalidDataException("Saved INF world request lacks its original gateway metadata.");
            }
            try
            {
                if (ReadInfWorldPacket(gateway) != request.Transition || !InfGateways.Any(original => original.NativeIndex == gateway.NativeIndex && original.Start == gateway.Start && original.End == gateway.End && original.Destination == gateway.Destination && original.NativeRecord is not null && original.NativeRecord.SequenceEqual(gateway.NativeRecord!)))
                {
                    throw new InvalidDataException("Saved INF world request differs from its loaded original gateway record.");
                }
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidDataException("Saved INF world packet is invalid.", exception);
            }
        }
    }
}
