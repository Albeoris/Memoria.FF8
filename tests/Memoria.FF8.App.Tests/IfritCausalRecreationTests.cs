using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class IfritCausalRecreationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private static void DriveUntil(GameWorld world, GameSession session, Func<bool> complete)
    {
        for (int input = 0; input < 3000; input++)
        {
            world.EnsureField();
            if (complete())
            {
                return;
            }
            Apply(world, NextAutomatic(world, session));
        }
        throw new NotSupportedException($"Automatic causal drive exceeded its bounded input budget at field{session.State.FieldId}, frame{world.Events.Frame}.");
    }

    private static PlayerAction NextAutomatic(GameWorld world, GameSession session)
    {
        DialogueState? dialogue = world.Events.CurrentDialogue;
        return session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue?.FirstChoice is int first ? new PlayerAction(PlayerActionKind.Choose, first) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
    }

    internal static void WalkGateway(GameWorld world, GameSession session, int destination, bool worldExit = false, int? lineObjectId = null)
    {
        int source = session.State.FieldId;
        DriveUntil(world, session, () => world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        FieldAssets field = world.Field!;
        var gateways = field.Information!.Gateways.Where(gateway => gateway.Defined && (worldExit ? gateway.DestinationField < 72 : gateway.DestinationField == destination)).ToArray();
        Vector2 midpoint;
        Vector2 lineDirection;
        if (gateways.Length == 0)
        {
            ActorState current = Player(world);
            ActorState? line = world.Events.Actors.Where(actor => actor.LineEnabled && actor.LineStart is not null && actor.LineEnd is not null && (lineObjectId is null || actor.ObjectId == lineObjectId)).MaxBy(actor => Vector2.DistanceSquared(new Vector2(current.X, current.Y), new Vector2((actor.LineStart!.Value.X + actor.LineEnd!.Value.X) / 2, (actor.LineStart.Value.Y + actor.LineEnd.Value.Y) / 2)));
            if (line is null)
            {
                throw new NotSupportedException($"Field{source} has no advisory INF gateway or executed SETLINE to{destination}.");
            }
            midpoint = new Vector2((line.LineStart!.Value.X + line.LineEnd!.Value.X) / 2, (line.LineStart.Value.Y + line.LineEnd.Value.Y) / 2);
            lineDirection = new Vector2(line.LineEnd.Value.Y - line.LineStart.Value.Y, line.LineStart.Value.X - line.LineEnd.Value.X);
        }
        else
        {
            var gateway = gateways[0];
            midpoint = new Vector2((gateway.Start.X + gateway.End.X) / 2f, (gateway.Start.Y + gateway.End.Y) / 2f);
            lineDirection = new Vector2(gateway.End.Y - gateway.Start.Y, gateway.Start.X - gateway.End.X);
        }
        ActorState player = Player(world);
        int targetTriangle = ReachableTriangles(field, player.WalkmeshTriangle, world.Events.Scene.LockedTriangles).MinBy(index => Vector2.DistanceSquared(Center(field, index), midpoint));
        List<int> route = TriangleRoute(field, player.WalkmeshTriangle, targetTriangle, world.Events.Scene.LockedTriangles);
        Vector2 last = new(player.X, player.Y);
        foreach (Vector2 point in route.Skip(1).Select(index => Center(field, index)).Append(midpoint))
        {
            int unchanged = 0;
            for (int tick = 0; tick < 3000 && session.State.Scene == SceneKind.Field && session.State.FieldId == source; tick++)
            {
                if (world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable)
                {
                    Apply(world, NextAutomatic(world, session));
                    continue;
                }
                player = Player(world);
                last = new Vector2(player.X, player.Y);
                Vector2 delta = point - last;
                if (delta.Length() <= 33)
                {
                    break;
                }
                Vector2 axes = Vector2.Transform(Vector2.Normalize(delta), Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
                ApplyGuidedMove(world, new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y), ref unchanged);
                if (tick == 2999)
                {
                    throw new NotSupportedException($"Advisory route field{source} to{destination} stalled toward({point.X},{point.Y}) from({player.X},{player.Y})/triangle{player.WalkmeshTriangle}.");
                }
            }
            if (session.State.Scene != SceneKind.Field || session.State.FieldId != source)
            {
                break;
            }
        }
        Vector2 forward = midpoint - last;
        if (forward.LengthSquared() == 0)
        {
            forward = lineDirection;
        }
        for (int tick = 0; tick < 500 && session.State.Scene == SceneKind.Field && session.State.FieldId == source; tick++)
        {
            Vector2 axes = Vector2.Transform(Vector2.Normalize(forward), Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
            Apply(world, world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable ? NextAutomatic(world, session) : new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y));
        }
        Assert.True(worldExit ? session.State.Scene == SceneKind.WorldMap : session.State.FieldId == destination, $"Advisory gateway field{source} did not naturally reach{destination}: {world.NextTrigger}.");
        Console.WriteLine($"Fresh route field{source}→{session.State.Scene}/{session.State.FieldId}, native arrival{session.State.FieldArrival}.");
        world.EnsureField();
    }

    private static Vector2 Center(FieldAssets field, int triangle)
    {
        var value = field.Walkmesh.Triangles[triangle];
        return new Vector2((value.A.X + value.B.X + value.C.X) / 3f, (value.A.Y + value.B.Y + value.C.Y) / 3f);
    }

    internal static void WalkDirectToInteraction(GameWorld world, GameSession session, int target, Func<bool>? complete = null)
    {
        int unchanged = 0;
        for (int tick = 0; tick < 3000; tick++)
        {
            if (complete?.Invoke() == true)
            {
                return;
            }
            if (complete is null && world.Events.SelectInteractionTarget() == target)
            {
                return;
            }
            if (world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable)
            {
                Apply(world, NextAutomatic(world, session));
                continue;
            }
            ActorState player = Player(world);
            ActorState actor = world.Events.Actors[target];
            Vector2 delta = new(actor.X - player.X, actor.Y - player.Y);
            Vector2 axes = Vector2.Transform(Vector2.Normalize(delta), Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
            ApplyGuidedMove(world, new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y), ref unchanged);
        }
        throw new NotSupportedException($"Direct advisory interaction in field{world.Events.FieldId}/actor{target} stalled at {Player(world).X},{Player(world).Y}.");
    }

    internal static void WalkRoutedToInteraction(GameWorld world, GameSession session, int target)
    {
        DriveUntil(world, session, () => world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        FieldAssets field = world.Field!;
        ActorState actor = world.Events.Actors[target];
        ActorState player = Player(world);
        Vector2 destination = new(actor.X, actor.Y);
        int triangle = ReachableTriangles(field, player.WalkmeshTriangle, world.Events.Scene.LockedTriangles).MinBy(index => Vector2.DistanceSquared(Center(field, index), destination));
        foreach (int next in TriangleRoute(field, player.WalkmeshTriangle, triangle, world.Events.Scene.LockedTriangles).Skip(1))
        {
            Vector2 point = Center(field, next);
            int unchanged = 0;
            for (int tick = 0; tick < 3000; tick++)
            {
                if (world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable)
                {
                    Apply(world, NextAutomatic(world, session));
                    continue;
                }
                if (world.Events.SelectInteractionTarget() == target)
                {
                    return;
                }
                player = Player(world);
                Vector2 delta = point - new Vector2(player.X, player.Y);
                if (delta.Length() <= 33)
                {
                    break;
                }
                Vector2 axes = Vector2.Transform(Vector2.Normalize(delta), Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
                ApplyGuidedMove(world, new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y), ref unchanged);
            }
        }
        WalkDirectToInteraction(world, session, target);
    }

    internal static void WalkAdvisoryPoint(GameWorld world, GameSession session, Vector2 point)
    {
        int unchanged = 0;
        for (int tick = 0; tick < 3000; tick++)
        {
            ActorState player = Player(world);
            Vector2 delta = point - new Vector2(player.X, player.Y);
            if (delta.Length() <= 33)
            {
                return;
            }
            Vector2 axes = Vector2.Transform(Vector2.Normalize(delta), Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
            if (world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable)
            {
                Apply(world, NextAutomatic(world, session));
                continue;
            }
            ApplyGuidedMove(world, new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y), ref unchanged);
        }
        throw new NotSupportedException($"Advisory waypoint {point} stalled in field{world.Events.FieldId} at {Player(world).X},{Player(world).Y}.");
    }

    private static void WritePng(string path, byte[] rgba, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static IReadOnlySet<int> ReachableTriangles(FieldAssets field, int source, IReadOnlySet<int> locked)
    {
        var visited = new HashSet<int> { source };
        var queue = new Queue<int>();
        queue.Enqueue(source);
        while (queue.TryDequeue(out int current))
        {
            var triangle = field.Walkmesh.Triangles[current];
            foreach (int neighbor in new[] { triangle.NeighborA, triangle.NeighborB, triangle.NeighborC })
            {
                if (neighbor >= 0 && !locked.Contains(neighbor) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }
        return visited;
    }

    private static List<int> TriangleRoute(FieldAssets field, int source, int destination, IReadOnlySet<int> locked)
    {
        Dictionary<int, int> parents = [];
        var visited = new HashSet<int> { source };
        var queue = new Queue<int>();
        queue.Enqueue(source);
        while (queue.TryDequeue(out int current) && !visited.Contains(destination))
        {
            var triangle = field.Walkmesh.Triangles[current];
            foreach (int neighbor in new[] { triangle.NeighborA, triangle.NeighborB, triangle.NeighborC })
            {
                if (neighbor >= 0 && !locked.Contains(neighbor) && visited.Add(neighbor))
                {
                    parents[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }
        var route = new List<int> { destination };
        while (route[^1] != source)
        {
            if (!parents.TryGetValue(route[^1], out int previous))
            {
                throw new NotSupportedException($"No connected advisory triangle route {source}→{destination} in field{field.Id}.");
            }
            route.Add(previous);
        }
        route.Reverse();
        return route;
    }

    private static ActorState Player(GameWorld world)
    {
        return world.Events.Actors.Single(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
    }

    private static void Apply(GameWorld world, PlayerAction action)
    {
        PlayerActionResult result = world.Apply(action);
        Assert.True(result.Accepted, $"Fresh causal input refused: {action}; {result.Trigger}.");
    }

    private static void ApplyRecipe(GameWorld world, string repository, string name, params string[] properties)
    {
        ApplyRecipeFromIndex(world, repository, name, 0, properties);
    }

    private static void ApplyRecipeFromIndex(GameWorld world, string repository, string name, int skip, params string[] properties)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "../fixtures", name)));
        JsonElement value = document.RootElement;
        foreach (string property in properties)
        {
            value = value.GetProperty(property);
        }
        foreach (JsonElement item in value.EnumerateArray().Skip(skip))
        {
            PlayerAction action = item.Deserialize<PlayerAction>(JsonOptions)!;
            if (action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Choose or PlayerActionKind.EnterName)
            {
                for (int wait = 0; world.Events.CurrentDialogue is not { CanConfirm: true } && wait < 3000; wait++)
                {
                    Apply(world, new PlayerAction(PlayerActionKind.Wait));
                }
            }
            Apply(world, action);
        }
    }

    private static void ApplyGuidedMove(GameWorld world, PlayerAction action, ref int unchanged)
    {
        ActorState player = Player(world);
        Vector3 before = new(player.X, player.Y, player.Z);
        Apply(world, action);
        Vector3 after = new(player.X, player.Y, player.Z);
        unchanged = before == after ? unchanged + 1 : 0;
        if (unchanged >= 5)
        {
            throw new NotSupportedException($"Advisory guide needs a new waypoint after five unchanged poses in field{world.Events.FieldId} at {after}/triangle{player.WalkmeshTriangle}.");
        }
    }

    private static void ApplyWorldRecipe(GameWorld world, string repository, string name)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "../fixtures", name)));
        foreach (JsonElement item in document.RootElement.GetProperty("DepartingWorldState").GetProperty("Actions").EnumerateArray())
        {
            Apply(world, new PlayerAction(Enum.Parse<PlayerActionKind>(item.GetProperty("Kind").GetString()!), Horizontal: item.GetProperty("Horizontal").GetSingle(), Vertical: item.GetProperty("Vertical").GetSingle(), Frames: item.GetProperty("RequestedFrames").GetInt32()));
        }
    }

    private static void ApplyStepRecipes(GameWorld world, string repository, string name, bool compactRows)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "../fixtures", name)));
        foreach (JsonElement step in document.RootElement.GetProperty("Steps").EnumerateArray())
        {
            foreach (JsonElement item in step.GetProperty("Actions").EnumerateArray())
            {
                PlayerAction action = compactRows ? new PlayerAction(Enum.Parse<PlayerActionKind>(item[0].GetString()!), Horizontal: item[1].GetSingle(), Vertical: item[2].GetSingle(), Frames: item[3].GetInt32()) : item.Deserialize<PlayerAction>(JsonOptions)!;
                Apply(world, action);
            }
        }
    }

    private static string Repository()
    {
        DirectoryInfo? current = new(TestResources.FixtureDirectory);
        return current?.FullName ?? throw new DirectoryNotFoundException("The original route evidence repository is required.");
    }
}
