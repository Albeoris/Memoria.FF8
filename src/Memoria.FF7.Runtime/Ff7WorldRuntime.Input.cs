using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldTerrainReference(int ChunkX, int ChunkZ, int Triangle);
public sealed record Ff7WorldInputState(ushort Held = 0, ushort Pressed = 0, int Horizontal = 0, int Vertical = 0, bool Run = true, int PreviousX = 0, int PreviousY = 0, int PreviousZ = 0, int CollisionSide = 0, int CollisionAge = 0, int BridgeCooldown = 0, int TerrainScript = 0, Ff7WorldTerrainReference[]? TerrainCache = null, int? CameraHeadingTarget = null, int? FreeDirectionOffset = null, ushort PreviousControlHeld = 0, Ff7WorldBridgeState? Bridge = null, int CollisionRecovery = 2, int HeightSmoothVelocity = 0, int HeightSmoothRemaining = 0)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldTerrainReference? GroundTriangle { get; init; }
}

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldInputState input = new();
    private int movementSpeedPercent = 100;
    private Ff7WorldMath inputMath = null!;
    private IReadOnlyList<byte> collisionMasks = null!;
    private readonly Dictionary<(int Block, int Mesh), PcWorldMesh> collisionMeshes = [];
    private readonly Dictionary<int, (Ff7WorldCharacterDescriptor Descriptor, Ff7FieldModelResources Models)> leaderAnimations = [];
    private Ff7Installation inputInstallation = null!;
    private Ff7WorldVisibility visibility = null!;

    private void InitializePlayerInput(Ff7Installation installation)
    {
        Ff7WorldPresentationData presentation = Ff7PcExecutableData.LoadWorldPresentation(installation, Resources);
        cameraMath = new(presentation);
        inputMath = new(presentation);
        InitializeBridges(installation);
        visibility = new(presentation);
        inputInstallation = installation;
        collisionMasks = Ff7PcExecutableData.LoadWorldCollisionMasks(installation);
        int savedHeading = persistent[0x3F8] | (persistent[0x3F9] & 15) << 8;
        if (Resources.World == 2 && entryModule == 0 && entrySelector != 0)
        {
            savedHeading = 0;
        }
        camera = camera with { Heading = viewMode == 2 ? savedHeading : 0 };
        input = input with { CameraHeadingTarget = camera.Heading };
    }

    public void ConfigureMovementSpeed(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 300);
        movementSpeedPercent = percent;
    }

    public void SetButtons(ushort held, ushort pressed)
    {
        input = input with { Held = held, Pressed = pressed };
        HeldConfirm = (held & 32) != 0;
    }

    public void SetMovement(int horizontal, int vertical, bool run)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(horizontal, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(horizontal, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(vertical, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vertical, 1);
        input = input with { Horizontal = horizontal, Vertical = vertical, Run = run };
    }

    private void AdvancePlayerInput()
    {
        AdvancePlayerInputCore();
        AdvanceWorldCameraHeading();
    }

    private void AdvancePlayerInputCore(bool autoPlayPreview = false)
    {
        Ff7WorldEntity[] activeEntities = ActiveWorldEntities().ToArray();
        HashSet<int> movedModels = activeEntities.Where(entity => (entity.Flags & 1) != 0).Select(entity => entity.Model).ToHashSet();
        foreach (Ff7WorldEntity entity in activeEntities)
        {
            int? contact = entity.ContactModel;
            if (contact is int target && (movedModels.Contains(entity.Model) || movedModels.Contains(target)))
            {
                contact = null;
            }
            UpdateEntity(entity with { ContactModel = contact, Flags = entity.Flags & ~7, PreviousX = entity.X, PreviousY = entity.Y, PreviousZ = entity.Z });
        }
        if (controlledModel < 0)
        {
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        input = input with { PreviousX = player.X, PreviousY = player.Y, PreviousZ = player.Z };
        int difference = Math.Abs(player.Y - camera.GroundHeight);
        int ground = difference <= 200 ? difference > 50 ? camera.GroundHeight + Math.Sign(player.Y - camera.GroundHeight) * 50 : player.Y : (camera.GroundHeight * 7 + player.Y) >> 3;
        if (scriptCamera?.Mode != 2)
        {
            camera = camera with { GroundHeight = ground };
        }
        if (!movementEnabled)
        {
            if ((player.Terrain & 31) == 14)
            {
                int driftX = 0;
                int driftZ = 0;
                ApplyBridgeDrift(ref driftX, ref driftZ);
                UpdateEntity(ResolveWorldModelContact(player with { X = Wrap(player.X + driftX, 0x48000), Z = Wrap(player.Z + driftZ, 0x38000), Flags = driftX != 0 || driftZ != 0 ? player.Flags | 1 : player.Flags & ~1 }));
            }
            return;
        }
        if (Resources.World == 2 && player.Model == SubmarineModel)
        {
            RejectAutoPlayMovement(autoPlayPreview, "Underwater submarine motion requires its own native preview contract.");
            AdvanceUnderwaterSubmarineInput(player);
            return;
        }
        if ((player.Model == 3 && viewMode == 3 || player.Model == 5 && (player.Flags & 128) != 0) && vehicle is not { DismountPhase: 2 })
        {
            AdvanceHighwindInput(player, autoPlayPreview);
            return;
        }
        int delta = (player.Model switch { 3 => 240, YellowChocoboModel or 19 or 41 or 42 or 5 => 120, 6 => 90, _ => 60 }) * movementSpeedPercent / 100;
        ushort held = WorldControlHeld();
        ushort pressed = (ushort)(held & ~input.PreviousControlHeld);
        int horizontal = input.Horizontal;
        int vertical = input.Vertical;
        if (player.Model == 5 && (held & 0x40) != 0)
        {
            horizontal = 0;
            vertical = 0;
            held &= 0x0FFF;
        }
        if (player.Model == SubmarineModel && viewMode == 2 && (held & 0x20) != 0)
        {
            held = (ushort)((held & ~0x4000) | 0x1000);
            vertical = -1;
        }
        int dx = horizontal * delta;
        int dz = vertical * delta;
        int heading = horizontal < 0 ? -1024 : horizontal > 0 ? 1024 : -1;
        if (vertical < 0)
        {
            if (heading == -1)
            {
                heading = 2048;
            }
            else
            {
                dx = dx * 3 >> 2;
                dz = delta * -3 >> 2;
                heading += heading >> 1;
            }
        }
        else if (vertical > 0)
        {
            if (heading == -1)
            {
                heading = 0;
            }
            else
            {
                dx = dx * 3 >> 2;
                dz = delta * 3 >> 2;
                heading -= heading >> 1;
            }
        }
        ApplyWorldControlOptions(ref player, ref dx, ref dz, heading, held, vertical, autoPlayPreview);
        if (player.Model == SubmarineModel && (pressed & 0x40) != 0 && (player.Terrain & 31) == 3)
        {
            RejectAutoPlayMovement(autoPlayPreview, "Submarine module transfer is a native script boundary.");
            BeginSubmarineMapTransfer(2);
        }
        if (IsWorldChocobo(player.Model) && heading != -1)
        {
            player = player with { ScriptDirection = null };
        }
        if ((player.Terrain & 31) == 14)
        {
            ApplyBridgeDrift(ref dx, ref dz);
        }
        Ff7WorldEntity moved = player with { X = Wrap(player.X + dx, 0x48000), Z = Wrap(player.Z + dz, 0x38000), Flags = dx != 0 || dz != 0 ? player.Flags | 1 : player.Flags & ~1 };
        UpdateEntity(ResolveWorldModelContact(moved));
    }

    private IEnumerable<Ff7WorldEntity> NativeWorldCollisionSources()
    {
        return Machine.Capture().Models.Where(model => !model.Detached).Select(model => RequireEntity(model.Model));
    }

    private Ff7WorldEntity ResolveWorldModelContact(Ff7WorldEntity player)
    {
        if (player.Model == SubmarineModel && Resources.World == 0)
        {
            return player;
        }
        foreach (Ff7WorldEntity other in NativeWorldCollisionSources())
        {
            if (!WorldModelsCollide(player, other))
            {
                continue;
            }
            player = player with { ContactModel = other.Model };
            int currentDistance = Math.Abs(WrappedDifference(other.X - player.X, 0x48000)) + Math.Abs(WrappedDifference(other.Z - player.Z, 0x38000));
            int previousDistance = Math.Abs(WrappedDifference(other.X - player.PreviousX, 0x48000)) + Math.Abs(WrappedDifference(other.Z - player.PreviousZ, 0x38000));
            if (previousDistance > currentDistance)
            {
                player = player with { X = player.PreviousX, Y = (player.Flags & 4) != 0 ? player.Y : player.PreviousY ?? player.Y, Z = player.PreviousZ, Flags = player.Flags & ~1 };
            }
            break;
        }
        return player;
    }

    private static bool CanWorldModelsCollide(Ff7WorldEntity player, Ff7WorldEntity other)
    {
        return player.Model != other.Model && (player.Flags & 128) == 0 && (other.Flags & 128) == 0 && player.Model < 26 && other.Model < 26 && other.Model is not (21 or 22 or 23);
    }

    private bool WorldModelsCollide(Ff7WorldEntity player, Ff7WorldEntity other)
    {
        return WorldModelsCollideAt(player, other, player.X, player.Z);
    }

    private bool WorldModelsCollideAt(Ff7WorldEntity player, Ff7WorldEntity other, int playerX, int playerZ)
    {
        if (!CanWorldModelsCollide(player, other))
        {
            return false;
        }
        int dx = WrappedDifference(other.X - playerX, 0x48000);
        int dz = WrappedDifference(other.Z - playerZ, 0x38000);
        if (Math.Abs(dx) >= 1024 || Math.Abs(dz) >= 1024)
        {
            return false;
        }
        int x = (dx + 1024) >> 8;
        int z = (dz + 1024) >> 8;
        return ((collisionMasks[player.Model * 8 + z] >> x) & 1) != 0 || ((collisionMasks[other.Model * 8 + 7 - z] >> (7 - x)) & 1) != 0;
    }

    private void ResolvePlayerTerrain(bool autoPlayPreview = false, Action<Ff7AutoPlayWorldTerrainBoundary>? observeBoundary = null, bool surfaceDismountPreview = false)
    {
        if (mapTransfer is not null)
        {
            return;
        }
        if (controlledModel < 0)
        {
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        if (Resources.World == 2 && player.Model == SubmarineModel)
        {
            RejectAutoPlayMovement(autoPlayPreview, "Underwater submarine terrain requires its own native preview contract.");
            ResolveUnderwaterSubmarineTerrain(player);
            return;
        }
        if ((player.Model == 3 || player.Model == 5 && (player.Flags & 128) != 0) && vehicle is not { DismountPhase: 2 })
        {
            ResolveHighwindTerrain(player, autoPlayPreview);
            return;
        }
        if (vehicle is { DismountPhase: 2 })
        {
            RejectAutoPlayMovement(autoPlayPreview && !surfaceDismountPreview, "Vehicle dismount requires its transport-state preview contract.");
            player = OffsetDismountingVehicle(player);
        }
        if (chocobo is { DismountPhase: 2 })
        {
            RejectAutoPlayMovement(autoPlayPreview, "Chocobo dismount requires its transport-state preview contract.");
            Vector3 step = inputMath.TransformRotation(new(0, 0, 300), 0, player.Direction + player.TurnOffset + 1024, 0);
            player = OffsetWorldEntity(player with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ }, (int)step.X, (int)step.Z);
        }
        FootCollisionResult? selected = ResolveFootCollision(player);
        input = input with { BridgeCooldown = Math.Max(0, input.BridgeCooldown - 1) };
        if (selected is { } result)
        {
            int terrainType = result.Terrain & 31;
            int turn = (player.Flags & 1) != 0 ? (player.TurnOffset * 7 + result.Angle) >> 3 : (player.TurnOffset + result.Angle) >> 1;
            bool moved = result.X != input.PreviousX || result.Z != input.PreviousZ;
            int height = ResolveFootHeight(player, result, moved, terrainType);
            player = player with { X = result.X, Y = height, Z = result.Z, GroundHeight = result.Height, Terrain = result.Terrain, TerrainResolved = true, TurnOffset = turn, Flags = (player.Flags | 2) & ~1 | (moved ? 1 : 0) };
            if (result.Side != input.CollisionSide)
            {
                input = input with { CollisionSide = result.Side, CollisionAge = 0 };
            }
            input = input with { CollisionRecovery = 0, GroundTriangle = result.Triangle ?? input.GroundTriangle };
            RejectAutoPlayMovement(autoPlayPreview && chocoboPathControl is { InitializationRequested: true }, "Chocobo path initialization is a native execution boundary.");
            InitializeRequestedChocoboPath(result.Triangle ?? input.GroundTriangle, player);
        }
        else
        {
            player = player with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Flags = (player.Flags & ~1) | 2 };
            if ((player.Terrain & 31) == 14)
            {
                input = input with { BridgeCooldown = 5 };
            }
            if (input.CollisionRecovery == 2)
            {
                input = input with { CollisionRecovery = 1 };
            }
        }
        input = input with { CollisionAge = unchecked(input.CollisionAge + 1) };
        UpdateEntity(player);
        int script = player.Terrain >> 5 & 7;
        if ((player.Flags & (8 | 128)) == 0)
        {
            if (script >= 3 && input.TerrainScript != script)
            {
                if (autoPlayPreview)
                {
                    observeBoundary?.Invoke(new(player.X >> 13, player.Z >> 13, script - 3, script == 7, script != 7 && ReadAutoPlayMeshAnalysis(player.X >> 13, player.Z >> 13, script, 2048, true).IsInert));
                }
                else
                {
                    StartObservedTerrainScript(player.X >> 13, player.Z >> 13, script - 3);
                }
            }
            input = input with { TerrainScript = script >= 3 ? script : 0 };
            if (script == 7)
            {
                UpdateEntity(player with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Flags = player.Flags & ~1 });
            }
        }
        if (!autoPlayPreview)
        {
            AdvanceWorldModelPresentation();
        }
    }

    private int ResolveFootHeight(Ff7WorldEntity player, FootCollisionResult result, bool moved, int terrain)
    {
        int offset = WorldGroundOffset(terrain, player.Model);
        int y = (player.Flags & 128) != 0 ? player.Y : result.Height + offset;
        int velocity = input.HeightSmoothVelocity;
        int remaining = input.HeightSmoothRemaining;
        if (terrain is 1 or 25)
        {
            remaining = 30;
        }
        else if (terrain is 2 or 12)
        {
            remaining = 0;
        }
        if ((player.Flags & 128) == 0 && remaining != 0)
        {
            remaining -= moved ? 1 : 0;
            if (velocity != 0)
            {
                if ((y > input.PreviousY) ^ (velocity > 0))
                {
                    velocity = 0;
                    y = input.PreviousY;
                }
                else if (!moved)
                {
                    y = input.PreviousY;
                }
                else if (y > input.PreviousY ? y - input.PreviousY > velocity : y - input.PreviousY < velocity)
                {
                    y = input.PreviousY + velocity;
                    velocity <<= 1;
                }
                else
                {
                    velocity = 0;
                }
            }
            else if (Math.Abs(y - input.PreviousY) > 50)
            {
                velocity = Math.Sign(y - input.PreviousY);
                y = input.PreviousY;
            }
        }
        else
        {
            velocity = 0;
            remaining = 0;
        }
        input = input with { HeightSmoothVelocity = velocity, HeightSmoothRemaining = remaining };
        return y;
    }

    private void AdvanceWorldModelPresentation()
    {
        Ff7WorldEntity player = RequireEntity(controlledModel);
        HashSet<(int X, int Z)> visibleChunks = visibility.RequiredChunks(player.X, player.Z, camera.Heading).Select(chunk => (chunk.ChunkX, chunk.ChunkZ)).ToHashSet();
        foreach (Ff7WorldEntity original in ActiveWorldEntities().ToArray())
        {
            Ff7WorldEntity entity = original;
            if (entity.Model != controlledModel && visibleChunks.Contains((entity.X >> 13, entity.Z >> 13)))
            {
                (int Height, int Terrain)? terrain = QueryTerrain(entity.X, entity.Z, entity.Y, entity.Terrain, entity.Model, false, out _, true);
                entity = RequireEntity(entity.Model);
                if (terrain is { } found)
                {
                    int height = (entity.Flags & 128) == 0 ? found.Height + WorldGroundOffset(found.Terrain & 31, entity.Model) : entity.Y;
                    entity = entity with { Y = height, GroundHeight = found.Height, Terrain = found.Terrain, TerrainResolved = true, Flags = entity.Flags | 2 };
                }
            }
            if ((entity.Flags & 8) != 0)
            {
                entity = entity with { Flags = entity.Flags & ~2 };
            }
            if ((entity.Flags & 2) == 0)
            {
                UpdateEntity(entity);
                continue;
            }
            UpdateEntity(AdvanceWorldModelAnimation(entity));
        }
    }

    private Ff7WorldEntity AdvanceWorldModelAnimation(Ff7WorldEntity entity)
    {
        if (!leaderAnimations.TryGetValue(entity.Model, out var animationResources))
        {
            if (!Ff7PcExecutableData.TryLoadWorldCharacterDescriptor(inputInstallation, entity.Model, out Ff7WorldCharacterDescriptor? descriptor, Resources.World, entity.Model == 19 ? chocoboTint : submarineTint, PresentationArea))
            {
                return entity;
            }
            animationResources = (descriptor!, descriptor!.Load(Resources));
            leaderAnimations.Add(entity.Model, animationResources);
        }
        int dx = WrappedDifference(entity.X - entity.PreviousX, 0x48000);
        int dz = WrappedDifference(entity.Z - entity.PreviousZ, 0x38000);
        int animation = entity.Animation != 0 ? Math.Max(0, entity.Animation) : dx * dx + dz * dz >= 100 ? 1 : 0;
        bool moved = dx != 0 || dz != 0;
        if (entity.Animation == 0 && entity.Model == 6)
        {
            vehicle ??= new([]);
            if (entity.Model == controlledModel && moved && input.GroundTriangle is { } ground)
            {
                PcWorldMesh mesh = GetWorldCollisionMesh(ground.ChunkX, ground.ChunkZ);
                PcWorldTriangle triangle = mesh.Triangles[ground.Triangle];
                if (mesh.Normals.Count > Math.Max(triangle.A, Math.Max(triangle.B, triangle.C)))
                {
                    Vector3 normal = mesh.Normals[triangle.A] + mesh.Normals[triangle.B] + mesh.Normals[triangle.C];
                    vehicle = vehicle with { BuggyTiltX = (vehicle.BuggyTiltX * 3 - inputMath.Direction((int)normal.Z, -(int)normal.Y)) >> 2, BuggyTiltZ = (vehicle.BuggyTiltZ * 3 - inputMath.Direction((int)normal.X, -(int)normal.Y)) >> 2 };
                }
            }
            entity = entity with { TiltX = vehicle.BuggyTiltX, TiltZ = vehicle.BuggyTiltZ };
            entity = entity with { FrameDivisor = entity.Model == controlledModel ? 32 : 0 };
            if (moved)
            {
                vehicle ??= new([]);
                int prior = vehicle.BuggyAnimation;
                int terrain = entity.Terrain & 31;
                int next = prior switch { 1 when terrain == 11 => 2, 2 when entity.AnimationFrame > 5 => 3, 3 when terrain is not (4 or 11) => 4, 4 when entity.AnimationFrame > 5 => 1, >= 1 and <= 4 => prior, _ => 1 };
                if (next != prior)
                {
                    entity = entity with { AnimationFrame = 0, PresentationAnimationEpoch = entity.PresentationAnimationEpoch + 1 };
                }
                vehicle = vehicle with { BuggyAnimation = next };
                animation = next;
            }
            else
            {
                animation = 0;
            }
        }
        if (entity.Animation == 0 && entity.Model == SubmarineModel)
        {
            entity = entity with { FrameDivisor = moved ? 32 : 0 };
        }
        animation = Math.Min(animation, animationResources.Descriptor.Animations.Count - 1);
        PcFieldAnimation clip = animationResources.Models.Animations[animationResources.Descriptor.Animations[animation]];
        int frame = entity.AnimationFrame;
        if (frame >= clip.Frames.Count)
        {
            frame = (entity.Flags & 32) != 0 ? clip.Frames.Count - 1 : 0;
        }
        int divisor = entity.FrameDivisor >> 4;
        int remainder = entity.FrameRemainder;
        int nextFrame = frame;
        if (divisor != 0)
        {
            remainder += 2;
            nextFrame = unchecked((byte)(frame + remainder / divisor));
            remainder %= divisor;
        }
        int nextDivisor = entity.Model switch { 3 when (entity.Flags & 128) != 0 => 32, 3 when entity.FrameDivisor != 0 => unchecked((byte)(entity.FrameDivisor + 1)), 5 when entity.Model == controlledModel => 32, 5 when entity.FrameDivisor != 0 => unchecked((byte)(entity.FrameDivisor + 1)), _ => entity.FrameDivisor };
        return entity with { PoseAnimation = animation, PoseFrame = frame, AnimationFrame = nextFrame, FrameRemainder = remainder, FrameDivisor = nextDivisor };
    }

    private PcWorldMesh GetWorldCollisionMesh(int chunkX, int chunkZ)
    {
        int block = ResolveCollisionBlock(chunkX, chunkZ, WorldMapState);
        int index = (chunkZ & 3) * 4 + (chunkX & 3);
        if (!collisionMeshes.TryGetValue((block, index), out PcWorldMesh? mesh))
        {
            mesh = Resources.Map.ReadMesh(block, index);
            collisionMeshes.Add((block, index), mesh);
        }
        return BridgeCollisionMesh(chunkX, chunkZ, mesh);
    }

    private int ResolveCollisionBlock(int chunkX, int chunkZ, int mapState)
    {
        return Resources.World switch { 2 => Ff7WorldVisibility.ResolveUnderwaterBlock(chunkX, chunkZ), 3 => Ff7WorldVisibility.ResolveSnowBlock(chunkX, chunkZ), _ => Ff7WorldVisibility.ResolveBlockIndex((chunkZ >> 2) * 9 + (chunkX >> 2), mapState) };
    }

    private uint ReadNativeTerrainMask(int previousTerrain, int model, bool dismount, int queryControlledModel, int queryDismountPhase)
    {
        return model switch
        {
            SubmarineModel when queryDismountPhase == 2 => 0x21B6F83u,
            SubmarineModel => SubmarineTerrainMask,
            3 when queryDismountPhase == 2 => 0x021B6F83u,
            5 when entities.TryGetValue(model, out Ff7WorldEntity? aircraft) && (aircraft.Flags & 128) != 0 => queryDismountPhase == 2 ? 0x70u : uint.MaxValue,
            5 => queryDismountPhase == 2 ? 0x20800u : 0x70u,
            6 => queryDismountPhase == 2 ? 0x21B6F83u : 0x331B6F13u,
            8 => 0x04040008u,
            100 => 1u << 7,
            _ when model is 0 or 1 or 2 or YellowChocoboModel && model == queryControlledModel && (previousTerrain & 31) is 13 or 14 => 0x20006000u,
            YellowChocoboModel => YellowChocoboTerrainMask,
            19 => dismount ? YellowChocoboTerrainMask : ChocoboTerrainMask(chocoboTint),
            0 or 1 or 2 => 0x721B6F83u,
            _ => uint.MaxValue
        };
    }

    private (int Height, int Terrain)? QueryTerrain(int x, int z, int previousHeight, int previousTerrain, int model, bool dismount = false)
    {
        return QueryTerrain(x, z, previousHeight, previousTerrain, model, dismount, out _);
    }

    private (int Height, int Terrain)? QueryTerrain(int x, int z, int previousHeight, int previousTerrain, int model, bool dismount, out Ff7WorldTerrainReference? reference, bool entityCache = false, int? adviceControlledModel = null, int? adviceDismountPhase = null)
    {
        reference = null;
        int chunkX = x >> 13;
        int chunkZ = z >> 13;
        PcWorldMesh mesh = GetWorldCollisionMesh(chunkX, chunkZ);
        int localX = x & 8191;
        int localZ = z & 8191;
        int selected = -1;
        int height = 0;
        bool cachedContains = false;
        Ff7WorldTerrainReference[]? cache = entityCache ? RequireEntity(model).TerrainCache : input.TerrainCache;
        int queryControlledModel = adviceControlledModel ?? controlledModel;
        int queryDismountPhase = adviceDismountPhase ?? vehicle?.DismountPhase ?? 0;
        uint mask = ReadNativeTerrainMask(previousTerrain, model, dismount, queryControlledModel, queryDismountPhase);
        bool Allowed(PcWorldTriangle face) => (mask & (1u << face.Terrain)) != 0 && (!(dismount || queryDismountPhase == 2 && model is 3 or 6 or SubmarineModel) || (face.TerrainAndScript & 0xE0) != 0xE0);
        foreach (Ff7WorldTerrainReference cached in cache ?? [])
        {
            if (cached.ChunkX == chunkX && cached.ChunkZ == chunkZ && TryTriangleHeight(mesh, cached.Triangle, localX, localZ, out height))
            {
                cachedContains = true;
                if (Allowed(mesh.Triangles[cached.Triangle]))
                {
                    selected = cached.Triangle;
                    break;
                }
            }
        }
        if (selected < 0 && cachedContains)
        {
            return null;
        }
        if (selected < 0)
        {
            int best = int.MaxValue;
            for (int triangle = 0; triangle < mesh.Triangles.Count; triangle++)
            {
                if (TryTriangleHeight(mesh, triangle, localX, localZ, out int candidateHeight))
                {
                    int difference = model is 3 or 5 || Resources.World == 2 ? candidateHeight : Math.Abs(candidateHeight - previousHeight);
                    if (difference < best)
                    {
                        selected = triangle;
                        height = candidateHeight;
                        best = difference;
                    }
                }
            }
            if (selected >= 0)
            {
                Ff7WorldTerrainReference[] updated = new[] { new Ff7WorldTerrainReference(chunkX, chunkZ, selected) }.Concat(cache ?? []).Take(6).ToArray();
                if (entityCache)
                {
                    UpdateEntity(RequireEntity(model) with { TerrainCache = updated });
                }
                else
                {
                    input = input with { TerrainCache = updated };
                }
            }
        }
        if (selected < 0)
        {
            return null;
        }
        PcWorldTriangle face = mesh.Triangles[selected];
        if (!Allowed(face))
        {
            return null;
        }
        reference = new(chunkX, chunkZ, selected);
        return (height, face.TerrainAndScript | (face.PackedIds & 0xFF00));
    }

    internal static bool TryTriangleHeight(PcWorldMesh mesh, int triangle, int x, int z, out int height)
    {
        PcWorldTriangle face = mesh.Triangles[triangle];
        Vector3 a = mesh.Vertices[face.A];
        Vector3 b = mesh.Vertices[face.B];
        Vector3 c = mesh.Vertices[face.C];
        static int NativeRound(float value) => checked((int)MathF.Round(value, MidpointRounding.ToEven));
        static float Edge(Vector3 first, Vector3 second, int x, int z) => (float)((double)(second.Z - first.Z) * (x - first.X) - (double)(second.X - first.X) * (z - first.Z));
        height = 0;
        if (NativeRound(Edge(a, b, x, z)) > 0 || NativeRound(Edge(b, c, x, z)) > 0 || NativeRound(Edge(c, a, x, z)) > 0)
        {
            return false;
        }
        Vector3 first = b - a;
        Vector3 second = c - a;
        Vector3 normal = new((float)((double)first.Y * second.Z - (double)first.Z * second.Y), (float)((double)first.Z * second.X - (double)first.X * second.Z), (float)((double)first.X * second.Y - (double)first.Y * second.X));
        normal.Y *= 1f / 256;
        float result;
        if (NativeRound(normal.Y) == 0)
        {
            float sum = a.Y + b.Y + c.Y;
            result = sum * (1f / 3);
        }
        else
        {
            float sum = normal.Y * a.Y;
            normal.X *= 1f / 256;
            float alongX = (a.X - x) * normal.X;
            normal.Z *= 1f / 256;
            float alongZ = (a.Z - z) * normal.Z;
            sum = (float)((double)sum + alongX + alongZ);
            result = sum / normal.Y;
        }
        height = NativeRound(result);
        return true;
    }

    private static int Wrap(int value, int range) => (value % range + range) % range;

    private void ValidatePlayerInput(Ff7WorldInputState? state, int restoredMapState)
    {
        if (state is null)
        {
            return;
        }
        if (state.Horizontal is < -1 or > 1 || state.Vertical is < -1 or > 1 || state.CollisionSide is < 0 or > 1 || state.CollisionRecovery is < 0 or > 2 || state.BridgeCooldown is < 0 or > 65535 || state.TerrainScript is < 0 or > 7 || state.TerrainCache is { Length: > 6 } || state.CameraHeadingTarget is < 0 or > 4095 || state.HeightSmoothRemaining is < 0 or > 30 || state.HeightSmoothVelocity is < -131072 or > 131072)
        {
            throw new InvalidDataException("FFVII world input or terrain cache state is corrupt.");
        }
        ValidateBridgeState(state.Bridge);
        foreach (Ff7WorldTerrainReference reference in state.TerrainCache ?? [])
        {
            if (reference is null || reference.ChunkX is < 0 or >= 36 || reference.ChunkZ is < 0 or >= 28 || reference.Triangle < 0)
            {
                throw new InvalidDataException("FFVII world terrain cache reference is corrupt.");
            }
            int block = ResolveCollisionBlock(reference.ChunkX, reference.ChunkZ, restoredMapState);
            int mesh = (reference.ChunkZ & 3) * 4 + (reference.ChunkX & 3);
            if (reference.Triangle >= Resources.Map.ReadMesh(block, mesh).Triangles.Count)
            {
                throw new InvalidDataException("FFVII world terrain cache refers to an absent native triangle.");
            }
        }
        if (state.GroundTriangle is { } ground)
        {
            if (ground.ChunkX is < 0 or >= 36 || ground.ChunkZ is < 0 or >= 28 || ground.Triangle < 0 || ground.Triangle >= Resources.Map.ReadMesh(ResolveCollisionBlock(ground.ChunkX, ground.ChunkZ, restoredMapState), (ground.ChunkZ & 3) * 4 + (ground.ChunkX & 3)).Triangles.Count)
            {
                throw new InvalidDataException("FFVII world ground-normal owner refers to an absent native triangle.");
            }
        }
    }

    private static int WrappedDifference(int difference, int range)
    {
        return difference < -range / 2 ? difference + range : difference >= range / 2 ? difference - range : difference;
    }

    /// <summary>Samples a world coordinate across the same seam used by the FFVII movement process.</summary>
    public static float InterpolateWorldCoordinate(int previous, int current, int extent, float fraction)
    {
        float coordinate = previous + WrappedDifference(current - previous, extent) * fraction;
        return coordinate < 0 ? coordinate + extent : coordinate >= extent ? coordinate - extent : coordinate;
    }
}
