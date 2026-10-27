using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Runs recorded ordinary actions through the live host's VM without requiring a graphics adapter.</summary>
internal static class HeadlessPlayer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    internal static PlayerAction[] ReadActions(string json) => JsonSerializer.Deserialize<PlayerAction[]>(json, JsonOptions) ?? throw new InvalidDataException("Missing ordinary action array.");

    public static int Run(string[] args)
    {
        string? actionsPath = null;
        string? outputDirectory = null;
        (int X, int Z)? worldTarget = null;
        string? worldPathFile = null;
        string? fieldPathFile = null;
        float[][]? fieldPath = null;
        int[][]? worldPath = null;
        int pathIndex = 0;
        int worldTolerance = 64;
        int worldBudget = 600;
        List<string> launchArguments = [];
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] is "--actions" or "--output" or "--world-target" or "--world-budget" or "--world-path" or "--world-tolerance" or "--field-path")
            {
                string argument = args[index];
                if (++index == args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }
                if (argument == "--actions")
                {
                    actionsPath = Path.GetFullPath(args[index]);
                }
                else if (argument == "--output")
                {
                    outputDirectory = Path.GetFullPath(args[index]);
                }
                else if (argument == "--world-target")
                {
                    string[] coordinates = args[index].Split(',');
                    if (coordinates.Length != 2)
                    {
                        throw new ArgumentException("World target requires native X,rawZ.");
                    }
                    worldTarget = (int.Parse(coordinates[0]), int.Parse(coordinates[1]));
                }
                else if (argument == "--world-path")
                {
                    worldPathFile = Path.GetFullPath(args[index]);
                }
                else if (argument == "--field-path")
                {
                    fieldPathFile = Path.GetFullPath(args[index]);
                }
                else if (argument == "--world-tolerance")
                {
                    worldTolerance = int.Parse(args[index]);
                }
                else
                {
                    worldBudget = int.Parse(args[index]);
                }
            }
            else
            {
                launchArguments.Add(args[index]);
            }
        }
        int inputSources = (actionsPath is null ? 0 : 1) + (worldTarget is null ? 0 : 1) + (worldPathFile is null ? 0 : 1) + (fieldPathFile is null ? 0 : 1);
        if (inputSources != 1 || outputDirectory is null || worldBudget is < 1 or > 30000 || worldTolerance is < 8 or > 64)
        {
            throw new ArgumentException("Headless play requires --state, --actions <JSON array> and --output <new directory>.");
        }
        LaunchOptions options = LaunchOptions.Parse(launchArguments.ToArray());
        if (options.StatePath is null || options.InitialState.Scene is not (SceneKind.Field or SceneKind.WorldMap or SceneKind.Movie))
        {
            throw new NotSupportedException("Headless play requires an exact field, world or movie checkpoint.");
        }
        PlayerAction[] actions = actionsPath is null ? [] : ReadActions(File.ReadAllText(actionsPath));
        if (worldPathFile is not null)
        {
            worldPath = JsonSerializer.Deserialize<int[][]>(File.ReadAllText(worldPathFile)) ?? throw new InvalidDataException("Missing world guide path.");
            if (worldPath.Length == 0 || worldPath.Any(point => point.Length != 2 || point[0] is < -131072 or >= 131072 || point[1] is < -98304 or >= 98304))
            {
                throw new InvalidDataException("World guide requires ordered native X/rawZ pairs within original world spans.");
            }
            worldTarget = (worldPath[0][0], worldPath[0][1]);
        }
        if (fieldPathFile is not null)
        {
            fieldPath = JsonSerializer.Deserialize<float[][]>(File.ReadAllText(fieldPathFile)) ?? throw new InvalidDataException("Missing field guide path.");
            if (fieldPath.Length == 0 || fieldPath.Any(point => point.Length != 2 || point.Any(value => !float.IsFinite(value) || value is < short.MinValue or > short.MaxValue)))
            {
                throw new InvalidDataException("Field guide requires ordered finite original X/Y pairs within signed WORD coordinates.");
            }
        }
        if (Directory.Exists(outputDirectory))
        {
            throw new IOException("Headless evidence requires a new output directory.");
        }
        if (new PlayerSaveStore(options.SavePath).ContainsPath(outputDirectory))
        {
            throw new ArgumentException("Research checkpoints cannot target the player-saves directory.");
        }
        Directory.CreateDirectory(outputDirectory);
        var session = new GameSession(options.InitialState);
        using var resourceHost = new GameResourceHost(options.GameDirectory, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage);
        using var world = new GameWorld(session, options.GameDirectory, resources: resourceHost.Resources, resolveGameFile: resourceHost.ResolveGameFile, readSceneMods: () => resourceHost.SceneMods, modelOverrides: resourceHost, readGameplayPolicy: () => resourceHost.GameplayPolicy);
        world.EnsureField();
        world.SetRenderSize(options.Width, options.Height);
        List<PlayerActionResult> results = [];
        List<PlayerAction> issuedActions = [];
        string? failure = null;
        string? stopReason = null;
        int blockedTicks = 0;
        PlayerAction? attempted = null;
        for (int index = 0; index < (worldTarget is null && fieldPath is null ? actions.Length : worldBudget); index++)
        {
            resourceHost.Pump();
            world.SynchronizeGameplayTuning();
            WorldState? before = session.State.World;
            PlayerAction action;
            if (fieldPath is not null)
            {
                if (session.State.Scene != SceneKind.Field || !world.Events.IsUserControlEnabled || world.Events.IsDialogueBlocking)
                {
                    stopReason = "Field guide stopped at a scene, control or dialogue change.";
                    break;
                }
                ActorState player = world.Events.PlayerActor is { Active: true } leader ? leader : throw new NotSupportedException("Field guiding requires its current native runtime leader binding.");
                while (pathIndex < fieldPath.Length && Math.Sqrt(Math.Pow(fieldPath[pathIndex][0] - player.X, 2) + Math.Pow(fieldPath[pathIndex][1] - player.Y, 2)) <= worldTolerance)
                {
                    pathIndex++;
                }
                if (pathIndex == fieldPath.Length)
                {
                    stopReason = "Ordinary field movement reached the ordered guide path.";
                    break;
                }
                float dx = fieldPath[pathIndex][0] - player.X;
                float dy = fieldPath[pathIndex][1] - player.Y;
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float angle = world.Events.InputAxisDirection * MathF.Tau / 256;
                action = new PlayerAction(PlayerActionKind.Move, Horizontal: (MathF.Cos(angle) * dx + MathF.Sin(angle) * dy) / distance, Vertical: (-MathF.Sin(angle) * dx + MathF.Cos(angle) * dy) / distance);
            }
            else
            {
                if (worldPath is not null && before is not null)
                {
                    while (pathIndex < worldPath.Length && Math.Sqrt(Math.Pow(worldPath[pathIndex][0] - before.X, 2) + Math.Pow(worldPath[pathIndex][1] - before.Z, 2)) <= worldTolerance)
                    {
                        pathIndex++;
                    }
                    if (pathIndex == worldPath.Length)
                    {
                        stopReason = "Ordinary world movement reached the ordered guide path.";
                        break;
                    }
                    worldTarget = (worldPath[pathIndex][0], worldPath[pathIndex][1]);
                }
                if (worldTarget is (int targetX, int targetZ))
                {
                    if (session.State.Scene != SceneKind.WorldMap || before is null)
                    {
                        stopReason = "World navigation stopped at a scene change.";
                        break;
                    }
                    if (worldPath is null && Math.Sqrt(Math.Pow(targetX - before.X, 2) + Math.Pow(targetZ - before.Z, 2)) <= worldTolerance)
                    {
                        stopReason = "Ordinary world movement reached the guide target.";
                        break;
                    }
                    action = WorldNavigationGuide.Next(before, targetX, targetZ);
                }
                else
                {
                    action = actions[index];
                }
            }
            Save(world, session, Path.Combine(outputDirectory, "preaction-checkpoint.json"));
            attempted = action;
            issuedActions.Add(action);
            try
            {
                if (session.State.Scene == SceneKind.Movie && action.Kind is not (PlayerActionKind.SkipMovie or PlayerActionKind.Wait or PlayerActionKind.FieldButtons or PlayerActionKind.Move))
                {
                    throw new NotSupportedException("Headless movie actions require SkipMovie, bounded Wait, Move or raw FieldButtons.");
                }
                PlayerActionResult result = world.Apply(action);
                results.Add(result);
                if (worldTarget is not null && session.State.World is WorldState after)
                {
                    blockedTicks = before!.X == after.X && before.Z == after.Z ? blockedTicks + 1 : 0;
                    if (blockedTicks >= 5)
                    {
                        stopReason = "Five ordinary world proposals were blocked by native terrain probes; inspect before choosing a sidestep.";
                        break;
                    }
                }
                if (!result.Accepted || result.Trigger.Kind is PlayerTriggerKind.Dialogue or PlayerTriggerKind.FieldTransition or PlayerTriggerKind.Movie or PlayerTriggerKind.Tutorial or PlayerTriggerKind.Faulted)
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                Console.Error.WriteLine(exception);
                break;
            }
        }
        Save(world, session, Path.Combine(outputDirectory, "checkpoint.json"));
        try
        {
            byte[] pixels = world.WorldScene?.LastDeparture is not null ? world.WorldScene.Render(options.Width, options.Height) : world.Render(TimeSpan.Zero);
            WritePng(Path.Combine(outputDirectory, "frame.png"), pixels, options.Width, options.Height);
        }
        catch (Exception exception)
        {
            failure ??= exception.ToString();
            Console.Error.WriteLine(exception);
        }
        var report = new { Source = options.StatePath, Actions = issuedActions, Results = results, AttemptedAction = attempted, Failure = failure, StopReason = stopReason, Trigger = world.NextTrigger, RenderedFieldId = world.Field?.Id, World = session.State.World ?? session.State.DepartingWorldState, FieldVm = world.Events.Capture(), world.Events.Coverage, world.Events.Decisions, world.Events.DialogueOutcomes, world.Events.ActionLog };
        File.WriteAllText(Path.Combine(outputDirectory, "report.json"), JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine(JsonSerializer.Serialize(new { Output = outputDirectory, Accepted = results.Count(result => result.Accepted), world.NextTrigger, StopReason = stopReason, Failure = failure?.Split('\n')[0], World = session.State.World is WorldState state ? new { state.Frame, state.X, state.Z, state.NativeHeight } : null }));
        return failure is null ? 0 : 1;
    }

    private static void Save(GameWorld world, GameSession session, string path)
    {
        if (session.State.Scene == SceneKind.WorldMap)
        {
            world.EnsureField();
        }
        else if (world.HasCurrentFieldAllocation)
        {
            session.SetEventState(world.Events.Capture());
        }
        else
        {
            session.SetDepartingFieldState(world.Events.Capture());
        }
        session.Save(path);
    }

    private static void WritePng(string path, byte[] rgba, int width, int height)
    {
        using SKBitmap bitmap = PortableRaster.FromRgba(rgba, width, height);
        bitmap.SavePng(path);
    }
}
