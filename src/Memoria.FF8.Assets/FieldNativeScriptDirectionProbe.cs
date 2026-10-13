namespace Memoria.FF8.Assets;

public enum FieldNativeScriptDirectionStatus
{
    Moving,
    Stopped,
    Snapped,
    Unsupported
}

public readonly record struct FieldNativeScriptDirectionRequest(int FixedX, int FixedY, int TargetX, int TargetY, short Speed, short StopDistance, ushort Radius, byte MovementDirection, byte FacingDirection, short TurnAccumulator, byte DirectionLimit, byte DirectionOffset = 0);
public sealed record FieldNativeScriptDirectionResult(FieldNativeScriptDirectionStatus Status, int FixedX, int FixedY, byte Direction, short TurnAccumulator, int? DistanceSquared, int? Distance, byte? DesiredDirection, string? UnsupportedReason);

/// <summary>Bounded original 0x479970 XY stop, snap and limited direction selection. Ground/contact movement and native body-direction updates belong to the caller.</summary>
public static class FieldNativeScriptDirectionProbe
{
    // Even-byte metadata from the original 0xB80C18 table; entries90..128 all equal32.
    private static readonly byte[] DirectionAngles = [0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 20, 20, 20, 21, 21, 21, 22, 22, 22, 23, 23, 24, 24, 24, 25, 25, 26, 26, 26, 27, 27, 28, 28, 28, 29, 29, 30, 30, 30, 31, 31, 32];

    /// <summary>Native47A880→477380 direct BYTE facing, without MOVE stop/snap policy.</summary>
    public static byte? ResolveFacing(int fixedX, int fixedY, int targetX, int targetY)
    {
        int dx = (targetX >> 12) - (fixedX >> 12);
        int dy = (targetY >> 12) - (fixedY >> 12);
        long squared = (long)dx * dx + (long)dy * dy;
        if (squared > int.MaxValue)
        {
            return null;
        }
        int length = IntegerSquareRoot((int)squared);
        if (length == 0)
        {
            return 0;
        }
        int x = unchecked(dx << 12) / length / 32;
        int y = unchecked(dy << 12) / length / 32;
        bool horizontal = x * x > y * y;
        int minor = Math.Abs(horizontal ? y : x);
        if (minor > 128)
        {
            return null;
        }
        int angle = DirectionAngles[Math.Min(minor, 90)];
        return unchecked((byte)(horizontal ? x > 0 ? 64 + Math.Sign(y) * angle : 192 - Math.Sign(y) * angle : y > 0 ? 128 - Math.Sign(x) * angle : Math.Sign(x) * angle));
    }

    public static FieldNativeScriptDirectionResult Resolve(FieldNativeScriptDirectionRequest request)
    {
        int dx = (request.TargetX >> 12) - (request.FixedX >> 12);
        int dy = (request.TargetY >> 12) - (request.FixedY >> 12);
        long squared = (long)dx * dx + (long)dy * dy;
        int? distanceSquared = null;
        int? distance = null;

        FieldNativeScriptDirectionResult Result(FieldNativeScriptDirectionStatus status, byte direction, short accumulator, byte? desired = null, string? reason = null)
        {
            bool snapped = status == FieldNativeScriptDirectionStatus.Snapped;
            return new FieldNativeScriptDirectionResult(status, snapped ? request.TargetX : request.FixedX, snapped ? request.TargetY : request.FixedY, direction, accumulator, distanceSquared, distance, desired, reason);
        }

        if (squared > int.MaxValue)
        {
            return Result(FieldNativeScriptDirectionStatus.Unsupported, request.MovementDirection, request.TurnAccumulator, reason: "Native distance overflow and its subsequent square-root policy are outside this bounded contract.");
        }
        distanceSquared = (int)squared;
        if (request.StopDistance != 0)
        {
            int stopRadius = request.Radius + request.StopDistance;
            int threshold = unchecked(stopRadius * stopRadius + 4096);
            if (distanceSquared <= threshold)
            {
                return Result(FieldNativeScriptDirectionStatus.Stopped, request.MovementDirection, request.TurnAccumulator);
            }
        }
        int snapThreshold = request.Speed * request.Speed >> 16;
        if (distanceSquared < snapThreshold || distanceSquared < 4)
        {
            return Result(FieldNativeScriptDirectionStatus.Snapped, request.MovementDirection, request.TurnAccumulator);
        }
        if (request.DirectionOffset != 0)
        {
            return Result(FieldNativeScriptDirectionStatus.Unsupported, request.MovementDirection, request.TurnAccumulator, reason: "Nonzero native direction offset574 requires the original unsigned-WORD branch contract.");
        }

        int length = IntegerSquareRoot(distanceSquared.Value);
        distance = length;
        byte? facing = ResolveFacing(request.FixedX, request.FixedY, request.TargetX, request.TargetY);
        if (facing is null)
        {
            return Result(FieldNativeScriptDirectionStatus.Unsupported, request.MovementDirection, request.TurnAccumulator, reason: "Native direction query leaves the proved original table range.");
        }
        byte desired = facing.Value;
        if (length < request.Radius || request.TurnAccumulator is < -256 or > 256 || request.DirectionLimit == 0)
        {
            return Result(FieldNativeScriptDirectionStatus.Moving, desired, request.TurnAccumulator, desired);
        }

        int difference = desired - request.FacingDirection;
        if (difference > 128)
        {
            difference -= 256;
        }
        else if (difference < -128)
        {
            difference += 256;
        }
        // +/-128 keep their original sign. Exact +/-limit takes a full step and changes474.
        if (difference > -request.DirectionLimit && difference < request.DirectionLimit)
        {
            return Result(FieldNativeScriptDirectionStatus.Moving, desired, request.TurnAccumulator, desired);
        }
        int step = Math.Sign(difference) * request.DirectionLimit;
        byte nextDirection = unchecked((byte)(request.FacingDirection + step));
        short nextAccumulator = unchecked((short)(request.TurnAccumulator + step));
        return Result(FieldNativeScriptDirectionStatus.Moving, nextDirection, nextAccumulator, desired);
    }

    private static int IntegerSquareRoot(int value)
    {
        int root = (int)Math.Sqrt(value);
        while ((long)(root + 1) * (root + 1) <= value)
        {
            root++;
        }
        while ((long)root * root > value)
        {
            root--;
        }
        return root;
    }
}
