using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private GameplaySaveActorTurn? CaptureGameplaySaveTurn(ActorState actor)
    {
        if (actor.RotationTarget is not float target)
        {
            if (actor.RotationUsesNativeCosine || actor.RotationUsesNativeLinear)
            {
                throw new InvalidDataException("Gameplay native turn flags have no represented active target.");
            }
            return null;
        }
        if (actor.RotationUsesNativeCosine && actor.RotationUsesNativeLinear || !float.IsFinite(target) || target != MathF.Truncate(target) || target is < short.MinValue or > short.MaxValue || !float.IsFinite(actor.RotationStart) || actor.RotationStart != MathF.Truncate(actor.RotationStart) || actor.RotationStart is < 0 or > 255 || actor.RotationTotalFrames is < 0 or > 255 || actor.RotationFrames is < 0 or > 256 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId))
        {
            throw new InvalidDataException("Gameplay native body turn requires its allocated actor and exact BYTE/WORD phase.");
        }
        GameplaySaveActorTurn result = new(actor.RotationUsesNativeLinear ? (byte)1 : actor.RotationUsesNativeCosine ? (byte)2 : (byte)3, (byte)actor.RotationStart, (short)target, (byte)actor.RotationTotalFrames, (ushort)actor.RotationFrames);
        result.Validate();
        int elapsed = Math.Max(0, result.Duration - result.Remaining);
        float delta = (result.Target - result.Start + 384) % 256 - 128;
        float direction = result.Kind == 3 ? result.Duration == 0 ? result.Start : (result.Start + delta * (1 - result.Remaining / (float)Math.Max(1, (int)result.Duration)) + 256) % 256 : unchecked((byte)(result.Kind == 1 ? NativeJumpMath.Interpolate(result.Start, result.Target, result.Duration, (short)elapsed) : NativeCosineInterpolation.Interpolate(result.Start, result.Target, result.Duration, elapsed)));
        if (actor.Direction != direction && !NativeMovementOwnsFacing(actor))
        {
            throw new InvalidDataException("Gameplay native turn visible direction disagrees with its logical phase.");
        }
        return result;
    }

    private static void RestoreGameplaySaveTurn(ActorState actor, GameplaySaveActorTurn? saved)
    {
        saved?.Validate();
        actor.RotationTarget = saved?.Target;
        actor.RotationStart = saved?.Start ?? 0;
        actor.RotationTotalFrames = saved?.Duration ?? 0;
        actor.RotationFrames = saved?.Remaining ?? 0;
        actor.RotationUsesNativeLinear = saved?.Kind == 1;
        actor.RotationUsesNativeCosine = saved?.Kind == 2;
    }

    private void ValidateGameplayTurnWait(Fiber fiber)
    {
        if (fiber.WaitReason is not (WaitReason.Rotation or WaitReason.NativeTurnCompletion))
        {
            return;
        }
        if (program is null || fiber.Complete || fiber.Initialization || (uint)fiber.ObjectId >= actors.Count || !Scene.NativeActorObjectIds.Contains(fiber.ObjectId) || actors[fiber.ObjectId].NativeTurnMode580 is null or > 3)
        {
            throw new InvalidDataException("Gameplay turn wait requires a live allocated actor and its native completion byte.");
        }
        _ = CaptureGameplaySaveTurn(actors[fiber.ObjectId]);
        if (fiber.WaitReason == WaitReason.NativeTurnCompletion && fiber.Script.ContainsOperation(fiber.Position) && program.Operations[fiber.Position].Opcode == Jsm.Opcode.Unknown12)
        {
            return;
        }
        if (!fiber.Script.ContainsOperation(fiber.Position - 1))
        {
            throw new InvalidDataException("Gameplay turn wait lost its consumed instruction.");
        }
        Jsm.Opcode opcode = program.Operations[fiber.Position - 1].Opcode;
        bool rotation = opcode is Jsm.Opcode.CTURNL or Jsm.Opcode.CTURNR or Jsm.Opcode.LTURNL or Jsm.Opcode.LTURNR or Jsm.Opcode.Unknown6 or Jsm.Opcode.Unknown7 or Jsm.Opcode.Unknown8 or Jsm.Opcode.Unknown9 or Jsm.Opcode.CTURN or Jsm.Opcode.LTURN;
        bool party = opcode is Jsm.Opcode.PLTURN or Jsm.Opcode.PCTURN;
        if (fiber.WaitReason == WaitReason.Rotation ? !rotation : !party)
        {
            throw new InvalidDataException("Gameplay native turn wait does not match its consumed or retried instruction.");
        }
    }

    private void ValidateGameplayMovementSyncWait(Fiber fiber)
    {
        if (fiber.WaitReason != WaitReason.Movement)
        {
            return;
        }
        if (program is null || fiber.Complete || fiber.Initialization || !fiber.Script.ContainsOperation(fiber.Position) || program.Operations[fiber.Position].Opcode != Jsm.Opcode.MOVESYNC || !Scene.NativeActorObjectIds.Contains(fiber.ObjectId) || actors[fiber.ObjectId].NativeMovementCompletion542 is null)
        {
            throw new InvalidDataException("Gameplay movement sync requires its held MOVESYNC instruction and allocated actor completion byte.");
        }
    }
}
