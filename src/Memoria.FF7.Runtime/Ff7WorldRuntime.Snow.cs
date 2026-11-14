using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldSnowState(int PoleIndex = 0, int Countdown = 0, int WindLength = 0, int WindVelocity = 0, int CycleLength = 0, int LockedFacing = 0);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldSnowState? snow;

    private void ApplySnowWorldControls(ref Ff7WorldEntity player, ushort held, ushort pressed, ref int dx, ref int dz)
    {
        snow ??= new();
        int angle = camera.Heading;
        int countdown = snow.Countdown;
        int length = snow.WindLength;
        int locked = snow.LockedFacing;
        int wind = snow.WindVelocity;
        int cycle = snow.CycleLength;
        if (countdown == length)
        {
            locked = player.Direction + angle;
        }
        else if (countdown < length)
        {
            player = player with { TargetDirection = (locked - angle) & 4095 };
        }
        if ((held & 0xF000) != 0 || countdown < length)
        {
            countdown--;
        }
        if (countdown <= 0)
        {
            countdown = cycle = WorldRandom.Next() + 128;
            length = (WorldRandom.Next() >> 3) + 64;
            wind = ((WorldRandom.Next() & 2) - 1) << 4;
        }
        int poleIndex = snow.PoleIndex;
        if (countdown >= length && (pressed & 0x20) != 0)
        {
            int model = 21 + poleIndex;
            Machine.EnsurePresentationModel(model);
            Ff7WorldEntity pole = RequireEntity(model);
            Vector3 offset = inputMath.TransformRotation(new(0, 0, 300), 0, -angle, 0);
            int direction = -angle & 4095;
            entities[model] = pole with { X = Wrap(player.X + (int)offset.X, 0x48000), Y = player.Y, Z = Wrap(player.Z + (int)offset.Z, 0x38000), Direction = direction, TargetDirection = direction, ScriptDirection = null, TurnOffset = 0, PresentationEpoch = pole.PresentationEpoch + 1 };
            poleIndex = (poleIndex + 1) % 3;
        }
        int target = ((input.CameraHeadingTarget ?? camera.Heading) + (countdown < length ? wind : 0)) & 4095;
        input = input with { CameraHeadingTarget = target };
        Vector3 displacement = inputMath.TransformRotation(new(dx, 0, dz), 0, -target, 0);
        dx = (int)displacement.X;
        dz = (int)displacement.Z;
        snow = new(poleIndex, countdown, length, wind, cycle, locked);
    }

    private static void ValidateSnowState(Ff7WorldSnowState? state, int world)
    {
        if (state is not null && (world != 3 || state.PoleIndex is < 0 or > 2 || state.Countdown is < 0 or > 383 || state.WindLength is < 0 or > 95 || state.WindVelocity is not (-16 or 0 or 16) || state.CycleLength is < 0 or > 383 || state.LockedFacing is < 0 or > 8190))
        {
            throw new InvalidDataException("FFVII snow-world wind and marker state is corrupt.");
        }
    }
}
