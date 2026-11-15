using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Checks the callback owners used by world presentation, independently of the selected body and camera proximity.</summary>
internal static class WorldPresentationReadiness
{
    public static bool IsDeferred(WorldState state)
    {
        if (state.NativeFootRuntime is { StationTrains: { } source, StationTrainProcess.ProjectionCamera: null })
        {
            return source.Callback == 0;
        }
        if (state.NativeTrainRuntime is { } train)
        {
            return train.Callback == 0;
        }
        if (state.NativeScriptedRuntime is { } scripted)
        {
            return scripted.Path.Callback == 0;
        }
        if (state.NativeRagnarokRuntime is { } ragnarok)
        {
            return IsDeferred(ragnarok.BackgroundRails);
        }
        if (state.NativeGardenRuntime is { } garden)
        {
            return IsDeferred(garden.BackgroundRails);
        }
        if (state.NativeChocoboRuntime is { } chocobo)
        {
            return IsDeferred(chocobo.Objects.BackgroundRails);
        }
        if (state.NativeCarRuntime is { } car)
        {
            return IsDeferred(car);
        }
        if (state.NativeFootRuntime?.ParkedCar is { } parked)
        {
            return IsDeferred(parked.Car);
        }
        return state.NativeFootRuntime?.StationTrains is null && IsDeferred(state.NativeFootRuntime?.NormalObjects);
    }

    private static bool IsDeferred(NativeCarWorldRuntimeState car)
    {
        if (car.RagnarokObjects is { } ragnarok)
        {
            return IsDeferred(ragnarok.BackgroundRails);
        }
        if (car.GardenObjects is { } garden)
        {
            return IsDeferred(garden.BackgroundRails);
        }
        return IsDeferred(car.ObjectProcess);
    }

    private static bool IsDeferred(NativeWorldBackgroundRailState? rails)
    {
        return rails is { Callback: 0, HasBanks: true };
    }

    private static bool IsDeferred(NativeNormalFootObjectProcess? objects)
    {
        return objects is { Callback: 0 } && (objects.Primary is not null || objects.Alternate is not null);
    }
}
