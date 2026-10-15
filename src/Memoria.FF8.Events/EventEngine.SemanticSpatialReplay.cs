using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private int semanticSpatialReplayDepth;
    private int semanticSpatialPositionGeneration;
    private readonly Dictionary<int, FieldFixedPosition> semanticSpatialPositions = [];

    public bool IsSemanticSpatialReplay => semanticSpatialReplayDepth > 0;

    /// <summary>Completed visits execute current semantic scripts while deliberately omitting spatial presentation and traversal.</summary>
    public IDisposable BeginSemanticSpatialReplay()
    {
        if (semanticSpatialReplayDepth++ == 0)
        {
            semanticSpatialPositions.Clear();
            semanticSpatialPositionGeneration = generation;
        }
        return new SemanticSpatialReplayScope(this);
    }

    private sealed class SemanticSpatialReplayScope(EventEngine owner) : IDisposable
    {
        private EventEngine? engine = owner;

        public void Dispose()
        {
            EventEngine? current = engine;
            if (current is null)
            {
                return;
            }
            engine = null;
            if (--current.semanticSpatialReplayDepth == 0)
            {
                current.semanticSpatialPositions.Clear();
            }
        }
    }

    private static bool IsSemanticSpatialOpcode(Jsm.Opcode opcode)
    {
        return IsSemanticSpatialFacingOpcode(opcode) || IsSemanticSpatialMovementOpcode(opcode);
    }

    private string? UnsupportedSemanticSpatialInstruction(Fiber fiber, JsmOperation operation)
    {
        if (IsSemanticSpatialMovementOpcode(operation.Opcode))
        {
            return UnsupportedSemanticSpatialMovement(fiber, operation);
        }
        int count = SemanticSpatialFacingOperandCount(operation.Opcode);
        if (fiber.Stack.Count < count || fiber.StackExpressions.Count < count)
        {
            return $"{operation.Opcode} requires {count} current facing operands.";
        }
        NativeScalarValue[] values = fiber.Stack.Capture().Take(count).Reverse().ToArray();
        for (int index = 0; index < values.Length; index++)
        {
            bool word = operation.Opcode is Jsm.Opcode.FACEDIR or Jsm.Opcode.RFACEDIR or Jsm.Opcode.FACEDIROFF or Jsm.Opcode.RFACEDIROFF
                || index == count - 1 && operation.Opcode is (Jsm.Opcode.FACEDIRP or Jsm.Opcode.RFACEDIRP or Jsm.Opcode.FACEDIRA or Jsm.Opcode.RFACEDIRA);
            if (!values[index].IsExact && !values[index].ToWidth(word ? 2 : 4).IsExact)
            {
                return $"{operation.Opcode} requires its current exact facing operands.";
            }
        }
        return null;
    }

    private FieldFixedPosition? ReadSemanticSpatialPosition(ActorState actor)
    {
        if (!IsSemanticSpatialReplay)
        {
            return null;
        }
        if (semanticSpatialPositionGeneration != generation)
        {
            semanticSpatialPositions.Clear();
            semanticSpatialPositionGeneration = generation;
        }
        if (semanticSpatialPositions.TryGetValue(actor.ObjectId, out FieldFixedPosition? position))
        {
            if (actor.X == position.X / 4096f && actor.Y == position.Y / 4096f && actor.Z == position.Z / 4096f)
            {
                return position;
            }
            semanticSpatialPositions.Remove(actor.ObjectId);
        }
        return null;
    }

    private void TeleportSemanticActor(ActorState actor, FieldFixedPosition position, int? triangle = null)
    {
        if (!IsSemanticSpatialReplay)
        {
            throw new InvalidOperationException("Semantic spatial placement requires its completed-visit replay scope.");
        }
        if (semanticSpatialPositionGeneration != generation)
        {
            semanticSpatialPositions.Clear();
            semanticSpatialPositionGeneration = generation;
        }
        NativeMotionAuthority? authority = actor.NativeMotionAuthority;
        actor.InvalidateNativeMotionAuthority();
        actor.NativeMotionAuthority = authority;
        actor.NativeMovementRequest = null;
        actor.MovementTarget = null;
        actor.MovementActorId = null;
        actor.NativeMovementCompletion542 = 2;
        actor.X = position.X / 4096f;
        actor.Y = position.Y / 4096f;
        actor.Z = position.Z / 4096f;
        actor.WalkmeshTriangle = triangle ?? ResolvePartyTraceTriangle?.Invoke(position.X / 4096, position.Y / 4096, position.Z / 4096) ?? actor.WalkmeshTriangle;
        semanticSpatialPositions[actor.ObjectId] = position;
        if (IsNativePartyPlayerBody(actor))
        {
            Scene.NativePlayerPosition = position;
        }
    }
}
