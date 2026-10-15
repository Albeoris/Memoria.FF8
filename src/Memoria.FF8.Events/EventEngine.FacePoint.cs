using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private Vector3 PrepareNativeFacePoint(Fiber fiber)
    {
        int[] args = fiber.Stack.Capture().Take(4).Reverse().Select(value => value.ToWidth(2).RequireExact()).ToArray();
        return new Vector3(unchecked((short)args[0]), unchecked((short)args[1]), unchecked((short)args[2]));
    }

    private string? UnsupportedNativeFacePoint(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.Stack.Count < 4 || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.FaceStateVersion != 1 || actor.ModelId < 0 || ReadFaceAnchorHeight is null || ResolveNativeActorFacing is null || actor.FaceElapsed is < short.MinValue or > short.MaxValue || actor.FaceSecondAngle is < short.MinValue or > short.MaxValue || actor.FacePendingBaseline is < short.MinValue or > short.MaxValue)
        {
            return "Native point-facing requires four WORD operands, current allocated face words and the actual native joint/facing providers";
        }
        try
        {
            if (fiber.Stack.Capture().Take(4).Any(value => !value.ToWidth(2).IsExact))
            {
                return "Native point-facing requires exact consumed low WORDs; no representative is selected";
            }
            _ = PrepareNativeFaceTargets(actor, PrepareNativeFacePoint(fiber));
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativeFacePoint(Fiber fiber, bool asynchronous, out NativeScalarValue[]? scalarArguments)
    {
        Vector3 point = PrepareNativeFacePoint(fiber);
        int[] args = PopNativeFaceWords(fiber, out scalarArguments);
        ActorState actor = actors[fiber.ObjectId];
        //527B90/527FA0 share the point WORD stores;527FA0 always returns2.
        actor.FaceTargetPoint = point;
        actor.FaceDuration = unchecked((ushort)args[3]);
        actor.FaceProgress = 0;
        actor.FaceReturning = false;
        fiber.WaitReason = asynchronous || actor.FaceDuration == 0 ? WaitReason.None : WaitReason.FaceDirection;
        return args;
    }

    private static int[] PopNativeFaceWords(Fiber fiber, out NativeScalarValue[]? scalarArguments)
    {
        NativeScalarValue[] original = fiber.Stack.Capture().Take(4).Reverse().ToArray();
        scalarArguments = original.Any(value => !value.IsExact) ? original : null;
        int[] args = new int[4];
        string[] expressions = new string[4];
        for (int index = 3; index >= 0; index--)
        {
            NativeScalarValue raw = fiber.Stack.PopValue();
            args[index] = raw.IsExact ? raw.RequireExact() : raw.ToWidth(2).RequireExact();
            expressions[index] = fiber.StackExpressions.Pop();
        }
        fiber.LastExpressions = expressions;
        return args;
    }

    private (int Pitch, int Yaw) PrepareNativeFaceTargets(ActorState actor, Vector3 target)
    {
        FieldFixedPosition raw = ReadNativeScriptPosition(actor) ?? throw new NotSupportedException("Native face direction requires the current raw actor body position.");
        if (!float.IsFinite(actor.Direction) || actor.Direction != (byte)actor.Direction || new[] { actor.DisplayOffset.X, actor.DisplayOffset.Y, actor.DisplayOffset.Z }.Any(value => !float.IsFinite(value) || value != MathF.Truncate(value) || value is < short.MinValue or > short.MaxValue) || new[] { actor.FaceLimits.X, actor.FaceLimits.Y, actor.FaceLimits.Z }.Any(value => !float.IsFinite(value) || value != MathF.Truncate(value) || value is < 0 or > 255) || new[] { target.X, target.Y, target.Z }.Any(value => !float.IsFinite(value) || value != MathF.Truncate(value) || value is < short.MinValue or > short.MaxValue))
        {
            throw new NotSupportedException("Native face direction requires exact target/display WORDs, BYTE limits and visible body577.");
        }
        float height = ReadFaceAnchorHeight?.Invoke(actor) ?? throw new NotSupportedException("Native face direction requires its actual declared control-node height.");
        if (!float.IsFinite(height) || height < int.MinValue || (double)height > int.MaxValue)
        {
            throw new NotSupportedException("Native face control-node height exceeds its bounded query30 result.");
        }
        //472B30 consumes SAR12 body coordinates, signed display WORDs and the low signedWORD of query30.
        int x = (raw.X >> 12) + (int)actor.DisplayOffset.X;
        int y = (raw.Y >> 12) + (int)actor.DisplayOffset.Y;
        int z = (raw.Z >> 12) + (int)actor.DisplayOffset.Z + unchecked((short)(int)height);
        long dx = (long)(int)target.X - x;
        long dy = (long)(int)target.Y - y;
        long squared = dx * dx + dy * dy;
        if (squared > int.MaxValue)
        {
            throw new NotSupportedException("Native face direction square-root overflow is outside the proved477380 contract.");
        }
        int distance = (int)Math.Sqrt(squared);
        byte yaw = ResolveNativeFaceAngle(x, y, (int)target.X, (int)target.Y);
        byte pitch = ResolveNativeFaceAngle(z, distance, (int)target.Z, 0);
        return (Math.Clamp(unchecked((sbyte)pitch), -(int)actor.FaceLimits.X, (int)actor.FaceLimits.X), Math.Clamp(unchecked((sbyte)(yaw - (byte)actor.Direction)), -(int)actor.FaceLimits.Z, (int)actor.FaceLimits.Z));
    }

    private static void ValidateNativeFaceCheckpoint(System.Text.Json.JsonElement json, ActorState actor)
    {
        foreach (string name in new[] { nameof(ActorState.FaceFirstAngle), nameof(ActorState.FaceSecondAngle), nameof(ActorState.FaceElapsed), nameof(ActorState.FaceLimits), nameof(ActorState.FaceReturning), nameof(ActorState.FacePendingBaseline), nameof(ActorState.FaceTargetPoint), nameof(ActorState.FacePitchTarget), nameof(ActorState.FaceYawTarget), nameof(ActorState.FaceDuration), nameof(ActorState.FaceProgress), nameof(ActorState.FirstControlRotation) })
        {
            if (!json.TryGetProperty(name, out _))
            {
                throw new InvalidDataException("Saved actor lacks the current native face words; migrate this save explicitly.");
            }
        }
        if (actor.FaceDuration is < 0 or > ushort.MaxValue || actor.FaceProgress < 0 || actor.FaceProgress > actor.FaceDuration || new[] { actor.FaceFirstAngle, actor.FaceSecondAngle, actor.FaceElapsed, actor.FacePendingBaseline, actor.FacePitchTarget, actor.FaceYawTarget }.Any(value => value is < short.MinValue or > short.MaxValue) || new[] { actor.FaceLimits.X, actor.FaceLimits.Y, actor.FaceLimits.Z }.Any(value => !float.IsFinite(value) || value != MathF.Truncate(value) || value is < 0 or > 255) || actor.FaceTargetPoint is Vector3 point && new[] { point.X, point.Y, point.Z }.Any(value => !float.IsFinite(value) || value != MathF.Truncate(value) || value is < short.MinValue or > short.MaxValue))
        {
            throw new InvalidDataException("Saved native face duration/progress, target/start WORDs or BYTE limits are inconsistent.");
        }
    }

    private byte ResolveNativeFaceAngle(int x, int y, int targetX, int targetY)
    {
        //The current provider implements original477380's integer normalization and byte table.
        NativeActorFacingQuery query = new(unchecked(x << 12), unchecked(y << 12), unchecked(targetX << 12), unchecked(targetY << 12));
        return ResolveNativeActorFacing?.Invoke(query) ?? throw new NotSupportedException("Native face direction lacks its proved477380 angle proposal.");
    }
}
