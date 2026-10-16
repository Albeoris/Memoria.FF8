using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState AdvanceRagnarokTransfer(WorldState state)
    {
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        NativeRagnarokTransferState transfer = runtime.Transfer ?? throw new InvalidOperationException("Ragnarok transfer has no actual admitted phase owner.");
        NativeRagnarokTransferStep sample = transfer.Advance();
        var objects = runtime.Objects.ToBuilder();
        int index = runtime.Allocation.RagnarokIndex;
        objects[index] = objects[index] with { Position = sample.Position };
        runtime = runtime with { Objects = objects.ToImmutable(), Transfer = sample.State, TransferCamera = sample.Camera, Motor = runtime.Motor with { Speed = transfer.Counter == 0 ? 0 : runtime.Motor.Speed, FlightHeight = unchecked((short)sample.Position.Y), CameraYaw = checked((ushort)WrapYaw(sample.Camera.Yaw)), CameraPitch = sample.Camera.Pitch }, Forest = runtime.Forest! with { CameraPitch = sample.Camera.Pitch } };
        state = state with { X = WrapCoordinate(sample.Position.X, 262144), Z = WrapCoordinate(-sample.Position.Z, 196608), NativeHeight = sample.Position.Y, CameraYaw = WrapYaw(sample.Camera.Yaw), AnimationCounter = sample.AnimationCounter, NativeRagnarokRuntime = runtime };
        if (sample.Completed)
        {
            if (transfer.Kind == NativeRagnarokTransferKind.Landing)
            {
                state = CompleteRagnarokLanding(state, sample.State, runtime.LandingPlacement ?? throw new InvalidDataException("Landing omitted its actual53E7A0 destination."), sample.Camera);
            }
            runtime = state.NativeRagnarokRuntime!;
            //554860 runs after54B460 and replaces only the nine projection words.
            runtime = runtime with { Transfer = null, LandingPlacement = null, TransferCamera = runtime.TransferCamera! with { ProjectionWords = transfer.TargetCamera.ProjectionWords, PerspectiveWords = transfer.TargetCamera.PerspectiveWords } };
            state = state with { NativeRagnarokRuntime = runtime };
        }
        return state;
    }

    private (uint Flags, int GroundHeight) RagnarokCurrentGround(WorldState state)
    {
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        (uint Flags, int GroundHeight)? result = null;
        foreach (NativeWorldGroundAlternative alternative in runtime.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint flags = polygon.GroundType | (uint)polygon.TextureFlags << 8 | (uint)polygon.VertexFlags << 16;
            var current = (Flags: flags, GroundHeight: (int)runtime.CurrentGroundHeight!.Value);
            if (result is not null && result.Value != current)
            {
                throw new NotSupportedException("Ragnarok transfer predicates disagree across actual retained ground alternatives.");
            }
            result = current;
        }
        return result ?? throw new InvalidDataException("Ragnarok has no current ground owner.");
    }

    private NativeWorldPathPosition? PrepareRagnarokLanding(WorldState cameraState)
    {
        NativeRagnarokWorldRuntimeState runtime = cameraState.NativeRagnarokRuntime!;
        (uint flags, int groundHeight) = RagnarokCurrentGround(cameraState);
        if (!NativeWorldTerrain.CanDisembark(flags, 50))
        {
            return null;
        }
        NativeWorldPathProjectionCertificate projection = RagnarokProjection(cameraState);
        bool trainsExcluded = WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection);
        int Contact(IReadOnlyList<WorldNativeGardenProbe> probes, int selected, int padding, bool forced)
        {
            return WorldRagnarokContactCertificate.Evaluate(assets, runtime, probes, projection, padding, trainsExcluded, selected, 50, forced);
        }
        NativeWorldPathPosition body = new(cameraState.X, cameraState.NativeHeight, -cameraState.Z);
        //The shared current53E7A0 evaluator is also used by Garden. This call
        //uses its real Ragnarok50 source, native foot0 destination and528 distance.
        WorldGardenPlacementResult foot = assets.ResolveNativeGardenPlacement(body, checked((short)cameraState.BodyYaw), 50, 0, 528, groundHeight, flags, runtime.GroundCache!, replacements, Contact);
        if (!foot.Accepted)
        {
            return null;
        }
        WorldGardenPlacementResult ship = assets.ResolveNativeGardenPlacement(body, checked((short)cameraState.BodyYaw), 50, 50, 0, groundHeight, flags, runtime.GroundCache!, replacements, Contact);
        return ship.Accepted ? foot.Position : null;
    }
}
