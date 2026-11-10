using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private void SetWorldViewMode(int mode)
    {
        viewMode = mode;
        if (mode is 0 or 1)
        {
            input = input with { CameraHeadingTarget = 0 };
        }
    }

    private void SynchronizeFreeDirection(Ff7WorldEntity entity)
    {
        input = input with { FreeDirectionOffset = entity.TargetDirection + (input.CameraHeadingTarget ?? camera.Heading) - 2048 };
    }

    private void ApplyWorldControlOptions(ref Ff7WorldEntity player, ref int dx, ref int dz, int heading)
    {
        ushort held = input.Held;
        held |= input.Horizontal < 0 ? (ushort)0x8000 : input.Horizontal > 0 ? (ushort)0x2000 : (ushort)0;
        held |= input.Vertical < 0 ? (ushort)0x1000 : input.Vertical > 0 ? (ushort)0x4000 : (ushort)0;
        ushort pressed = (ushort)(held & ~input.PreviousControlHeld);
        if ((held & 12) != 0 && viewMode is not (2 or 3))
        {
            SetWorldViewMode(2);
        }
        if ((pressed & 3) != 0)
        {
            SetWorldViewMode(viewMode != 0 ? 0 : 2);
        }
        if ((pressed & 0x900) != 0)
        {
            radarMode = (radarMode + 1) % 3;
        }
        input = input with { PreviousControlHeld = held };
        if (viewMode == 3)
        {
            throw new NotSupportedException("FFVII foot controls do not support the native vehicle camera mode.");
        }
        if (viewMode == 2)
        {
            int left = ((held & 4) != 0 ? 1 : 0) + ((held & (input.Vertical > 0 ? 0x2000 : 0x8000)) != 0 ? 1 : 0);
            int right = ((held & 8) != 0 ? 1 : 0) + ((held & (input.Vertical > 0 ? 0x8000 : 0x2000)) != 0 ? 1 : 0);
            int target = ((input.CameraHeadingTarget ?? camera.Heading) + (right - left) * 16 * (input.Vertical != 0 ? 1 : 2)) & 4095;
            input = input with { CameraHeadingTarget = target };
            if (heading != -1)
            {
                player = TurnWorldPlayer(player, heading - target);
            }
            Vector3 displacement = inputMath.TransformRotation(new(dx, 0, dz), 0, -target, 0);
            dx = (int)displacement.X;
            dz = (int)displacement.Z;
            int offset;
            if (heading != -1)
            {
                offset = heading + 2048;
                if (offset > 2048)
                {
                    offset -= 4096;
                }
                if (input.Vertical <= 0)
                {
                    offset >>= 1;
                }
                input = input with { FreeDirectionOffset = offset };
            }
            else
            {
                offset = input.FreeDirectionOffset ?? player.TargetDirection + target - 2048;
            }
            player = TurnWorldPlayer(player, 2048 - target + offset);
        }
        else if (heading != -1)
        {
            player = TurnWorldPlayer(player, heading);
        }
    }

    private void AdvanceWorldCameraHeading()
    {
        int target = input.CameraHeadingTarget ?? camera.Heading;
        int heading = camera.Heading;
        if (heading < target - 2048)
        {
            heading += 4096;
        }
        else if (heading > target + 2048)
        {
            heading -= 4096;
        }
        camera = camera with { Heading = (heading * 15 + target) >> 4 };
    }

    private static Ff7WorldEntity TurnWorldPlayer(Ff7WorldEntity player, int heading)
    {
        heading &= 4095;
        int desired = heading;
        if (Math.Abs(player.Direction - desired) > Math.Abs(player.Direction - desired + 4096))
        {
            desired -= 4096;
        }
        else if (Math.Abs(player.Direction - desired) > Math.Abs(player.Direction - desired - 4096))
        {
            desired += 4096;
        }
        return player with { Direction = ((player.Direction * 3 + desired) >> 2) & 4095, TargetDirection = heading };
    }
}
