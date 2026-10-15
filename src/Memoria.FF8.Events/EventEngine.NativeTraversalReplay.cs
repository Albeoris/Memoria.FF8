using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public bool HasPendingSemanticButtonConsumer
    {
        get
        {
            if (program is null)
            {
                return false;
            }
            foreach (Fiber fiber in fibers.Where(fiber => !fiber.Complete && fiber.WaitReason != WaitReason.NativeHalt))
            {
                Stack<int> pending = new();
                HashSet<int> visited = [];
                pending.Push(fiber.Position);
                while (pending.TryPop(out int position))
                {
                    if (position < fiber.Script.Start || position >= fiber.Script.End || !visited.Add(position))
                    {
                        continue;
                    }
                    JsmOperation operation = program.Operations[position];
                    if (operation.Opcode is Jsm.Opcode.KEYON or Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2)
                    {
                        return true;
                    }
                    if (operation.Opcode is Jsm.Opcode.RET or Jsm.Opcode.HALT)
                    {
                        continue;
                    }
                    if (operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
                    {
                        pending.Push(position + operation.Parameter);
                    }
                    if (operation.Opcode != Jsm.Opcode.JMP)
                    {
                        pending.Push(position + 1);
                    }
                }
            }
            return false;
        }
    }

    /// <summary>Describes the current controlled native owner without issuing input; Forward is its available target endpoint.</summary>
    public ReplayNativeTraversal? PendingSemanticTraversal
    {
        get
        {
            ActorState? actor = actors.FirstOrDefault(IsNativePartyPlayerBody);
            if (!IsUserControlEnabled || actor is null || actor.NativeMovementCompletion542 == 2)
            {
                return null;
            }
            if (actor.NativeLadderMode3 is { Active: true } mode3)
            {
                return CreateNativeTraversal(actor, mode3.ScriptId, mode3.Operation, 3, true);
            }
            if (actor.NativeLadder is { Active: true } mode4)
            {
                return mode4.ReplayCompletion ?? CreateNativeTraversal(actor, mode4.ScriptId, mode4.Operation, 4, true);
            }
            return null;
        }
    }

    /// <summary>Resolves a legacy physical setter against an actual currently controlled native ladder, without advancing it.</summary>
    public ReplayNativeTraversal? ResolvePendingSemanticTraversal(int physicalButtons)
    {
        if (physicalButtons is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalButtons));
        }
        ActorState? actor = actors.FirstOrDefault(IsNativePartyPlayerBody);
        if (!IsUserControlEnabled || actor is null || actor.NativeMovementCompletion542 == 2)
        {
            return null;
        }
        ushort held = (services.InputConfiguration ?? throw new NotSupportedException("Native traversal requires its current input configuration.")).Map((ushort)physicalButtons);
        if (actor.NativeLadderMode3 is { Active: true } mode3)
        {
            bool reverse = (held & (mode3.Up ? 0xC000 : 0x3000)) != 0;
            bool forward = (held & (mode3.Up ? 0x1000 : 0x4000)) != 0;
            if (forward && reverse)
            {
                throw new NotSupportedException("Opposed native mode3 inputs do not identify one semantic endpoint.");
            }
            return forward || reverse ? CreateNativeTraversal(actor, mode3.ScriptId, mode3.Operation, 3, forward, true) : null;
        }
        if (actor.NativeLadder is { Active: true } mode4)
        {
            bool reverse = (held & (mode4.Up ? 0xC000 : 0x3000)) != 0;
            bool forward = !reverse && (held & (mode4.Up ? 0x3000 : 0xC000)) != 0;
            return forward || reverse ? CreateNativeTraversal(actor, mode4.ScriptId, mode4.Operation, 4, forward, true) : null;
        }
        return null;
    }

    public bool CanApplySemanticTraversal(ReplayNativeTraversal traversal)
    {
        ValidateNativeTraversalProducer(traversal);
        ActorState actor = actors[traversal.Producer.ObjectId];
        if (!IsUserControlEnabled || !IsNativePartyPlayerBody(actor) || actor.NativeMovementCompletion542 == 2)
        {
            return false;
        }
        return traversal.Mode == 3
            ? actor.NativeLadderMode3 is { Active: true } mode3 && mode3.ScriptId == traversal.Producer.ScriptId && mode3.Operation == traversal.Producer.Operation
            : actor.NativeLadder is { Active: true } mode4 && mode4.ScriptId == traversal.Producer.ScriptId && mode4.Operation == traversal.Producer.Operation && (mode4.ReplayCompletion is null || mode4.ReplayCompletion.Forward == traversal.Forward);
    }

    /// <summary>Queues the current mapped direction; ordinary callbacks must reach and report the actual native endpoint.</summary>
    public PlayerActionResult ApplySemanticNativeTraversal(ReplayNativeTraversal traversal)
    {
        if (!CanApplySemanticTraversal(traversal))
        {
            return new PlayerActionResult(false, NextTrigger, 0, "The current native traversal producer is not ready.");
        }
        ActorState actor = actors[traversal.Producer.ObjectId];
        bool up = traversal.Mode == 3 ? actor.NativeLadderMode3!.Up : actor.NativeLadder!.Up;
        ushort mapped = up == traversal.Forward ? (ushort)0x1000 : (ushort)0x4000;
        ushort raw = services.InputConfiguration!.RawForMappedPulse(mapped);
        return Apply(new PlayerAction(PlayerActionKind.FieldButtons, raw));
    }

    private ReplayNativeTraversal CreateNativeTraversal(ActorState actor, int scriptId, int operation, byte mode, bool forward, bool inferred = false)
    {
        string executable = NativeRuntimeProfile is { IsVerified: true } profile ? profile.ExecutableHash : throw new NotSupportedException("Native traversal requires its authenticated executable.");
        ReplayScriptGoal producer = new(FieldId, programHash ?? throw new NotSupportedException("Native traversal requires its current issuing program."), executable, actor.ObjectId, scriptId, operation, (int)program!.Operations[operation].Opcode, inferred);
        ReplayNativeTraversal traversal = new(1, producer, mode, forward, inferred);
        ValidateNativeTraversalProducer(traversal);
        return traversal;
    }

    private void ValidateNativeTraversalProducer(ReplayNativeTraversal traversal)
    {
        traversal.Validate();
        ReplayScriptGoal producer = traversal.Producer;
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } profile || producer.FieldId != FieldId || producer.ProgramHash != programHash || producer.ExecutableHash is string executable && executable != profile.ExecutableHash || (uint)producer.ObjectId >= actors.Count)
        {
            throw new InvalidDataException("Native traversal does not belong to this current verified field program.");
        }
        EventScript? script = program.Objects[producer.ObjectId].Scripts.SingleOrDefault(script => script.Id == producer.ScriptId);
        if (script is null || producer.Operation < script.Start || producer.Operation >= script.End || (int)program.Operations[producer.Operation].Opcode != producer.Code)
        {
            throw new InvalidDataException("Native traversal lost its original issuing opcode and site.");
        }
        Jsm.Opcode opcode = program.Operations[producer.Operation].Opcode;
        if (traversal.Mode == 3 ? opcode is not (Jsm.Opcode.LADDERUP or Jsm.Opcode.LADDERDOWN) : opcode is not (Jsm.Opcode.LADDERUP2 or Jsm.Opcode.LADDERDOWN2))
        {
            throw new InvalidDataException("Native traversal mode differs from its current issuing opcode.");
        }
    }

    private void RecordNativeTraversalCompletion(ReplayNativeTraversal traversal, ushort physicalHeld)
    {
        ValidateNativeTraversalProducer(traversal);
        ReplayScriptGoal producer = traversal.Producer;
        int direction = traversal.Forward ? 1 : -1;
        RecordCausalReceipt(new ReplayReceipt(ReplayReceiptKind.NativeTraversal, FieldId, Frame, producer.ObjectId, producer.ScriptId, producer.Operation, producer.Code, direction, Arguments: [traversal.Mode, direction, physicalHeld]));
    }
}
