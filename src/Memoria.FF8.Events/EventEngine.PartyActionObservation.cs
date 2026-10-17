using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativePartyActionRequest(int FieldId, string ProgramHash, long Frame, int CallerObjectId, int ScriptId, int Operation, long FiberSequence, int PartySlot, int RawPriority, int LocalSelector, int ReceiverObjectId, int ReceiverCharacterId, int PhysicalBinding, int DeclaredScriptCount, int PhysicalPointerIndex, int PhysicalCodeObjectId, int PhysicalScriptId, int PhysicalOperation);

public sealed partial class EventEngine
{
    /// <summary>Observes a valid free native party request before its PC, operands or target slot change. Observer exceptions propagate without becoming native faults.</summary>
    public event Action<NativePartyActionRequest>? BeforeNativePartyActionRequest;

    private void ObserveNativePartyActionRequest(Fiber fiber, JsmOperation operation)
    {
        if (BeforeNativePartyActionRequest is not { } observer || NativeRuntimeProfile is not { IsVerified: true } || operation.Opcode is not (Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW))
        {
            return;
        }
        int priority = fiber.Stack.Skip(1).First();
        ActorState? receiver = ResolveJoinPartyActor(operation.Parameter);
        if (receiver is null || RequestPriorityOccupied(receiver.ObjectId, priority))
        {
            return;
        }
        int selector = fiber.Stack.Peek();
        NativeScriptPointer pointer = ReadNativePartyRequestPointer(receiver.ObjectId, selector, Scene);
        observer(new NativePartyActionRequest(FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position, fiber.Sequence, operation.Parameter, priority, selector, receiver.ObjectId, receiver.CharacterId, Scene.PartyBindingMemory!.At(operation.Parameter), program!.Objects[receiver.ObjectId].Scripts.Count, pointer.Index, pointer.ObjectId, pointer.Script.Id, pointer.Script.Start));
    }
}
