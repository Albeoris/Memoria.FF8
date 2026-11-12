using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private const byte ChocoboEntity = 49;

    private NativeGardenWorldAllocation ReadChocoboAllocation(WorldTransition arrival)
    {
        GameState state = session.State;
        return assets.ReadChocoboGardenAllocation(arrival, state.WorldEntryMemory ?? throw new NotSupportedException("Mounted Chocobo requires the retained world-entry bytes."), state.EventMemory, state.SavedWorld!, state.CharacterVitals ?? throw new NotSupportedException("Mounted Chocobo requires the saved character and Guardian Force owner."), state.PocketStation?.Flags ?? throw new NotSupportedException("Mounted Chocobo requires the saved PocketStation flags."));
    }

    private void InitializeChocobo(WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeGardenWorldAllocation allocation = ReadChocoboAllocation(arrival);
        _ = assets.GetFootModel(0);
        _ = assets.GetChocoboModel();
        if ((allocation.ChocoboFlags1536 & 3) == 1)
        {
            _ = assets.GetChocoboModel(companion: true);
        }
        if (session.State.World is null)
        {
            NativeSavedWorldPose pose = allocation.LoadPose;
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.RawZ, replacements, []) ?? throw new NotSupportedException("Mounted Chocobo entry has no original ground polygon.");
            NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Mounted Chocobo requires its retained world process.");
            NativeGardenCameraDefaults defaults = assets.ReadGardenCameraDefaults();
            short initialPitch = allocation.AnimationVariant == 0 ? defaults.Pitch : (short)-512;
            NativeWorldFootCameraState camera = new(pose.X, pose.RawZ, pose.Height, pose.X, pose.RawZ, pose.Height - 256, allocation.CameraYaw, initialPitch, -341, -7168) { ProjectionDistance = allocation.AnimationVariant == 0 ? 1024 : 640 };
            NativeGardenWorldRuntimeState objects = NativeGardenWorldRuntimeState.CreateFresh(session.State.SavedWorld!, allocation, CreateFreshGroundCache(ground.Identity), process, defaults, camera, unchecked((short)-(int)ground.Ground.Height), ReadInitialTerrainOcclusion());
            objects = objects with { BackgroundRails = CreateBackgroundRails(allocation.Flags265, camera), CameraPitch = initialPitch, Forest = objects.Forest with { CameraPitch = initialPitch } };
            byte groundType = ground.Ground.Triangle.GroundType;
            NativeChocoboWorldAnimation animation = NativeChocoboWorldAnimation.Create(pose.X, pose.Height, pose.RawZ, pose.Yaw, groundType <= 5);
            session.SetWorldState(new WorldState
            {
                X = pose.X,
                Z = pose.RawZ,
                NativeHeight = pose.Height,
                BodyYaw = pose.Yaw,
                CameraYaw = allocation.CameraYaw,
                CameraTargetX = pose.X,
                CameraTargetZ = pose.RawZ,
                CameraTargetNativeHeight = pose.Height - 256,
                AnimationId = animation.RiderAnimation,
                GroundType = groundType,
                ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, inputs.StoryByte),
                NativeChocoboRuntime = new(objects, animation, PreviousFootHorizontalInput(process, worldLoad: true)),
                EventUi = NativeWorldEventUi.Create()
            });
        }
        NativeChocoboWorldRuntimeState current = State.NativeChocoboRuntime ?? throw new InvalidDataException("Mounted Chocobo restoration requires its executed runtime.");
        current.Validate(State);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(allocation), JsonSerializer.SerializeToElement(current.Objects.Allocation)))
        {
            throw new InvalidDataException("Mounted Chocobo allocation differs from the original world packet and selected resources.");
        }
        assets.ValidateNativeGroundCache(current.GroundCache, replacements);
    }

    private WorldState StepChocobo(WorldState before, int movementSpeedPercent, float horizontal, float vertical, float cameraHorizontal, int priorGraphicsSpeed, ref NativeWorldSalaryState salary, out uint salaryMovementUnits, out int graphicsSpeed, bool mapOnly)
    {
        NativeChocoboWorldRuntimeState mounted = before.NativeChocoboRuntime!;
        NativeGardenWorldRuntimeState objects = mounted.Objects;
        NativeWorldForestState forest = objects.Forest;
        long frame = before.Frame + 1;
        int inputX = Math.Sign(horizontal);
        if (NativeWorldEntryControl.DelayAfterCallback(frame, 0) > 0)
        {
            horizontal = 0;
            vertical = 0;
            cameraHorizontal = 0;
        }
        (int componentX, int componentZ) = ReadFootMovementComponents(horizontal, vertical);
        // 557BBE selects the signed shift5 motor for type49.
        (int nativeX, int nativeZ) = Rotate((16 * componentX) >> 5, (16 * componentZ) >> 5, before.CameraYaw);
        graphicsSpeed = NativeWorldVectorMagnitude.Truncate(nativeX, 0, nativeZ);
        int moveX = ScalePlayerMovement(nativeX, movementSpeedPercent);
        int moveZ = ScalePlayerMovement(nativeZ, movementSpeedPercent);
        bool moving = nativeX != 0 || nativeZ != 0;
        int yaw = before.BodyYaw;
        bool dampCamera = !moving;
        if (moving)
        {
            int desired = WrapYaw(NativeRound(Math.Atan2(-nativeZ, nativeX) * NativeAtanScale) + 1024 + (objects.GroundYaw >> 1));
            int delta = YawDelta(desired, yaw);
            dampCamera = Math.Abs(delta) > 256;
            yaw = dampCamera ? WrapYaw(yaw + Math.Sign(delta) * 512) : desired;
        }
        int velocity = cameraHorizontal == 0 ? FollowCamera(before.CameraVelocity, YawDelta(yaw, before.CameraYaw), dampCamera) : Math.Clamp(before.CameraVelocity + Math.Sign(cameraHorizontal) * 8, -128, 128);
        int cameraYaw = WrapYaw(before.CameraYaw + (velocity >> 3));
        int targetX = (before.X + NearWrapped(before.CameraTargetX, before.X, 262144)) >> 1;
        int targetZ = (before.Z + NearWrapped(before.CameraTargetZ, before.Z, 196608)) >> 1;
        int targetHeight = (3 * before.CameraTargetNativeHeight + before.NativeHeight - 256) >> 2;
        byte previousGroundType = before.GroundType!.Value;
        NativeWorldProcess process = session.State.WorldProcess!;
        process = process with { CameraCooldown = forest.CameraCooldown };
        NativeForestCameraResult pitch = objects.AnimationVariant == 0 ? process.StepNormalFootCamera(forest.CameraPitch, previousGroundType, objects.PriorTerrainOcclusion!.Value, held8: moving) : new(forest.CameraPitch, process);
        NativeWorldFootCameraState camera = new(before.X, before.Z, before.NativeHeight, targetX, targetZ, targetHeight, cameraYaw, pitch.Pitch, -341, -7168) { ProjectionDistance = mounted.CameraTransition?.ProjectionDistance ?? objects.ProjectionCamera!.ProjectionDistance };
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        NativeWorldBackgroundRailState rails = StepBackgroundRails(objects.BackgroundRails!, camera, ChocoboEntity, mapOnly ? (byte)12 : (byte)0);
        WorldState after = before with { Frame = frame, BodyYaw = yaw, CameraYaw = cameraYaw, CameraVelocity = velocity, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight };
        objects = objects with { ProjectionCamera = camera, CameraPitch = pitch.Pitch, Forest = forest with { CameraPitch = pitch.Pitch, CameraCooldown = pitch.Process.CameraCooldown } };
        if (mapOnly)
        {
            salaryMovementUnits = (uint)objects.MovementMagnitude20409F4 / 8;
            objects = objects with { BackgroundRails = CompleteBackgroundRails(rails, before.X, before.Z, before.NativeHeight) };
            objects = PublishChocoboBodies(objects, mounted.Animation, before.X, before.NativeHeight, before.Z, yaw);
            return after with { NativeChocoboRuntime = mounted with { Objects = objects } };
        }

        NativeChocoboWorldAnimation animation = mounted.Animation;
        bool hasCompanion = (objects.Allocation.ChocoboFlags1536 & 3) == 1;
        NativeChocoboTrailPose companion = animation.CompanionPose;
        bool visible = previousGroundType > 5 && IsVisible(camera.Origin);
        bool companionVisible = hasCompanion && animation.CompanionPlaced && !companion.Forest && IsVisible(new(companion.X, companion.Height, -companion.Z));
        int riderLength = assets.GetFootModel(0).Animations[animation.RiderAnimation].Frames.Count * 16;
        int companionLength = companionVisible ? assets.GetChocoboModel(companion: true).Animations[animation.CompanionAnimation].Frames.Count * 16 : 0;
        animation = animation.AdvanceAnimation(moving, visible, visible, companionVisible, riderLength, companionLength, objects.Forest, assets.ForestTables.Random, out forest);
        // 550070 consumes the updated clips at the bodies and ground pointers retained from the previous callback.
        NativeWorldForestTables tables = assets.ForestTables;
        NativeWorldWaterStepResult mountStep = forest.EmitChocoboParticles(17, previousGroundType, animation.MountAnimation, animation.MountCounter, salary.MountWaterStepEdges, new(before.X, before.NativeHeight, -before.Z), checked((short)before.BodyYaw), priorGraphicsSpeed, tables.Random, tables.AllEffects);
        forest = mountStep.Forest;
        byte companionEdges = salary.CompanionWaterStepEdges;
        if (hasCompanion)
        {
            byte companionGroundType = animation.CompanionPlaced ? ReadChocoboCompanionGround(companion, mounted.GroundCache) : previousGroundType;
            NativeWorldWaterStepResult companionStep = forest.EmitChocoboParticles(18, companionGroundType, animation.CompanionAnimation, animation.CompanionCounter, companionEdges, new(companion.X, companion.Height, -companion.Z), checked((short)companion.Yaw), priorGraphicsSpeed, tables.Random, tables.AllEffects);
            forest = companionStep.Forest;
            companionEdges = companionStep.Edges!.Value;
        }
        salary = salary.WithChocoboWaterStepEdges(mountStep.Edges!.Value, companionEdges);
        forest = forest.AdvanceParticlesOnly(tables.AllEffects);
        int? previousInput = mounted.PreviousHorizontalInput ?? PreviousFootHorizontalInput(process);
        int? group = inputX != 0 && previousInput != inputX ? inputX > 0 ? 0 : 1 : null;
        WorldNativeGroundResult ground = assets.ResolveNativeGround(before.X, before.Z, moveX, moveZ, -objects.CurrentGroundHeight!.Value, group, mounted.GroundCache, replacements, ChocoboEntity, previousInput is null, footprintContact: (probes, padding) => WorldGardenContactCertificate.Evaluate(assets, objects.Allocation, objects.Objects, ChocoboEntity, ChocoboEntity, probes, projection, padding, rails: rails), currentBodyHeight: before.NativeHeight, rawSpeed: priorGraphicsSpeed);
        int x = ground.X;
        int z = ground.Z;
        byte groundType = ground.Center?.Ground.Triangle.GroundType ?? previousGroundType;
        short groundHeight = ground.Center is { } center ? unchecked((short)-(int)center.Ground.Height) : objects.CurrentGroundHeight.Value;
        int height = ground.GroundCommitted ? NativeWorldTerrain.ResolveHeight(ChocoboEntity, groundHeight, groundType, before.NativeHeight, rawSpeed: priorGraphicsSpeed).Height : before.NativeHeight;
        int acceptedSpeed = ground.PositionCommitted ? graphicsSpeed : 0;
        salaryMovementUnits = NativeWorldSalaryMovement.AcceptedVectorUnits(ground.PositionCommitted ? moveX : 0, 0, ground.PositionCommitted ? moveZ : 0);
        animation = animation.AdvanceTrail(x, height, z, yaw, groundType <= 5, acceptedSpeed, hasCompanion);
        WorldNativeTerrainOcclusionResult occlusion = assets.CertifyNativeTerrainCameraOcclusion(camera, objects.CurrentGroundHeight.Value, replacements, ChocoboEntity);
        objects = objects with
        {
            Forest = forest,
            BackgroundRails = CompleteBackgroundRails(rails, x, z, height),
            GroundCache = ground.Cache,
            GroundYaw = checked((short)ground.GroundYaw),
            CurrentGroundHeight = groundHeight,
            PriorTerrainOcclusion = occlusion.Occluded ?? throw new NotSupportedException(occlusion.Reason),
            MovementMagnitude20409F4 = acceptedSpeed,
            PrimaryContact = ground.PrimaryContact!.Value,
            BroadContact = ground.BroadContact!.Value
        };
        objects = PublishChocoboBodies(objects, animation, x, height, z, yaw);
        return after with
        {
            X = x,
            Z = z,
            NativeHeight = height,
            GroundType = groundType,
            AnimationId = animation.RiderAnimation,
            AnimationCounter = animation.RiderCounter,
            NativeChocoboRuntime = mounted with { Objects = objects, Animation = animation, PreviousHorizontalInput = inputX }
        };

        bool IsVisible(NativeWorldPathPosition position)
        {
            return (projection.ProjectNode(position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible;
        }
    }

    private byte ReadChocoboCompanionGround(NativeChocoboTrailPose pose, NativeWorldGroundCacheState cache)
    {
        byte? groundType = null;
        foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
        {
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.Z, replacements, alternative.Entries) ?? throw new NotSupportedException("Mounted Chocobo companion has no original ground polygon.");
            byte candidate = ground.Ground.Triangle.GroundType;
            if (groundType is byte previous && candidate != previous)
            {
                throw new NotSupportedException("Mounted Chocobo companion ground differs between retained native cache alternatives.");
            }
            groundType = candidate;
        }
        return groundType!.Value;
    }

    private static NativeGardenWorldRuntimeState PublishChocoboBodies(NativeGardenWorldRuntimeState objects, NativeChocoboWorldAnimation animation, int x, int height, int z, int yaw)
    {
        NativeWorldPathPosition body = new(x, height, -z);
        objects = objects.PublishBody(body, 0, checked((short)yaw), 0, gardenControlled: false);
        var allocation = objects.Objects.SetItem(1, objects.Objects[1] with { Position = body, RotationY = checked((short)yaw) });
        if (animation.CompanionPlaced)
        {
            NativeChocoboTrailPose pose = animation.CompanionPose;
            allocation = allocation.SetItem(2, allocation[2] with { Position = new(pose.X, pose.Height, -pose.Z), RotationY = checked((short)pose.Yaw) });
        }
        return objects with { Objects = allocation };
    }

    private WorldRenderFrame PrepareChocoboFrame(WorldState state)
    {
        NativeChocoboWorldRuntimeState mounted = state.NativeChocoboRuntime!;
        NativeChocoboWorldAnimation animation = mounted.Animation;
        WorldRenderFrame frame = PrepareGardenFrame(state with { NativeGardenRuntime = mounted.Objects });
        List<NativeWorldAnimatedModelDraw> models = [];
        bool visible = state.GroundType is > 5;
        if (visible)
        {
            models.Add(new NativeWorldAnimatedModelDraw(assets.GetChocoboModel(), frame.Position, frame.Heading, animation.MountAnimation, animation.MountCounter >> 4) { ObjectId = new(WorldRenderObjectKind.Allocation, 1) });
        }
        NativeChocoboTrailPose companion = animation.CompanionPose;
        if ((mounted.Objects.Allocation.ChocoboFlags1536 & 3) == 1 && animation.CompanionPlaced && !companion.Forest)
        {
            models.Add(new NativeWorldAnimatedModelDraw(assets.GetChocoboModel(companion: true), new Vector3(companion.X, -companion.Height, companion.Z), companion.Yaw * MathF.Tau / 4096, animation.CompanionAnimation, animation.CompanionCounter >> 4) { ObjectId = new(WorldRenderObjectKind.Allocation, 2) });
        }
        return frame with { AvatarVisible = visible, AvatarModel = assets.GetFootModel(0), AnimatedModels = models };
    }
}
