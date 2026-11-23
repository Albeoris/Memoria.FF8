using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Ordinary queue actions use the live world without a window or timed simulation loop.</summary>
internal static class HeadlessControlHost
{
    public static int Run(string[] args)
    {
        LaunchOptions options = PlayerStateLoader.Parse(args);
        if (options.StatePath is null || options.ControlDirectory is null || options.InitialState.Scene is not (SceneKind.Field or SceneKind.WorldMap or SceneKind.Movie))
        {
            throw new ArgumentException("Headless control requires --state <standalone.json> and --control-dir <queue-directory> for a field, world or movie checkpoint.");
        }
        GameSession session = new(options.InitialState);
        using GameResourceHost resources = new(options.GameDirectory, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory, modLocations: options.ModLocations, connectEncounterMods: true);
        resources.PrepareStartupGameplayDifficulty(session.State.GameplayPreferences.Difficulty);
        using MovieClock movieClock = new(session, options.GameDirectory, options.MediaToolsDirectory, resources.ResolveGameFile);
        using GameWorld world = new(session, options.GameDirectory, movieClock, resources.Resources, validateWorldPresentation: false, resolveGameFile: resources.ResolveGameFile, readSceneMods: () => resources.SceneMods, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy, applyEncounterMods: resources.ApplyEncounterMods);
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
            TutorialRequest = world.Events.Scene.NativeTutorial, MenuTutorialRequest = world.Events.Scene.NativeMenuTutorial, Tutorial = world.Tutorial?.Status,
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
        object BattleStatus()
        {
            BattleSession? battle = world.Battle;
            return new
            {
                Battle = battle is null ? null : new
                {
                    battle.Mode, battle.Frame, battle.Outcome, battle.Name, battle.Combatants, battle.CanReturnToField,
                    battle.ActiveAction, battle.QueuedActions, battle.GuardianForces,
                    SealedCommands = battle.Combatants.Where(actor => actor.IsPlayer).ToDictionary(actor => actor.Id, actor => actor.NativeCommandSlots.Where(command => battle.IsPlayerCommandSealed(actor.Id, command)).Select(command => (int)command).ToArray()),
                    MagicAvailability = battle.Combatants.Where(actor => actor.IsPlayer).ToDictionary(actor => actor.Id, actor => actor.MagicStocks.Select(stock => new { stock.Id, Count = battle.GetAvailableMagicCasts(actor.Id, stock.Id) }).Where(stock => stock.Count > 0).ToArray()),
                    Items = battle.Items.Select(stock => new { stock.Id, stock.Count, Action = battle.GetActionDefinition(BattleAbility.Item, stock.Id) }).ToArray(),
                    Magic = battle.Combatants.Where(actor => actor.IsPlayer).SelectMany(actor => actor.MagicStocks).Select(stock => stock.Id).Distinct().Select(id => battle.GetActionDefinition(BattleAbility.Magic, id)).ToArray(),
                    EntryReady = battle.Presentation?.EntryReady,
                    RecentEvents = battle.Events.TakeLast(12).ToArray()
                }
            };
        }
        void Save(string path)
        {
            if (world.Battle is not null || session.HasActiveBattle)
            {
                throw new InvalidOperationException("Research checkpoints require a boundary outside battle.");
            }
            path = Path.GetFullPath(path);
            if (playerSaves.ContainsPath(path))
            {
                throw new ArgumentException("Research checkpoints cannot target the player-saves directory.");
            }
            if (session.State.Scene == SceneKind.WorldMap)
            {
                world.EnsureField();
            }
            else if (world.HasCurrentFieldAllocation)
            {
                session.SetEventState(world.Events.Capture());
            }
            else if (world.Events.HasFieldProgram)
            {
                session.SetDepartingFieldState(world.Events.Capture());
            }
            session.Save(path, compact: Path.GetExtension(path).Equals(".ff8save", StringComparison.OrdinalIgnoreCase));
        }
        object Dispatch(ControlRequest request)
        {
            switch (request.Command.ToLowerInvariant())
            {
                case "status":
                    return Status(request.Compact);
                case "battle-status":
                    return BattleStatus();
                case "battle-input":
                    world.ApplyBattleInput(request.Battle?.Input ?? throw new ArgumentException("Battle Input is required."));
                    return BattleStatus();
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
                            if (!applied.Accepted || session.State.FieldId != firstField || session.State.Scene != firstScene || world.CurrentDialogue?.CanConfirm == true || world.NextTrigger.Kind is PlayerTriggerKind.FieldTransition or PlayerTriggerKind.Battle or PlayerTriggerKind.Tutorial or PlayerTriggerKind.Faulted)
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
                    throw new ArgumentException("Headless control commands: status, action, advance, run, battle-status, battle-input, save, capture, quit.");
            }
        }
        ControlQueue queue = new(options.ControlDirectory, Dispatch);
        Console.WriteLine($"Headless control ready {session.State.FieldId}/{world.Events.Frame}");
        while (running)
        {
            resources.Pump();
            world.SynchronizeGameplayTuning();
            queue.Poll();
            if (running)
            {
                Thread.Sleep(5);
            }
        }
        return 0;
    }
}
