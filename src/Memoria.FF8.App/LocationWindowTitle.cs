using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class LocationWindowTitle
{
    public static string Format(GameState state, SceneEventState? scene, ActorState? player, long fieldFrame, int speedMultiplier, int? worldPlaceId = null, string? fieldName = null)
    {
        string location = state.Scene switch
        {
            SceneKind.WorldMap when state.World is { } world => FormattableString.Invariant($"World S{WorldAssets.SegmentAt(world.X, world.Z)} P{worldPlaceId?.ToString(CultureInfo.InvariantCulture) ?? "?"} XYZ({world.X},{world.NativeHeight},{world.Z}) E{world.NativeFootRuntime?.EntityType ?? world.NativeCarRuntime?.EntityType ?? (world.NativeRagnarokRuntime is not null ? 50 : world.NativeGardenRuntime is not null ? 48 : state.WorldArrival?.Mode ?? -1)} R{world.ReplacementMask:X} F{world.Frame}"),
            SceneKind.Field or SceneKind.Movie => $"{state.Scene} {state.FieldId}:{fieldName ?? state.FieldName} C{scene?.CameraIndex.ToString(CultureInfo.InvariantCulture) ?? "?"} P{scene?.PlaceId.ToString(CultureInfo.InvariantCulture) ?? "?"}{(player is null ? "" : FormattableString.Invariant($" XYZ({player.X:0},{player.Y:0},{player.Z:0}) T{player.WalkmeshTriangle}"))} F{fieldFrame}{(state.Scene == SceneKind.Movie ? $" M{state.MovieIndex}" : "")}",
            _ => state.Scene.ToString()
        };
        return $"Memoria FFVIII | {location}{(speedMultiplier == 1 ? "" : $" | x{speedMultiplier}")}";
    }
}

internal sealed partial class RecreateGame
{
    private void UpdateLocationWindowTitle()
    {
        string title = LocationWindowTitle.Format(session.State, events?.Scene, events?.PlayerActor, events?.Frame ?? 0, gameplayClock.SpeedMultiplier, session.State.Scene == SceneKind.WorldMap ? world?.WorldScene?.PlaceId : null, field?.Id == session.State.FieldId ? field.Name : null);
        if (Window.Title != title)
        {
            Window.Title = title;
        }
    }
}
