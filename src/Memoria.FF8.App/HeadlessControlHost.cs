using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Ordinary queue actions use the live world without a window or timed simulation loop.</summary>
internal static class HeadlessControlHost
{
    public static int Run(string[] args)
    {
        LaunchOptions options = LaunchOptions.Parse(args);
        if (options.StatePath is null || options.ControlDirectory is null || options.InitialState.Scene is not (SceneKind.Field or SceneKind.WorldMap or SceneKind.Movie))
        {
            throw new ArgumentException("Headless control requires --state <standalone.json> and --control-dir <queue-directory> for a field, world or movie checkpoint.");
        }
        GameSession session = new(options.InitialState);
        using GameResourceHost resources = new(options.GameDirectory);
        using GameWorld world = new(session, options.GameDirectory, resources: resources.Resources);
        world.EnsureField();
        bool running = true;
        bool faulted = world.Events.LastFault is not null;
        string? error = null;
        PlayerSaveStore playerSaves = new(options.SavePath);
        object Status(bool compact) => new
        {
            session.State.Scene, session.State.FieldId, session.State.FieldName, session.State.FieldArrival,
            session.State.MovieIndex, session.State.MovieSeconds, session.State.MoviePlayback, World = session.State.World,
            Trigger = world.NextTrigger, Dialogue = world.CurrentDialogue,
            PartyMenu = world.Events.PartyMenu, PartyMenuCandidates = world.Events.PartyMenuCandidates,
            FieldBars = session.State.FieldBars,
            IsUserControlEnabled = session.State.Scene == SceneKind.WorldMap || world.Events.IsUserControlEnabled,
            FieldFaulted = faulted || world.Events.LastFault is not null, FieldRenderFaulted = false,
            world.Events.LastFault, world.Events.Scene.CameraIndex, world.Events.Scene.LockedTriangles,
            Actors = compact ? (object)world.Events.Actors.Select(actor => new { actor.ObjectId, actor.CharacterId, actor.ModelId, actor.Active, actor.Visible, actor.TalkEnabled, actor.TalkRadius, actor.PushRadius, actor.X, actor.Y, actor.Z, actor.WalkmeshTriangle, actor.Direction, actor.LineStart, actor.LineEnd, actor.LineEnabled }).ToArray() : world.Events.Actors,
            Battle = world.Battle?.Capture(), Error = error,
            Coverage = compact ? null : world.Events.Coverage,
            Decisions = compact ? null : world.Events.Decisions,
            DialogueOutcomes = compact ? null : world.Events.DialogueOutcomes,
            ActionLog = compact ? null : world.Events.ActionLog,
            TraceCounts = new { Coverage = world.Events.Coverage.Count, Decisions = world.Events.Decisions.Count, DialogueOutcomes = world.Events.DialogueOutcomes.Count, Actions = world.Events.ActionLog.Count }
        };
        void Save(string path)
        {
            path = Path.GetFullPath(path);
            if (playerSaves.ContainsPath(path))
            {
                throw new ArgumentException("Research checkpoints cannot target the player-saves directory.");
            }
            if (session.State.Scene == SceneKind.WorldMap && world.WorldScene is not null || world.HasCurrentFieldAllocation)
            {
                session.SetEventState(world.Events.Capture());
            }
            else
            {
                session.SetDepartingFieldState(world.Events.Capture());
            }
            session.Save(path);
        }
        object Dispatch(ControlRequest request)
        {
            switch (request.Command.ToLowerInvariant())
            {
                case "status":
                    return Status(request.Compact);
                case "save":
                    Save(request.Path ?? options.SavePath);
                    return Status(request.Compact);
                case "quit":
                    running = false;
                    return new { Exiting = true };
                case "capture":
                    return new { Captured = false, Reason = "Headless control has no raster output; use save for an exact standalone checkpoint." };
                case "action":
                case "advance":
                case "run":
                    if (faulted || world.Events.LastFault is not null)
                    {
                        throw new InvalidOperationException("Headless control stopped at its preserved fault. Save, inspect or quit before resuming a safe checkpoint.");
                    }
                    bool repeat = request.Command.Equals("run", StringComparison.OrdinalIgnoreCase);
                    PlayerAction action = request.Command.Equals("advance", StringComparison.OrdinalIgnoreCase) ? new(PlayerActionKind.Wait, Frames: request.Frames) : request.Action ?? throw new ArgumentException("An ordinary action payload is required.");
                    int count = repeat ? request.Frames : 1;
                    if (count < 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(request), "Run requires a positive action count.");
                    }
                    if (repeat)
                    {
                        Save(Path.Combine(options.ControlDirectory, "before-run-checkpoint.json"));
                    }
                    int firstField = session.State.FieldId;
                    SceneKind firstScene = session.State.Scene;
                    PlayerActionResult applied = new(false, world.NextTrigger, 0);
                    try
                    {
                        for (int index = 0; index < count; index++)
                        {
                            applied = world.Apply(action);
                            if (applied.Accepted && action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Choose or PlayerActionKind.EnterName or PlayerActionKind.Cancel)
                            {
                                session.SetDialogueDraft(null);
                            }
                            if (!applied.Accepted || session.State.FieldId != firstField || session.State.Scene != firstScene || world.CurrentDialogue?.CanConfirm == true || world.NextTrigger.Kind is PlayerTriggerKind.FieldTransition or PlayerTriggerKind.Battle or PlayerTriggerKind.Faulted)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        faulted = true;
                        error = exception.Message;
                        Console.Error.WriteLine(exception);
                        throw;
                    }
                    return new { ActionResult = applied, Status = Status(request.Compact) };
                default:
                    throw new ArgumentException("Headless control commands: status, action, advance, run, save, capture, quit.");
            }
        }
        ControlQueue queue = new(options.ControlDirectory, Dispatch);
        Console.WriteLine($"Headless control ready {session.State.FieldId}/{world.Events.Frame}");
        while (running)
        {
            resources.Pump();
            queue.Poll();
            if (running)
            {
                Thread.Sleep(5);
            }
        }
        return 0;
    }
}
