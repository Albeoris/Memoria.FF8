using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PrepareNativeActionMatrices(NativeBattleGraphicsState graphics)
    {
        return NativeBattleActionGeometryRules.PrepareMatrices50D300(graphics, ReadNativeGraphicsBone);
    }
}

public static partial class NativeBattleActionGeometryRules
{
    public static NativeBattleGraphicsState PrepareMatrices50D300(NativeBattleGraphicsState graphics, Func<NativeBattleGraphicsState, int, byte, short, NativeBattleVector> readBone)
    {
        NativeBattleActionGeometry context = graphics.ActionContext ?? throw new InvalidDataException("Native action matrices lack their50D300 context.");
        if (context.PhysicalTargetMask is not ushort mask || mask == 0)
        {
            return graphics;
        }
        int actorIndex = context.ActorIndex;
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native action matrices require actor position WORDs.");
        NativeBattleVector actorPosition = new(transform.Position28.X, transform.Angle36, transform.Position28.Z);
        NativeBattleVector actorFirst = readBone(graphics, actorIndex, 240, 2048);
        NativeBattleVector actorSecond = readBone(graphics, actorIndex, 241, 2048);
        NativeBattleVector targetPosition;
        NativeBattleVector targetFirst;
        NativeBattleVector targetSecond;
        if (BitOperations.PopCount(mask) == 1)
        {
            int targetIndex = context.TargetIndex ?? throw new InvalidDataException("Native single-target matrices lack their physical recipient.");
            NativeBattleGraphicsModel target = graphics.Models[targetIndex];
            NativeBattleModelTransform targetTransform = target.Transform ?? throw new NotSupportedException("Native action matrices require target position WORDs.");
            targetFirst = readBone(graphics, targetIndex, 240, 2048);
            targetSecond = readBone(graphics, targetIndex, 241, 2048);
            targetPosition = target.ModelId4 is 96 or 97 ? targetSecond : targetTransform.Position28;
            targetPosition = new(targetPosition.X, targetTransform.Angle36, targetPosition.Z);
        }
        else
        {
            NativeBattleVector[] positions = Enumerable.Range(0, 7).Where(index => (mask & 1 << index) != 0).Select(index => graphics.Models[index].Transform?.Position28 ?? throw new NotSupportedException("Native target bounds require every physical target position.")).ToArray();
            targetPosition = NativeBattleActionGeometryRules.TargetCenter(positions);
            targetFirst = new(targetPosition.X, actorFirst.Y, targetPosition.Z);
            targetSecond = new(targetPosition.X, actorSecond.Y, targetPosition.Z);
        }
        NativeBattleVector fallback = new(actor.Rotation12 ?? throw new NotSupportedException("Native action fallback requires rotationWORD12."), unchecked((short)((actor.Rotation14 ?? throw new NotSupportedException("Native action fallback requires rotationWORD14.")) + 2048)), actor.Rotation16 ?? throw new NotSupportedException("Native action fallback requires rotationWORD16."));
        NativeBattleActionMatrix primary = NativeBattleActionGeometryRules.CaptureMatrix(actorPosition, targetPosition, fallback);
        context = context with
        {
            DistanceScale = primary.Scale,
            UnadjustedDistance = context.SourceTargetMask == 1 << actorIndex ? 1000 : 5000 * primary.Scale >> 12,
            Matrices =
            [
                primary,
                NativeBattleActionGeometryRules.CaptureMatrix(actorFirst, targetFirst, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorFirst, targetSecond, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorSecond, targetFirst, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorSecond, targetSecond, fallback)
            ],
            TargetMatrix = new(NativeBattlePoseMatrices.RotationWords(new(0, (mask & 7) == 0 ? (short)2048 : (short)0, 0)), targetPosition.X, targetPosition.Y, targetPosition.Z, 4096)
        };
        return graphics with { ActionContext = context };
    }
}
