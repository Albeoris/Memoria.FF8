using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldTerrainReference(int ChunkX, int ChunkZ, int Triangle);
public sealed record Ff7WorldInputState(ushort Held = 0, ushort Pressed = 0, int Horizontal = 0, int Vertical = 0, bool Run = true, int PreviousX = 0, int PreviousY = 0, int PreviousZ = 0, int CollisionSide = 0, int CollisionAge = 0, int BridgeCooldown = 0, int TerrainScript = 0, Ff7WorldTerrainReference[]? TerrainCache = null, int? CameraHeadingTarget = null, int? FreeDirectionOffset = null, ushort PreviousControlHeld = 0);

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
        inputMath = new(presentation);
        visibility = new(presentation);
        inputInstallation = installation;
        collisionMasks = Ff7PcExecutableData.LoadWorldCollisionMasks(installation);
        int savedHeading = persistent[0x3F8] | (persistent[0x3F9] & 15) << 8;
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

    private void AdvancePlayerInputCore()
    {
        foreach (Ff7WorldEntity entity in entities.Values.ToArray())
        {
            UpdateEntity(entity with { Flags = entity.Flags & ~7, PreviousX = entity.X, PreviousZ = entity.Z });
        }
        if (controlledModel < 0)
        {
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        input = input with { PreviousX = player.X, PreviousY = player.Y, PreviousZ = player.Z };
        int difference = Math.Abs(player.Y - camera.GroundHeight);
        int ground = difference <= 200 ? difference > 50 ? camera.GroundHeight + Math.Sign(player.Y - camera.GroundHeight) * 50 : player.Y : (camera.GroundHeight * 7 + player.Y) >> 3;
        camera = camera with { GroundHeight = ground };
        if (!movementEnabled || dialogue is { Completed: false })
        {
            return;
        }
        if (player.Model is not (0 or 1 or 2))
        {
            throw new NotSupportedException("FFVII world physical movement requires the original foot model and fixed camera control contract.");
        }
        int delta = 60 * movementSpeedPercent / 100;
        int dx = input.Horizontal * delta;
        int dz = input.Vertical * delta;
        int heading = input.Horizontal < 0 ? -1024 : input.Horizontal > 0 ? 1024 : -1;
        if (input.Vertical < 0)
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
        else if (input.Vertical > 0)
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
        ApplyWorldControlOptions(ref player, ref dx, ref dz, heading);
        Ff7WorldEntity moved = player with { X = Wrap(player.X + dx, 0x48000), Z = Wrap(player.Z + dz, 0x38000), Flags = dx != 0 || dz != 0 ? player.Flags | 1 : player.Flags & ~1 };
        if (entities.Values.Any(other => WorldModelsCollide(moved, other)))
        {
            moved = moved with { X = player.X, Y = player.Y, Z = player.Z, Flags = moved.Flags & ~1 };
        }
        UpdateEntity(moved);
    }

    private bool WorldModelsCollide(Ff7WorldEntity player, Ff7WorldEntity other)
    {
        if (player.Model == other.Model || (player.Flags & 128) != 0 || (other.Flags & 128) != 0 || player.Model >= 26 || other.Model >= 26 || other.Model is 21 or 22 or 23)
        {
            return false;
        }
        int dx = WrappedDifference(other.X - player.X, 0x48000);
        int dz = WrappedDifference(other.Z - player.Z, 0x38000);
        if (Math.Abs(dx) >= 1024 || Math.Abs(dz) >= 1024)
        {
            return false;
        }
        int x = (dx + 1024) >> 8;
        int z = (dz + 1024) >> 8;
        return ((collisionMasks[player.Model * 8 + z] >> x) & 1) != 0 || ((collisionMasks[other.Model * 8 + 7 - z] >> (7 - x)) & 1) != 0;
    }

    private void ResolvePlayerTerrain()
    {
        if (controlledModel < 0)
        {
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        if (player.Model is not (0 or 1 or 2))
        {
            throw new NotSupportedException("FFVII world terrain resolution requires the original foot model contract.");
        }
        FootCollisionResult? selected = ResolveFootCollision(player);
        if (selected is { } result)
        {
            int terrainType = result.Terrain & 31;
            if (terrainType is 1 or 25)
            {
                throw new NotSupportedException("FFVII foot terrain height transition smoothing is not yet supported for this native terrain.");
            }
            if (terrainType is 13 or 14)
            {
                throw new NotSupportedException("FFVII foot bridge movement requires its native bridge owner.");
            }
            int offset = terrainType is 4 or 7 ? -64 : 0;
            int turn = (player.Flags & 1) != 0 ? (player.TurnOffset * 7 + result.Angle) >> 3 : (player.TurnOffset + result.Angle) >> 1;
            bool moved = result.X != input.PreviousX || result.Z != input.PreviousZ;
            player = player with { X = result.X, Y = result.Height + offset, Z = result.Z, GroundHeight = result.Height, Terrain = result.Terrain, TerrainResolved = true, TurnOffset = turn, Flags = (player.Flags | 2) & ~1 | (moved ? 1 : 0) };
            if (result.Side != input.CollisionSide)
            {
                input = input with { CollisionSide = result.Side, CollisionAge = 0 };
            }
        }
        else
        {
            player = player with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Flags = (player.Flags & ~1) | 2 };
            if ((player.Terrain & 31) == 14)
            {
                input = input with { BridgeCooldown = 5 };
            }
        }
        input = input with { CollisionAge = unchecked(input.CollisionAge + 1), BridgeCooldown = Math.Max(0, input.BridgeCooldown - 1) };
        UpdateEntity(player);
        int script = player.Terrain >> 5 & 7;
        if ((player.Flags & (8 | 128)) == 0)
        {
            if (script >= 3 && input.TerrainScript != script)
            {
                Machine.StartMesh(player.X >> 13, player.Z >> 13, script - 3);
            }
            input = input with { TerrainScript = script >= 3 ? script : 0 };
            if (script == 7)
            {
                UpdateEntity(player with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Flags = player.Flags & ~1 });
            }
        }
        AdvanceWorldModelPresentation();
    }

    private void AdvanceWorldModelPresentation()
    {
        Ff7WorldEntity player = RequireEntity(controlledModel);
        HashSet<(int X, int Z)> visibleChunks = visibility.RequiredChunks(player.X, player.Z, camera.Heading).Select(chunk => (chunk.ChunkX, chunk.ChunkZ)).ToHashSet();
        foreach (Ff7WorldEntity original in entities.Values.ToArray())
        {
            Ff7WorldEntity entity = original;
            if (entity.Model != controlledModel && visibleChunks.Contains((entity.X >> 13, entity.Z >> 13)))
            {
                if (entity.Model is not (0 or 1 or 2))
                {
                    throw new NotSupportedException($"FFVII visible world model {entity.Model} requires its native model terrain and animation contract.");
                }
                (int Height, int Terrain)? terrain = QueryTerrain(entity.X, entity.Z, entity.Y, entity.Terrain);
                if (terrain is { } found)
                {
                    entity = entity with { GroundHeight = found.Height, Terrain = found.Terrain, TerrainResolved = true, Flags = entity.Flags | 2 };
                }
            }
            if ((entity.Flags & 8) != 0)
            {
                entity = entity with { Flags = entity.Flags & ~2 };
            }
            if ((entity.Flags & 2) == 0 || entity.Model is not (0 or 1 or 2))
            {
                UpdateEntity(entity);
                continue;
            }
            if (!leaderAnimations.TryGetValue(entity.Model, out var animationResources))
            {
                Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(inputInstallation, entity.Model);
                animationResources = (descriptor, descriptor.Load(Resources));
                leaderAnimations.Add(entity.Model, animationResources);
            }
            int dx = WrappedDifference(entity.X - entity.PreviousX, 0x48000);
            int dz = WrappedDifference(entity.Z - entity.PreviousZ, 0x38000);
            int animation = entity.Animation != 0 ? Math.Max(0, entity.Animation) : dx * dx + dz * dz >= 100 ? 1 : 0;
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
            UpdateEntity(entity with { PoseAnimation = animation, PoseFrame = frame, AnimationFrame = nextFrame, FrameRemainder = remainder });
        }
    }

    private (int Height, int Terrain)? QueryTerrain(int x, int z, int previousHeight, int previousTerrain)
    {
        int chunkX = x >> 13;
        int chunkZ = z >> 13;
        int block = Ff7WorldVisibility.ResolveBlockIndex((chunkZ >> 2) * 9 + (chunkX >> 2), WorldMapState);
        int index = (chunkZ & 3) * 4 + (chunkX & 3);
        if (!collisionMeshes.TryGetValue((block, index), out PcWorldMesh? mesh))
        {
            mesh = Resources.Map.ReadMesh(block, index);
            collisionMeshes.Add((block, index), mesh);
        }
        int localX = x & 8191;
        int localZ = z & 8191;
        int selected = -1;
        int height = 0;
        bool cachedContains = false;
        uint mask = (previousTerrain & 31) is 13 or 14 ? 0x20006000u : 0x721B6F83u;
        foreach (Ff7WorldTerrainReference cached in input.TerrainCache ?? [])
        {
            if (cached.ChunkX == chunkX && cached.ChunkZ == chunkZ && TryTriangleHeight(mesh, cached.Triangle, localX, localZ, out height))
            {
                cachedContains = true;
                if ((mask & (1u << mesh.Triangles[cached.Triangle].Terrain)) != 0)
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
                    int difference = Math.Abs(candidateHeight - previousHeight);
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
                input = input with { TerrainCache = new[] { new Ff7WorldTerrainReference(chunkX, chunkZ, selected) }.Concat(input.TerrainCache ?? []).Take(6).ToArray() };
            }
        }
        if (selected < 0)
        {
            return null;
        }
        PcWorldTriangle face = mesh.Triangles[selected];
        return (mask & (1u << face.Terrain)) != 0 ? (height, face.TerrainAndScript | (face.PackedIds & 0xFF00)) : null;
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
        if (state.Horizontal is < -1 or > 1 || state.Vertical is < -1 or > 1 || state.CollisionSide is < 0 or > 1 || state.BridgeCooldown is < 0 or > 5 || state.TerrainScript is < 0 or > 7 || state.TerrainCache is { Length: > 6 } || state.CameraHeadingTarget is < 0 or > 4095)
        {
            throw new InvalidDataException("FFVII world input or terrain cache state is corrupt.");
        }
        foreach (Ff7WorldTerrainReference reference in state.TerrainCache ?? [])
        {
            if (reference is null || reference.ChunkX is < 0 or >= 36 || reference.ChunkZ is < 0 or >= 28 || reference.Triangle < 0)
            {
                throw new InvalidDataException("FFVII world terrain cache reference is corrupt.");
            }
            int block = Ff7WorldVisibility.ResolveBlockIndex((reference.ChunkZ >> 2) * 9 + (reference.ChunkX >> 2), restoredMapState);
            int mesh = (reference.ChunkZ & 3) * 4 + (reference.ChunkX & 3);
            if (reference.Triangle >= Resources.Map.ReadMesh(block, mesh).Triangles.Count)
            {
                throw new InvalidDataException("FFVII world terrain cache refers to an absent native triangle.");
            }
        }
    }

    private static int WrappedDifference(int difference, int range)
    {
        return difference < -range / 2 ? difference + range : difference >= range / 2 ? difference - range : difference;
    }
}
