using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldVibrationTransportTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void CarLatchSamplesPriorGraphicsBeforeBlockedCurrentMotionAndSalaryUnits()
    {
        using WorldAssets assets = new(GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        // Declared transaction component, not a loaded world or a research route.
        GameState departure = session.State with { Scene = SceneKind.WorldMap, World = new WorldState { Frame = 10 } };
        NativeWorldSalaryState start = NativeWorldSalaryState.Begin(departure);
        int rawMagnitude = NativeWorldVectorMagnitude.Truncate(31, 0, 8);
        uint blockedMovement = NativeWorldSalaryMovement.AcceptedVectorUnits(0, 0, 0);
        Assert.Equal(32, rawMagnitude);
        Assert.Equal(0u, blockedMovement);

        // 550070 observes the previous20409F0 before53FAC0 publishes this vector.
        NativeWorldSalaryState first = start.PrepareCarVibration(33, assets.VibrationBank).WithGraphicsSpeed(rawMagnitude).Advance(null, start.Presentation, start.SoundProcess, blockedMovement, 2);
        Assert.Equal(-1, first.VibrationProcess!.CarHandleC76788);
        Assert.Equal(0, first.VibrationProcess.PreviousCarSpeed2045C84);
        Assert.Equal(32, first.VibrationProcess.GraphicsSpeed20409F0);
        Assert.Equal(0u, first.MovementUnits);

        NativeWorldSalaryState second = first.PrepareCarVibration(33, assets.VibrationBank).WithGraphicsSpeed(0).Advance(null, first.Presentation, first.SoundProcess, 0, 2);
        Assert.Equal(32, second.VibrationProcess!.CarHandleC76788);
        Assert.Equal((byte)255, second.VibrationProcess.Slots[0].Priority);
        Assert.Equal((ushort)2, second.VibrationProcess.Slots[0].Generation);
        Assert.Equal(32, second.VibrationProcess.PreviousCarSpeed2045C84);
        Assert.Equal(0, second.VibrationProcess.GraphicsSpeed20409F0);
        Assert.Equal(0, second.VibrationProcess.QuarterClockRemainder);
        Assert.Equal((short)0, second.VibrationProcess.Slots[0].A.Counter);

        NativeWorldSalaryState stopped = second.PrepareCarVibration(33, assets.VibrationBank).WithGraphicsSpeed(32).Advance(null, second.Presentation, second.SoundProcess, 0, 2);
        Assert.Equal(0, stopped.VibrationProcess!.PreviousCarSpeed2045C84);
        NativeWorldSalaryState restarted = stopped.PrepareCarVibration(33, assets.VibrationBank);
        Assert.Equal((ushort)2, restarted.VibrationProcess!.Slots[0].Generation);
        Assert.Equal(32, restarted.VibrationProcess.PreviousCarSpeed2045C84);
        Assert.Equal(32, restarted.VibrationProcess.CarHandleC76788);
        Assert.Same(departure.VibrationProcess, start.VibrationProcess);
        Assert.Equal(0, departure.VibrationProcess!.GraphicsSpeed20409F0);
        Assert.Equal(-1, departure.VibrationProcess.CarHandleC76788);
    }

    [Fact]
    public void PreparedWorldOwnersRequireActualCallbackCountAndPublishWithoutChangingDeparture()
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState departure = session.State with { Scene = SceneKind.WorldMap, World = new WorldState { Frame = 10 } };
        NativeWorldSalaryState start = NativeWorldSalaryState.Begin(departure);
        NativeWorldSalaryState prospective = start.WithGraphicsSpeed(NativeWorldVectorMagnitude.Truncate(22, 0, 22)).Advance(null, start.Presentation, start.SoundProcess, 3, 2);
        string before = JsonSerializer.Serialize(departure.VibrationProcess);
        Assert.Throws<InvalidDataException>(() => prospective.ApplyTo(departure, departure.World!));
        Assert.Equal(before, JsonSerializer.Serialize(departure.VibrationProcess));

        WorldState final = departure.World! with { Frame = 11 };
        GameState published = prospective.ApplyTo(departure with { World = final }, final);
        Assert.Equal(31, published.VibrationProcess!.GraphicsSpeed20409F0);
        Assert.Equal(2, published.VibrationProcess.QuarterClockRemainder);
        Assert.Same(prospective.VibrationProcess, published.VibrationProcess);
        Assert.Equal(before, JsonSerializer.Serialize(departure.VibrationProcess));

        session.StartNewGame();
        GameState live = session.State;
        NativeWorldControlState changedControls = live.WorldControls!.WithSaveEnabled(1);
        NativeVibrationProcessState invalid = published.VibrationProcess with { GraphicsSpeed20409F0 = -1 };
        Assert.Throws<InvalidDataException>(() => session.SetNativeEventOwners(changedControls, vibrationProcess: invalid));
        Assert.Same(live, session.State);
        Assert.Throws<InvalidDataException>(() => session.SetVibrationProcess(invalid));
        Assert.Same(live, session.State);
        session.SetNativeEventOwners(changedControls, vibrationProcess: published.VibrationProcess);
        Assert.Same(published.VibrationProcess, session.VibrationProcess);
        Assert.Same(changedControls, session.WorldControls);
        Assert.Equal((byte)0, live.WorldControls.Menu209);
    }

    [Fact]
    public void UnknownWorldVibrationCannotSupplyGraphicsOrCarSamplingAndNoncarDoesNotTouchLatch()
    {
        using WorldAssets assets = new(GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        GameState unknown = session.State with { VibrationProcess = null };
        NativeWorldSalaryState absent = NativeWorldSalaryState.Begin(unknown);
        Assert.Throws<NotSupportedException>(() => absent.WithGraphicsSpeed(0));
        Assert.Throws<NotSupportedException>(() => absent.PrepareCarVibration(33, assets.VibrationBank));
        Assert.Null(absent.VibrationProcess);

        NativeVibrationProcessState retained = session.VibrationProcess! with { GraphicsSpeed20409F0 = 7, PreviousCarSpeed2045C84 = 19 };
        NativeWorldSalaryState known = NativeWorldSalaryState.Begin(session.State with { VibrationProcess = retained });
        Assert.Same(retained, known.PrepareCarVibration(0, assets.VibrationBank).VibrationProcess);
        Assert.Throws<InvalidDataException>(() => known.WithGraphicsSpeed(-1));
        Assert.Same(retained, known.VibrationProcess);
    }
}
