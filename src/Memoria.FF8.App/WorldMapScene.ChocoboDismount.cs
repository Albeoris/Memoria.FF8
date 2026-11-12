using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeChocoboDismountState? PrepareChocoboDismount(WorldState state, NativeGardenWorldRuntimeState objects, NativeWorldPathProjectionCertificate projection, NativeWorldSalaryState salary)
    {
        if (salary.EventUi?.ScriptMode != 0 || state.NativeChocoboRuntime!.CameraTransition is { Flags: not 0 } ||
            NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0 ||
            salary.InputHistory is not { PreviousButtons: { } previous } input || (input.CurrentButtons & ~previous & 0x42) != 0x40)
        {
            return null;
        }

        NativeWorldPathPosition position = new(state.X, state.NativeHeight, -state.Z);
        short yaw = checked((short)state.BodyYaw);
        byte entity = checked((byte)objects.Objects[objects.Allocation.FootObjectIndex].EntityType);
        WorldGardenPlacementResult placement = assets.ResolveNativeGardenPlacement(position, yaw, ChocoboEntity, entity, 0, objects.CurrentGroundHeight!.Value, GardenFootCurrentPolygonFlags(objects.GroundCache), objects.GroundCache, replacements, Contact);
        if (placement.Position is not { } target)
        {
            return null;
        }

        //53EE80 checks the terrain under a separate 200-unit side probe.
        (int dx, int dz) = NativeGardenTransform.Offset(200, unchecked((short)(yaw - 1024)));
        NativeWorldPathPosition side = new(target.X + dx, target.Y, target.Z + dz);
        if (LookupChocoboGround(side, objects.GroundCache) is not { } ground || (ground.Ground.Triangle.VertexFlags & 1) == 0)
        {
            return null;
        }
        target = new(WrapCoordinate(target.X, 262144), target.Y, -WrapCoordinate(-target.Z, 196608));
        return new NativeChocoboDismountState(target, state.Frame, 0, objects.GroundCache.Alternatives[0].CurrentGround);

        int Contact(IReadOnlyList<WorldNativeGardenProbe> probes, int selected, int padding, bool forceCircle)
        {
            return WorldGardenContactCertificate.Evaluate(assets, objects.Allocation, objects.Objects, ChocoboEntity, selected, probes, projection, padding, forceCircle, rails: objects.BackgroundRails);
        }
    }

    private WorldGroundSelection? LookupChocoboGround(NativeWorldPathPosition position, NativeWorldGroundCacheState cache)
    {
        WorldGroundSelection? result = null;
        bool sampled = false;
        foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
        {
            WorldGroundSelection? selection = assets.LookupNativeGround(position.X, -position.Z, replacements, alternative.Entries);
            if (sampled && result?.Identity != selection?.Identity)
            {
                throw new NotSupportedException("Chocobo dismount terrain differs across retained cache alternatives.");
            }
            result = selection;
            sampled = true;
        }
        return result;
    }

    private (NativeGardenWorldRuntimeState Objects, NativeChocoboDismountState Transfer) AdvanceChocoboEscape(WorldState state, NativeGardenWorldRuntimeState objects, NativeChocoboDismountState transfer, NativeWorldPathProjectionCertificate projection, int trailWriter, int trailReader)
    {
        NativeGardenWorldObject mount = objects.Objects[1];
        switch (transfer.Phase)
        {
            case NativeChocoboEscapePhase.Initialize:
                NativeWorldPathPosition rider = new(state.X, state.NativeHeight, -state.Z);
                NativeChocoboEscapeRoute route = NativeChocoboEscapeRoute.Start(rider, checked((short)state.BodyYaw), objects.GroundCache.Alternatives[0].CurrentGround);
                transfer = transfer with { Phase = NativeChocoboEscapePhase.Search, Route = route };
                break;
            case NativeChocoboEscapePhase.Search:
                if (transfer.AnimationProgress < 400)
                {
                    route = assets.AdvanceNativeChocoboEscapeRoute(transfer.Route!, objects.GroundCache, replacements, IsBlocked);
                    transfer = transfer with { Route = route };
                    if (!route.Finished)
                    {
                        break;
                    }
                }
                transfer = transfer with { Phase = NativeChocoboEscapePhase.Wait, CompanionStopIndex = trailWriter };
                break;
            case NativeChocoboEscapePhase.Wait:
                if (transfer.AnimationProgress >= 400)
                {
                    transfer = transfer with { Phase = NativeChocoboEscapePhase.Run };
                }
                break;
            case NativeChocoboEscapePhase.Run:
                if (transfer.CompanionStopIndex == trailReader)
                {
                    transfer = transfer with { CompanionStopIndex = -1 };
                }
                if (transfer.Waypoint >= transfer.Route!.BestPath.Length - 1 ||
                    assets.ResolveNativeGroundIdentity(transfer.MountGround, replacements).GroundType <= 5 || !IsMountVisible())
                {
                    transfer = transfer with { Phase = NativeChocoboEscapePhase.Finish };
                    break;
                }
                NativeWorldPathPosition target = transfer.Route.BestPath[transfer.Waypoint].Position;
                short dx = unchecked((short)(target.X - mount.Position.X));
                short dz = unchecked((short)(target.Z - mount.Position.Z));
                short yaw = unchecked((short)NativePrecision53.RoundInteger(Math.Atan2(dx, dz) * NativeAtanScale));
                (int x, int z) = NativeGardenTransform.Offset(48, yaw);
                NativeWorldPathPosition next = new(WrapCoordinate(mount.Position.X + x, 262144), mount.Position.Y, -WrapCoordinate(-mount.Position.Z - z, 196608));
                WorldGroundSelection ground = LookupChocoboGround(next, objects.GroundCache) ?? throw new NotSupportedException("The native Chocobo escape route lost its ground polygon.");
                next = next with { Y = -(int)ground.Ground.Height + (ground.Ground.Triangle.GroundType <= 5 ? 80 : 0) };
                objects = objects with { Objects = objects.Objects.SetItem(1, mount with { Position = next, RotationY = yaw }) };
                transfer = transfer with { MountGround = ground.Identity, Waypoint = transfer.Waypoint + (dx + dz < 200 ? 1 : 0) };
                break;
        }
        return (objects, transfer);

        bool IsMountVisible()
        {
            return (projection.ProjectNode(mount.Position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible;
        }

        bool IsBlocked(NativeWorldPathPosition point)
        {
            // NG-050: The initial route contact has no initialized shape yaw. Use the retained mount yaw.
            IReadOnlyList<WorldNativeGardenProbe> probes = assets.CreateNativeGardenShapeFootprint(point, ChocoboEntity, mount.RotationY);
            return WorldGardenContactCertificate.Evaluate(assets, objects.Allocation, objects.Objects, ChocoboEntity, ChocoboEntity, probes, projection, rails: objects.BackgroundRails) >= 0;
        }
    }

    private NativeChocoboWorldAnimation AdvanceEscapeTrail(NativeChocoboWorldAnimation animation, NativeGardenWorldRuntimeState objects, NativeChocoboDismountState transfer, int acceptedSpeed, bool companionAllocated)
    {
        NativeGardenWorldObject mount = objects.Objects[1];
        bool forest = assets.ResolveNativeGroundIdentity(transfer.MountGround, replacements).GroundType <= 5;
        return animation.AdvanceTrail(mount.Position.X, mount.Position.Y, -mount.Position.Z, WrapYaw(mount.RotationY), forest, acceptedSpeed, companionAllocated, transfer.Phase == NativeChocoboEscapePhase.Run);
    }

    private NativeChocoboWorldAnimation AdvanceDismountAnimation(NativeChocoboWorldAnimation animation, NativeChocoboDismountState transfer, bool mounted)
    {
        var mountClips = assets.GetChocoboModel().Animations;
        int mountClipLength = mountClips[animation.MountAnimation].Frames.Count * 16;
        int mountAnimation = !mounted || animation.MountAnimation == 1 && animation.RiderAnimation == 4 || animation.MountAnimation == 3 && transfer.AnimationProgress >= mountClipLength ? 1 : 3;
        int mountCounter = animation.MountAnimation != mountAnimation ? 0 : mountAnimation == 3 ? transfer.AnimationProgress : (animation.MountCounter + 16) % mountClipLength;
        return animation with
        {
            RiderAnimation = 4,
            RiderCounter = checked((ushort)transfer.AnimationProgress),
            MountAnimation = mountAnimation,
            MountCounter = checked((ushort)mountCounter)
        };
    }

    private NativeChocoboWorldAnimation AnimateEscapingCompanion(NativeChocoboWorldAnimation animation, NativeGardenWorldRuntimeState objects, NativeChocoboDismountState transfer, NativeWorldPathProjectionCertificate projection, out NativeWorldForestState forest)
    {
        forest = objects.Forest;
        if (!ShowEscapingCompanion(objects, animation, transfer))
        {
            return animation;
        }
        NativeChocoboTrailPose pose = animation.CompanionPose;
        NativeWorldPathNodeProjection projected = projection.ProjectNode(new(pose.X, pose.Height, -pose.Z)) ?? throw new NotSupportedException(projection.UnsupportedReason);
        int clipLength = assets.GetChocoboModel(companion: true).Animations[animation.CompanionAnimation].Frames.Count * 16;
        return animation.AdvanceCompanionAnimation(projected.Visible, clipLength, forest, assets.ForestTables.Random, out forest);
    }

    private static bool ShowEscapingCompanion(NativeGardenWorldRuntimeState objects, NativeChocoboWorldAnimation animation, NativeChocoboDismountState transfer)
    {
        return (objects.Allocation.ChocoboFlags1536 & 3) == 1 && animation.CompanionPlaced && !animation.CompanionPose.Forest && transfer.Phase != NativeChocoboEscapePhase.Finish && transfer.CompanionStopIndex != -1;
    }

    private WorldState CompleteChocoboDismountCallback(WorldState state, ref NativeWorldSalaryState salary)
    {
        NativeChocoboDismountState transfer = state.ChocoboDismount!;
        transfer = transfer with { Counter = transfer.Counter + 1 };
        if (transfer.Counter < NativeChocoboDismountState.CallbackCount)
        {
            salary = salary.WithForcedAnimation(transfer.Counter == 1 ? 4 : -1);
            return state with { ChocoboDismount = transfer };
        }

        NativeChocoboWorldRuntimeState chocobo = state.NativeChocoboRuntime!;
        NativeGardenWorldRuntimeState objects = chocobo.Objects;
        NativeWorldPathPosition target = transfer.Destination;
        NativeWorldFootCameraState camera = objects.ProjectionCamera!;
        NativeWorldGroundCacheState cache = objects.GroundCache with { Alternatives = objects.GroundCache.Alternatives.Select(alternative => alternative with { Entries = [] }).ToArray() };
        NativeFootWorldRuntimeState foot = new()
        {
            ProofVersion = NativeFootWorldRuntimeState.CurrentProofVersion,
            Rule = NativeFootWorldRuntimeState.ChocoboDismountRule,
            EntityType = checked((byte)objects.Objects[objects.Allocation.FootObjectIndex].EntityType),
            AnimationVariant = objects.AnimationVariant,
            MapDisplayMode = objects.MapDisplayMode,
            CameraOffsetY = camera.OffsetY,
            CameraOffsetZ = camera.OffsetZ,
            CameraTransition = chocobo.CameraTransition,
            ReloadSourceWorldFrame = null,
            ChocoboDismountStartFrame = transfer.StartFrame,
            GroundCache = cache,
            GroundYaw = objects.GroundYaw,
            PreviousHorizontalInput = chocobo.PreviousHorizontalInput,
            MovementMagnitude20409F4 = objects.MovementMagnitude20409F4,
            Forest = objects.Forest,
            PriorTerrainOcclusion = objects.PriorTerrainOcclusion
        };
        objects = (objects with { GroundCache = cache }).PublishBody(target, 0, checked((short)state.BodyYaw), 0, gardenControlled: false);
        salary = salary.WithWorldScriptMode(0).WithForcedAnimation(0);
        return state with
        {
            X = target.X,
            Z = -target.Z,
            NativeHeight = target.Y,
            AnimationId = 0,
            AnimationCounter = 0,
            ChocoboDismount = transfer.Phase == NativeChocoboEscapePhase.Finish ? null : transfer with { ParkedAnimation = chocobo.Animation },
            NativeChocoboRuntime = null,
            NativeFootRuntime = foot,
            NativeGardenRuntime = objects
        };
    }

    private WorldState AdvanceParkedChocobo(WorldState state, int priorGraphicsSpeed, ref NativeWorldSalaryState salary)
    {
        if (state.ChocoboDismount is not { ParkedAnimation: { } animation } transfer)
        {
            return state;
        }
        NativeGardenWorldRuntimeState objects = state.NativeGardenRuntime ?? state.NativeCarRuntime!.GardenObjects!;
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(objects.ProjectionCamera!);
        (objects, transfer) = AdvanceChocoboEscape(state, objects, transfer, projection, animation.TrailWriteIndex, animation.CompanionTrailIndex);
        bool companionAllocated = (objects.Allocation.ChocoboFlags1536 & 3) == 1;
        if (state.EventUi?.ScriptMode != 12)
        {
            animation = AdvanceDismountAnimation(animation, transfer, mounted: false);
            animation = AnimateEscapingCompanion(animation, objects, transfer, projection, out NativeWorldForestState forest);
            if (transfer.Phase == NativeChocoboEscapePhase.Run)
            {
                forest = EmitChocoboParticles(objects, animation, transfer, state.GroundType!.Value, priorGraphicsSpeed, forest, ref salary);
            }
            objects = objects with { Forest = forest };
            if (state.NativeFootRuntime is { } foot)
            {
                state = state with { NativeFootRuntime = foot with { Forest = forest } };
            }
            if (state.NativeCarRuntime is { } car)
            {
                state = state with { NativeCarRuntime = car with { Forest = forest } };
            }
        }
        animation = AdvanceEscapeTrail(animation, objects, transfer, objects.MovementMagnitude20409F4, companionAllocated);
        objects = PublishChocoboCompanion(objects, animation);
        state = state.NativeGardenRuntime is not null ? state with { NativeGardenRuntime = objects } : state with { NativeCarRuntime = state.NativeCarRuntime! with { GardenObjects = objects } };
        return state with { ChocoboDismount = transfer.Phase == NativeChocoboEscapePhase.Finish ? null : transfer with { ParkedAnimation = animation } };
    }

    private void ValidateChocoboEscapeGround(NativeChocoboDismountState? transfer)
    {
        if (transfer is not null)
        {
            _ = assets.ResolveNativeGroundIdentity(transfer.MountGround, replacements);
            if (transfer.Route is { } route)
            {
                foreach (NativeChocoboEscapeWaypoint waypoint in route.SearchPath.Concat(route.BestPath))
                {
                    _ = assets.ResolveNativeGroundIdentity(waypoint.Ground, replacements);
                }
            }
        }
    }

    private WorldRenderFrame AddDismountModels(WorldRenderFrame frame, NativeGardenWorldRuntimeState objects, NativeChocoboWorldAnimation animation, NativeChocoboDismountState transfer)
    {
        List<NativeWorldAnimatedModelDraw> models = [];
        NativeGardenWorldObject mount = objects.Objects[1];
        if (transfer.Phase != NativeChocoboEscapePhase.Finish && assets.ResolveNativeGroundIdentity(transfer.MountGround, replacements).GroundType > 5)
        {
            models.Add(new(assets.GetChocoboModel(), new Vector3(mount.Position.X, -mount.Position.Y, -mount.Position.Z), mount.RotationY * MathF.Tau / 4096, animation.MountAnimation, animation.MountCounter >> 4) { ObjectId = new(WorldRenderObjectKind.Allocation, 1) });
        }
        NativeChocoboTrailPose companion = animation.CompanionPose;
        if (ShowEscapingCompanion(objects, animation, transfer))
        {
            models.Add(new(assets.GetChocoboModel(companion: true), new Vector3(companion.X, -companion.Height, companion.Z), companion.Yaw * MathF.Tau / 4096, animation.CompanionAnimation, animation.CompanionCounter >> 4) { ObjectId = new(WorldRenderObjectKind.Allocation, 2) });
        }
        return frame with { AnimatedModels = models };
    }
}
