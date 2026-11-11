using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record WorldAutoPlayTarget(string Id, int X, int RawZ, int DestinationField, int Order);
internal sealed record WorldAutoPlayTravelResult(PlayerActionResult Action, bool MatchedTarget);

internal sealed partial class GameWorld
{
    public IReadOnlyList<WorldAutoPlayTarget> DiscoverAutoPlayWorldTargets()
    {
        EnsureField();
        if (session.State.Scene != SceneKind.WorldMap || worldScene is null)
        {
            return [];
        }
        return worldScene.DiscoverAutoPlayEntrances();
    }

    public WorldAutoPlayTravelResult ExecuteAutoPlayWorldTarget(WorldAutoPlayTarget target, Action? onProgress = null)
    {
        EnsureField();
        WorldMapScene scene = worldScene ?? throw new InvalidOperationException("World automatic travel requires the current world scene.");
        bool missionCar = WorldMapScene.IsAutoPlayMissileBaseCar(scene.State, WorldEntryVariables.Read(session.State).StoryWord);
        WorldAutoPlayTarget current = scene.DiscoverAutoPlayEntrances().SingleOrDefault(item => item.Id == target.Id) ?? throw new InvalidOperationException("The requested world entrance is no longer reachable.");
        if (current != target)
        {
            throw new InvalidOperationException("The requested world entrance differs from its current discovery.");
        }
        IReadOnlyList<(int X, int RawZ)> path = scene.GetAutoPlayEntrancePath(current);
        int callbacks = 0;
        int entryHold = NativeWorldEntryControl.DelayAfterCallback(scene.State.Frame, 0);
        if (entryHold > 0)
        {
            PlayerActionResult wait = Apply(new PlayerAction(PlayerActionKind.Wait, Frames: entryHold));
            callbacks = wait.FramesAdvanced;
            if (!wait.Accepted || wait.Trigger.Kind != PlayerTriggerKind.WorldMap || scene.State.EventUi is { ScriptMode: not 0 })
            {
                return FinishWorldTravel(scene, target, wait);
            }
        }
        int stride = 1;
        HashSet<(int X, int RawZ)> avoidedCrossings = [];
        int waypoint = 0;
        while (waypoint < path.Count)
        {
            int stalled = 0;
            long bestDistance = long.MaxValue;
            while (true)
            {
                WorldState before = scene.State;
                int x = WorldMapScene.NearWrapped(path[waypoint].X, before.X, 262144);
                int z = WorldMapScene.NearWrapped(path[waypoint].RawZ, before.Z, 196608);
                long distance = WorldDistanceSquared(before.X, before.Z, x, z);
                if (distance <= (long)stride * stride)
                {
                    waypoint++;
                    break;
                }
                if (callbacks >= 30_000)
                {
                    return WorldTravelBlocked(scene, callbacks, "Ordinary world movement exceeded its bounded route callbacks.");
                }
                PlayerAction move = missionCar ? scene.SteerAutoPlayRental(x, z) : WorldNavigationGuide.Next(before, x, z);
                int? ordinaryStride = null;
                PlayerActionResult result = AutoPlayFastMovement && !missionCar ? scene.ApplyAutoPlayFootMove(x, z, () => Apply(move), out ordinaryStride) : Apply(move);
                callbacks += result.FramesAdvanced;
                if (!result.Accepted || result.FramesAdvanced == 0 || result.Trigger.Kind != PlayerTriggerKind.WorldMap || scene.State.EventUi is { ScriptMode: not 0 })
                {
                    return FinishWorldTravel(scene, target, result with { FramesAdvanced = callbacks });
                }
                WorldState after = scene.State;
                int nextX = WorldMapScene.NearWrapped(after.X, before.X, 262144);
                int nextZ = WorldMapScene.NearWrapped(after.Z, before.Z, 196608);
                long traveled = WorldDistanceSquared(before.X, before.Z, nextX, nextZ);
                if (traveled != 0)
                {
                    stride = ordinaryStride ?? Math.Max(1, (int)Math.Ceiling(Math.Sqrt(traveled)));
                }
                long remaining = WorldDistanceSquared(nextX, nextZ, x, z);
                if (remaining < Math.Min(bestDistance, distance))
                {
                    bestDistance = remaining;
                    stalled = 0;
                    onProgress?.Invoke();
                }
                else if (missionCar && move.Horizontal != 0 && traveled == 0 && before.BodyYaw != after.BodyYaw)
                {
                    stalled = 0;
                }
                else if (++stalled >= 8)
                {
                    var crossing = path[waypoint];
                    if (after.NativeFootRuntime is null && !missionCar || avoidedCrossings.Count >= 12 || !avoidedCrossings.Add(crossing))
                    {
                        return WorldTravelBlocked(scene, callbacks, $"Ordinary world movement could not reach crossing ({crossing.X}, {crossing.RawZ}) after {avoidedCrossings.Count} replans.");
                    }
                    while (missionCar && scene.State.NativeCarRuntime is { } car && (!WorldMapScene.IsAutoPlayCarStationary(car) || car.CameraTransition is { Flags: not 0 }))
                    {
                        if (callbacks >= 30_000)
                        {
                            return WorldTravelBlocked(scene, callbacks, "Ordinary world movement exceeded its bounded route callbacks while stopping the car.");
                        }
                        PlayerActionResult wait = Apply(new PlayerAction(PlayerActionKind.Wait));
                        callbacks += wait.FramesAdvanced;
                        if (!wait.Accepted || wait.FramesAdvanced == 0 || wait.Trigger.Kind != PlayerTriggerKind.WorldMap || scene.State.EventUi is { ScriptMode: not 0 })
                        {
                            return FinishWorldTravel(scene, target, wait with { FramesAdvanced = callbacks });
                        }
                    }
                    if (scene.ReplanAutoPlayEntrance(target, avoidedCrossings) is not { } detour)
                    {
                        return WorldTravelBlocked(scene, callbacks, $"Ordinary world movement could not reach crossing ({crossing.X}, {crossing.RawZ}) after {avoidedCrossings.Count} replans.");
                    }
                    path = detour;
                    waypoint = 0;
                    break;
                }
            }
        }
        int frames = Math.Max(1, NativeWorldEntryControl.DelayAfterCallback(scene.State.Frame, 0));
        PlayerActionResult arrival = Apply(new PlayerAction(PlayerActionKind.Wait, Frames: frames));
        if (arrival.Accepted && arrival.Trigger.Kind == PlayerTriggerKind.WorldMap && scene.State.EventUi is not { ScriptMode: not 0 })
        {
            return WorldTravelBlocked(scene, callbacks + arrival.FramesAdvanced, "The native terrain crossing did not activate a world entrance.");
        }
        return FinishWorldTravel(scene, target, arrival with { FramesAdvanced = callbacks + arrival.FramesAdvanced });
    }

    private WorldAutoPlayTravelResult FinishWorldTravel(WorldMapScene scene, WorldAutoPlayTarget target, PlayerActionResult result)
    {
        bool matched = result.Accepted && (result.Trigger.Kind == PlayerTriggerKind.FieldTransition ? result.Trigger.FieldId == target.DestinationField : result.Trigger.Kind != PlayerTriggerKind.Battle && session.State.Scene == SceneKind.WorldMap && scene.IsAtAutoPlayEntrance(target));
        return new(result, matched);
    }

    private WorldAutoPlayTravelResult WorldTravelBlocked(WorldMapScene scene, int frames, string detail)
    {
        return new(new(false, new(PlayerTriggerKind.WorldMap, session.State.FieldId, scene.State.Frame, null, null), frames, detail), false);
    }

    private static long WorldDistanceSquared(int x, int z, int targetX, int targetZ)
    {
        long dx = targetX - (long)x;
        long dz = targetZ - (long)z;
        return dx * dx + dz * dz;
    }
}
