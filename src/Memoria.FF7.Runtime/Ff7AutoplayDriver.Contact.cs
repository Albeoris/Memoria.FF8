using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    public Ff7AutoplayInput Contact(int entity, bool run = true)
    {
        return ChooseBoundaryInput(() => ChooseContactInput(entity, run));
    }

    private Ff7AutoplayInput ChooseContactInput(int entity, bool run)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor target = process.Actors.Single(actor => actor.Entity == entity);
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.PlayerLocked || process.Join is not null)
        {
            return default;
        }
        Vector3 position = new(target.X4096 / 4096f, target.Y4096 / 4096f, target.Z4096 / 4096f);
        Ff7AutoplayInput input = Steer(position, run);
        if (input.Horizontal != 0 || input.Vertical != 0)
        {
            var direction = Directions.Single(value => value.X == input.Horizontal && value.Y == input.Vertical);
            Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
            Ff7NativeMoveResult probe = session.Host.Movement!.StepDirection(controlled, (byte)((direction.Direction + session.Field.Triggers.ControlDirection) & 255), process.Actors, process.BlockedTriangles, playerLocked: false, lineBlocked: process.LineBlocked);
            if (probe.PushEntities.Contains(entity) && AllowsPlannedMove(controlled, probe))
            {
                return input;
            }
        }
        return Approach(position, target.Triangle, run);
    }

    internal Ff7AutoplayInput ChooseTargetInput(Ff7AutoPlayTarget target, Func<Ff7AutoplayInput> choose)
    {
        return ChooseBoundaryInput(choose, ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways));
    }

    private Ff7AutoPlayTarget[]? navigationLineTargets;
    private FieldInputState? navigationLineInput;
    private readonly Dictionary<(int Entity, int Script, FieldInputState Input, bool Talk), bool> navigationLineSkips = [];
    private bool navigationLineTalk;
    private bool navigationLineRejected;
    private bool? navigationRunMode;
    private bool? navigationActionProgress;
    private bool navigationEscapeAction;

    private sealed record NavigationModeState(long WaypointGeneration, int WaypointDestination, Vector3? CommittedWaypoint, int WaypointSourceTriangle, Vector3? FinalTarget, bool FinalApproach, long GatewayGeneration, PcFieldGateway? GatewayTarget, Vector3? GatewayCrossingTarget, long ObstacleGeneration, Vector3? ObstacleTarget, int ObstacleSpeed, Vector3[] ObstacleHistory, EscapeStep[] ObstacleEscape, bool? RunMode, bool? ActionProgress, bool LineRejected, bool EscapeAction);

    private NavigationModeState CaptureNavigationMode()
    {
        return new(waypointGeneration, waypointDestination, committedWaypoint, waypointSourceTriangle, finalTarget, finalApproach, gatewayGeneration, gatewayTarget, gatewayCrossingTarget, obstacleGeneration, obstacleTarget, obstacleSpeed, obstacleHistory.ToArray(), obstacleEscape.ToArray(), navigationRunMode, navigationActionProgress, navigationLineRejected, navigationEscapeAction);
    }

    private void RestoreNavigationMode(NavigationModeState state)
    {
        waypointGeneration = state.WaypointGeneration;
        waypointDestination = state.WaypointDestination;
        committedWaypoint = state.CommittedWaypoint;
        waypointSourceTriangle = state.WaypointSourceTriangle;
        finalTarget = state.FinalTarget;
        finalApproach = state.FinalApproach;
        gatewayGeneration = state.GatewayGeneration;
        gatewayTarget = state.GatewayTarget;
        gatewayCrossingTarget = state.GatewayCrossingTarget;
        obstacleGeneration = state.ObstacleGeneration;
        obstacleTarget = state.ObstacleTarget;
        obstacleSpeed = state.ObstacleSpeed;
        obstacleHistory.Clear();
        foreach (Vector3 position in state.ObstacleHistory)
        {
            obstacleHistory.Enqueue(position);
        }
        obstacleEscape.Clear();
        foreach (EscapeStep step in state.ObstacleEscape)
        {
            obstacleEscape.Enqueue(step);
        }
        navigationRunMode = state.RunMode;
        navigationActionProgress = state.ActionProgress;
        navigationLineRejected = state.LineRejected;
        navigationEscapeAction = state.EscapeAction;
    }
    internal bool? PlannedActionAdvances => navigationActionProgress;

    internal Ff7AutoplayInput ChooseTargetInput(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> offeredTargets, Func<bool, Ff7AutoplayInput> choose)
    {
        int? selectedLine = target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave ? target.Entity : null;
        Ff7AutoPlayTarget[]? retainedTargets = navigationLineTargets;
        FieldInputState? retainedInput = navigationLineInput;
        bool retainedTalk = navigationLineTalk;
        IReadOnlySet<int>? retainedGateways = admittedGateways;
        navigationLineTargets = offeredTargets.Where(candidate => candidate.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && (candidate.ChangesField || candidate.MayChangeNavigation) && candidate.Entity != selectedLine && !(target.Kind == Ff7AutoPlayTargetKind.Gateway && target.DestinationField == candidate.DestinationField)).ToArray();
        navigationLineSkips.Clear();
        admittedGateways = ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways);
        Ff7AutoplayInput? idle = null;
        Ff7AutoplayInput? detour = null;
        NavigationModeState? detourState = null;
        Ff7AutoPlayNavigationUnavailableException? failure = null;
        bool rejected = false;
        try
        {
            foreach (bool run in new[] { true, false })
            {
                if (navigationRunMode != run)
                {
                    waypointGeneration = -1;
                    committedWaypoint = null;
                    finalApproach = false;
                    ResetObstacleEscape();
                    navigationRunMode = run;
                }
                navigationLineInput = ProjectNavigationInput(new(Horizontal: 1, Run: run), session.Machine.Input);
                navigationLineTalk = false;
                navigationLineRejected = false;
                navigationActionProgress = null;
                navigationEscapeAction = false;
                try
                {
                    Ff7AutoplayInput action = choose(run);
                    if (PlanningPending)
                    {
                        return action;
                    }
                    bool admitted = AllowsBoundaryInput(action);
                    rejected |= navigationLineRejected || !admitted;
                    if (!admitted)
                    {
                        continue;
                    }
                    if (action.Horizontal != 0 || action.Vertical != 0 || action.Held != 0 || action.Talk)
                    {
                        if (detour is Ff7AutoplayInput retained)
                        {
                            Ff7AutoplayInput selected = PreferAdvancingNavigationInput(retained, action, navigationActionProgress == true);
                            if (selected == retained)
                            {
                                RestoreNavigationMode(detourState!);
                            }
                            return selected;
                        }
                        if (ShouldTryWalkingAction(action, navigationLineRejected, navigationEscapeAction))
                        {
                            detour = action;
                            detourState = CaptureNavigationMode();
                            continue;
                        }
                        return action;
                    }
                    idle ??= action;
                }
                catch (Ff7AutoPlayNavigationUnavailableException blocked)
                {
                    if (PlanningPending)
                    {
                        throw;
                    }
                    failure = blocked;
                    rejected = true;
                }
            }
            if (detour is Ff7AutoplayInput retainedDetour)
            {
                RestoreNavigationMode(detourState!);
                return retainedDetour;
            }
            if (idle is Ff7AutoplayInput waiting && !rejected)
            {
                return waiting;
            }
            throw failure ?? new Ff7AutoPlayNavigationUnavailableException(target.Entity, "Neither native run nor walk admits the selected approach without executing an unselected line owner.");
        }
        finally
        {
            navigationLineTargets = retainedTargets;
            navigationLineInput = retainedInput;
            navigationLineTalk = retainedTalk;
            admittedGateways = retainedGateways;
        }
    }

    internal static bool ShouldTryWalkingAction(Ff7AutoplayInput action, bool lineRejected, bool escapePlanned)
    {
        return lineRejected && !escapePlanned && action.Held == 0 && !action.Talk;
    }

    internal static Ff7AutoplayInput PreferAdvancingNavigationInput(Ff7AutoplayInput fallback, Ff7AutoplayInput candidate, bool advances)
    {
        return advances || candidate.Held != 0 || candidate.Talk ? candidate : fallback;
    }

    internal static FieldInputState ProjectNavigationInput(Ff7AutoplayInput action, FieldInputState previous)
    {
        ushort pressed = (ushort)(action.Held & ~previous.Held);
        FieldInputState input = new(action.Held, pressed, (ushort)(previous.Held & ~action.Held), action.Held, pressed, (ushort)(previous.RawHeld & ~action.Held), PreviousHeld: previous.Held, PreviousRawHeld: previous.RawHeld);
        if (action.Horizontal != 0 || action.Vertical != 0)
        {
            const ushort runButton = 0x40;
            static ushort Project(ushort value, bool enabled) => enabled ? (ushort)(value | runButton) : (ushort)(value & ~runButton);
            bool heldRun = (previous.Held & runButton) != 0;
            bool rawRun = (previous.RawHeld & runButton) != 0;
            input = input with { Held = Project(input.Held, action.Run), Pressed = Project(input.Pressed, action.Run && !heldRun), Released = Project(input.Released, !action.Run && heldRun), RawHeld = Project(input.RawHeld, action.Run), RawPressed = Project(input.RawPressed, action.Run && !rawRun), RawReleased = Project(input.RawReleased, !action.Run && rawRun) };
        }
        return input;
    }

    private bool CanSkipNavigationLine(Ff7AutoPlayTarget target)
    {
        FieldInputState input = navigationLineInput ?? throw new InvalidOperationException("Native navigation line input was not scoped.");
        var key = (target.Entity, target.Script, input, navigationLineTalk);
        if (!navigationLineSkips.TryGetValue(key, out bool skip))
        {
            skip = session.Machine.CanSkipScriptEntryForInput(target.Entity, target.Script, input);
            if (!skip && !navigationLineTalk)
            {
                FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(target.Entity, target.Script, input);
                skip = proof.Complete && proof.HasNoEffect;
            }
            navigationLineSkips.Add(key, skip);
        }
        return skip;
    }

    internal static bool HasNativeLineRequest(Ff7AutoPlayTargetKind kind, Ff7Line line)
    {
        return kind switch
        {
            Ff7AutoPlayTargetKind.LineTalk => line.Talk,
            Ff7AutoPlayTargetKind.LinePush => line.Push,
            Ff7AutoPlayTargetKind.LineCross => line.Across,
            Ff7AutoPlayTargetKind.LineTouch => line.Touch,
            Ff7AutoPlayTargetKind.LineEnter => line.Enter,
            Ff7AutoPlayTargetKind.LineLeave => line.Leave,
            _ => false
        };
    }

    private bool AllowsBoundaryInput(Ff7AutoplayInput input)
    {
        FieldInputState? retainedInput = navigationLineInput;
        bool retainedTalk = navigationLineTalk;
        try
        {
            if (navigationLineTargets is not null)
            {
                navigationLineInput = ProjectNavigationInput(input, session.Machine.Input);
                navigationLineTalk = input.Talk;
                foreach (Ff7AutoPlayTarget target in navigationLineTargets)
                {
                    Ff7Line line = session.Host.Process.Lines.Last(line => line.Entity == target.Entity);
                    if (HasNativeLineRequest(target.Kind, line) && !CanSkipNavigationLine(target))
                    {
                        navigationLineRejected = true;
                        return false;
                    }
                }
            }
            if ((input.Horizontal != 0 || input.Vertical != 0) && session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is Ff7Actor player)
            {
                var direction = Directions.Single(value => value.X == input.Horizontal && value.Y == input.Vertical);
                Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
                int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
                Ff7NativeMoveResult probe = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection + rotation)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                return AllowsPlannedMove(controlled, probe);
            }
            return true;
        }
        finally
        {
            navigationLineInput = retainedInput;
            navigationLineTalk = retainedTalk;
        }
    }

    private Ff7AutoplayInput ChooseBoundaryInput(Func<Ff7AutoplayInput> choose, IReadOnlySet<int>? gateways = null)
    {
        IReadOnlySet<int>? previous = admittedGateways;
        admittedGateways = gateways ?? previous;
        try
        {
            Ff7AutoplayInput input = choose();
            return AllowsBoundaryInput(input) ? input : default;
        }
        finally
        {
            admittedGateways = previous;
        }
    }
}
