using System.Runtime.CompilerServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldPresentationReadinessTests
{
    public static IEnumerable<object[]> RailOwners()
    {
        foreach (string owner in new[] { "foot", "car", "car-garden", "car-ragnarok", "parked-car", "parked-car-garden", "parked-car-ragnarok", "garden", "ragnarok", "chocobo" })
        {
            foreach (long callback in new long[] { 0, 1 })
            {
                foreach (int banks in new[] { 0, 1, 2, 3 })
                {
                    yield return new object[] { owner, callback, banks };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(RailOwners))]
    public void Rail_owner_waits_for_its_first_callback_only_when_a_bank_exists(string owner, long callback, int banks)
    {
        WorldState state = CreateRailOwner(owner, callback, banks);

        Assert.Equal(callback == 0 && banks != 0, WorldPresentationReadiness.IsDeferred(state));
        Assert.Equal(callback == 0 && banks != 0, WorldPresentationReadiness.IsDeferred(state with { Frame = 123 }));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Scripted_and_riding_train_allocations_wait_for_their_actual_first_callback(long callback, bool deferred)
    {
        NativeWorldScriptedPathState path = Partial<NativeWorldScriptedPathState>() with { Callback = callback };
        NativeScriptedWorldRuntimeState scripted = Partial<NativeScriptedWorldRuntimeState>() with { Path = path };
        NativeTrainWorldRuntimeState train = Partial<NativeTrainWorldRuntimeState>() with { Callback = callback };

        Assert.Equal(deferred, WorldPresentationReadiness.IsDeferred(new WorldState { NativeScriptedRuntime = scripted }));
        Assert.Equal(deferred, WorldPresentationReadiness.IsDeferred(new WorldState { NativeTrainRuntime = train }));
    }

    [Fact]
    public void Absent_optional_rail_owners_do_not_defer_presentation()
    {
        Assert.False(WorldPresentationReadiness.IsDeferred(new WorldState()));
        Assert.False(WorldPresentationReadiness.IsDeferred(new WorldState { NativeFootRuntime = Partial<NativeFootWorldRuntimeState>() }));
        Assert.False(WorldPresentationReadiness.IsDeferred(new WorldState { NativeCarRuntime = Partial<NativeCarWorldRuntimeState>() }));
        Assert.False(WorldPresentationReadiness.IsDeferred(new WorldState { NativeGardenRuntime = Partial<NativeGardenWorldRuntimeState>() }));
        Assert.False(WorldPresentationReadiness.IsDeferred(new WorldState { NativeRagnarokRuntime = Partial<NativeRagnarokWorldRuntimeState>() }));
    }

    [Fact]
    public void Readiness_follows_the_owner_selected_for_rendering()
    {
        WorldState deferredFoot = CreateRailOwner("foot", 0, 3);
        NativeTrainWorldRuntimeState train = Partial<NativeTrainWorldRuntimeState>() with { Callback = 1 };
        NativeWorldScriptedPathState path = Partial<NativeWorldScriptedPathState>() with { Callback = 1 };

        Assert.False(WorldPresentationReadiness.IsDeferred(deferredFoot with { NativeTrainRuntime = train }));
        Assert.False(WorldPresentationReadiness.IsDeferred(deferredFoot with { NativeScriptedRuntime = Partial<NativeScriptedWorldRuntimeState>() with { Path = path } }));
        Assert.False(WorldPresentationReadiness.IsDeferred(deferredFoot with { NativeGardenRuntime = Partial<NativeGardenWorldRuntimeState>() }));
        Assert.False(WorldPresentationReadiness.IsDeferred(deferredFoot with { NativeRagnarokRuntime = Partial<NativeRagnarokWorldRuntimeState>() }));
        Assert.False(WorldPresentationReadiness.IsDeferred(CreateRailOwner("car", 0, 3) with { NativeRagnarokRuntime = Partial<NativeRagnarokWorldRuntimeState>() }));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Station_train_without_a_foot_camera_uses_the_retained_riding_callback(long callback, bool deferred)
    {
        NativeTrainWorldRuntimeState source = Partial<NativeTrainWorldRuntimeState>() with { Callback = callback };
        NativeFootWorldRuntimeState foot = Partial<NativeFootWorldRuntimeState>() with { StationTrains = source, StationTrainProcess = new(1, 0, null!, null, null) };

        Assert.Equal(deferred, WorldPresentationReadiness.IsDeferred(new WorldState { NativeFootRuntime = foot }));
    }

    [Fact]
    public void Station_foot_camera_keeps_its_inherited_presentation_when_the_local_callback_is_zero()
    {
        NativeTrainWorldRuntimeState source = Partial<NativeTrainWorldRuntimeState>() with { Callback = 1 };
        NativeFootWorldRuntimeState foot = Partial<NativeFootWorldRuntimeState>() with { StationTrains = source, StationTrainProcess = new(1, 0, null!, null, Partial<NativeWorldFootCameraState>()) };

        Assert.False(WorldPresentationReadiness.IsDeferred(new WorldState { NativeFootRuntime = foot }));
    }

    private static WorldState CreateRailOwner(string owner, long callback, int banks)
    {
        NativeTrainRailBank? primary = (banks & 1) != 0 ? new(0, "", 1, 30, 0, null, 0, [], []) : null;
        NativeTrainRailBank? alternate = (banks & 2) != 0 ? new(1, "", 1, 30, 0, null, 0, [], []) : null;
        NativeNormalFootObjectProcess process = new(1, null!, callback, primary, alternate, null!, null, null);
        NativeWorldBackgroundRailState rails = new(1, 0, callback, primary, alternate, null!, null, null);
        NativeGardenWorldRuntimeState garden = Partial<NativeGardenWorldRuntimeState>() with { BackgroundRails = rails };
        NativeRagnarokWorldRuntimeState ragnarok = Partial<NativeRagnarokWorldRuntimeState>() with { BackgroundRails = rails };
        NativeCarWorldRuntimeState car = Partial<NativeCarWorldRuntimeState>();
        NativeFootWorldRuntimeState foot = Partial<NativeFootWorldRuntimeState>();
        return owner switch
        {
            "foot" => new WorldState { NativeFootRuntime = foot with { NormalObjects = process } },
            "car" => new WorldState { NativeCarRuntime = car with { ObjectProcess = process } },
            "car-garden" => new WorldState { NativeCarRuntime = car with { GardenObjects = garden } },
            "car-ragnarok" => new WorldState { NativeCarRuntime = car with { RagnarokObjects = ragnarok } },
            "parked-car" => new WorldState { NativeFootRuntime = foot with { ParkedCar = new(car with { ObjectProcess = process }, default, 0, 0) } },
            "parked-car-garden" => new WorldState { NativeFootRuntime = foot with { ParkedCar = new(car with { GardenObjects = garden }, default, 0, 0) } },
            "parked-car-ragnarok" => new WorldState { NativeFootRuntime = foot with { ParkedCar = new(car with { RagnarokObjects = ragnarok }, default, 0, 0) } },
            "garden" => new WorldState { NativeGardenRuntime = garden },
            "ragnarok" => new WorldState { NativeRagnarokRuntime = ragnarok },
            "chocobo" => new WorldState { NativeChocoboRuntime = new(garden, null!, null) },
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };
    }

    // Readiness observes only owner selection, callback age and bank presence; resource validation belongs to scene tests.
    private static T Partial<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}
