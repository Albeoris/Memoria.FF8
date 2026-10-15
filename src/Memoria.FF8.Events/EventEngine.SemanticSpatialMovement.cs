using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static bool IsSemanticSpatialMovementOpcode(Jsm.Opcode opcode)
    {
        return opcode is Jsm.Opcode.MOVE or Jsm.Opcode.RMOVE or Jsm.Opcode.FMOVE or Jsm.Opcode.RFMOVE or Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE or Jsm.Opcode.MACCEL or Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA or Jsm.Opcode.PMOVEA or Jsm.Opcode.RPMOVEA or Jsm.Opcode.MOVESYNC or Jsm.Opcode.MOVECANCEL or Jsm.Opcode.JOIN or Jsm.Opcode.SPLIT or Jsm.Opcode.JUMP or Jsm.Opcode.JUMP3 or Jsm.Opcode.PJUMPA;
    }

    private static int SemanticSpatialMovementOperandCount(Jsm.Opcode opcode)
    {
        return opcode switch
        {
            Jsm.Opcode.MOVE or Jsm.Opcode.RMOVE or Jsm.Opcode.FMOVE or Jsm.Opcode.RFMOVE or Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE or Jsm.Opcode.JUMP3 => 4,
            Jsm.Opcode.MACCEL => 5,
            Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA or Jsm.Opcode.PMOVEA or Jsm.Opcode.RPMOVEA or Jsm.Opcode.PJUMPA => 2,
            Jsm.Opcode.JUMP => 3,
            Jsm.Opcode.MOVECANCEL => 1,
            Jsm.Opcode.SPLIT => 9,
            Jsm.Opcode.MOVESYNC or Jsm.Opcode.JOIN => 0,
            _ => throw new NotSupportedException("This opcode has no semantic spatial movement policy.")
        };
    }

    private string? UnsupportedSemanticSpatialMovement(Fiber fiber, JsmOperation operation)
    {
        try
        {
            _ = PrepareSemanticSpatialMovement(fiber, operation);
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteSemanticSpatialMovement(Fiber fiber, JsmOperation operation)
    {
        var placements = PrepareSemanticSpatialMovement(fiber, operation);
        int[] operands = Pop(fiber, SemanticSpatialMovementOperandCount(operation.Opcode));
        foreach (var placement in placements)
        {
            TeleportSemanticActor(placement.Actor, placement.Position, placement.Triangle);
        }
        if (operation.Opcode == Jsm.Opcode.JOIN)
        {
            // Completed-visit reconstruction intentionally has no companion formation.
            partyJoin = null;
        }
        if (operation.Opcode == Jsm.Opcode.SPLIT)
        {
            partySplit = null;
        }
        fiber.NativeMoveOperation = null;
        fiber.NativeMoveTarget = null;
        return operands;
    }

    private (ActorState Actor, FieldFixedPosition Position, int? Triangle)[] PrepareSemanticSpatialMovement(Fiber fiber, JsmOperation operation)
    {
        int count = SemanticSpatialMovementOperandCount(operation.Opcode);
        if (fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact))
        {
            throw new NotSupportedException("Semantic spatial movement requires all original exact DWORD operands before consumption.");
        }
        int[] operands = fiber.Stack.Take(count).Reverse().ToArray();
        ActorState actor = actors[fiber.ObjectId];
        Jsm.Opcode opcode = operation.Opcode;
        if (opcode is Jsm.Opcode.MOVESYNC or Jsm.Opcode.JOIN)
        {
            return [];
        }
        if (opcode == Jsm.Opcode.SPLIT)
        {
            List<(ActorState Actor, FieldFixedPosition Position, int? Triangle)> placements = [];
            for (int slot = 0; slot < 3; slot++)
            {
                ActorState? member = ResolveNativeIndexedPartyBody(slot, out _, out _);
                if (member is not null)
                {
                    placements.Add((member, new FieldFixedPosition(unchecked(operands[slot * 3] << 12), unchecked(operands[slot * 3 + 1] << 12), unchecked(operands[slot * 3 + 2] << 12)), null));
                }
            }
            return placements.ToArray();
        }
        if (opcode == Jsm.Opcode.MOVECANCEL)
        {
            ActorState? cancelled = ReadSemanticSpatialNativeActor(operands[0]);
            return cancelled is null ? [] : [(cancelled, RequireSemanticSpatialPosition(cancelled), null)];
        }
        if (opcode is Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA or Jsm.Opcode.PMOVEA or Jsm.Opcode.RPMOVEA or Jsm.Opcode.PJUMPA)
        {
            ActorState? target = opcode is Jsm.Opcode.PMOVEA or Jsm.Opcode.RPMOVEA or Jsm.Opcode.PJUMPA ? ResolveNativeIndexedPartyBody(operands[0], out _, out _) : ReadSemanticSpatialNativeActor(operands[0]);
            return target is null ? [] : [(actor, RequireSemanticSpatialPosition(target), opcode == Jsm.Opcode.PJUMPA ? target.WalkmeshTriangle : null)];
        }
        int z = opcode == Jsm.Opcode.JUMP ? RequireSemanticSpatialPosition(actor).Z : unchecked(operands[2] << 12);
        int? triangle = opcode is Jsm.Opcode.JUMP or Jsm.Opcode.JUMP3 ? unchecked((ushort)operation.Parameter) : null;
        return [(actor, new FieldFixedPosition(unchecked(operands[0] << 12), unchecked(operands[1] << 12), z), triangle)];
    }

    private ActorState? ReadSemanticSpatialNativeActor(int nativeObjectId)
    {
        if (program is null || (uint)nativeObjectId >= program.NativeObjects.Count)
        {
            throw new NotSupportedException("Semantic actor-target movement requires its original full native object index.");
        }
        ActorState actor = actors[program.NativeObjects[nativeObjectId]];
        return Scene.NativeActorObjectIds.Contains(actor.ObjectId) ? actor : null;
    }

    private FieldFixedPosition RequireSemanticSpatialPosition(ActorState actor)
    {
        return ReadNativeScriptPosition(actor) ?? throw new NotSupportedException("Semantic spatial movement requires the current actor's actual position owner.");
    }
}
