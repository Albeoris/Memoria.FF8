using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldCameraOcclusion(int PitchOffset = 0, int Filter = 0, int Consecutive = 0, bool PreviousBlocked = false, int Delay = 0, bool Adjusting = false);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldCameraOcclusion cameraOcclusion = new();
    private Ff7WorldCameraMath cameraMath = null!;

    private int? ScriptedOrTerrainPitch => scriptCamera?.TargetPitch is > 0 ? scriptCamera.TargetPitch : cameraOcclusion.PitchOffset != 0 ? cameraController.Profile(viewMode).Pitch + cameraOcclusion.PitchOffset : null;

    private void AdvanceWorldCameraOcclusion()
    {
        if (mainState != 1 || Resources.World == 2 || controlledModel < 0)
        {
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        Vector3 focus = new(player.X, player.Y, player.Z);
        Ff7WorldCameraTransform transform = cameraMath.Create(focus, camera.Heading, camera.Pitch, camera.Distance, camera.GroundHeight, camera.Roll);
        bool occluded = false;
        foreach (Ff7WorldChunkPlacement chunk in visibility.RequiredChunks(player.X, player.Z, camera.Heading, worldMapState))
        {
            var mesh = GetWorldCollisionMesh(chunk.ChunkX, chunk.ChunkZ);
            if (Ff7WorldCameraMath.Occluded(mesh, chunk.Origin, transform, camera.Focal, viewMode, 5000 - ((vehicle?.FlightProgress ?? 0) * 2500 >> 8)))
            {
                occluded = true;
                break;
            }
        }
        int filter = (cameraOcclusion.Filter * 7 + (occluded ? 256 : 0)) >> 3;
        bool blocked = filter > 128;
        int consecutive = blocked ? checked(cameraOcclusion.Consecutive + 1) : 0;
        int delay = cameraOcclusion.Delay;
        bool adjusting = cameraOcclusion.Adjusting;
        if (cameraOcclusion.PreviousBlocked != blocked)
        {
            adjusting = false;
            delay = 15;
        }
        if ((player.Flags & 1) != 0 || consecutive >= 15)
        {
            if (delay-- < 0)
            {
                adjusting = true;
            }
        }
        int offset = cameraOcclusion.PitchOffset;
        if (adjusting)
        {
            if (blocked && offset > -350)
            {
                offset -= 5;
            }
            else if (!blocked && offset < 0)
            {
                offset += 5;
            }
        }
        cameraOcclusion = new(offset, filter, consecutive, blocked, delay, adjusting);
    }

    private static void ValidateCameraOcclusion(Ff7WorldCameraOcclusion? state)
    {
        if (state is not null && (state.PitchOffset is < -350 or > 0 || state.PitchOffset % 5 != 0 || state.Filter is < 0 or > 256 || state.Consecutive < 0 || state.Delay > 15))
        {
            throw new InvalidDataException("FFVII world camera occlusion has corrupt native feedback state.");
        }
    }
}
