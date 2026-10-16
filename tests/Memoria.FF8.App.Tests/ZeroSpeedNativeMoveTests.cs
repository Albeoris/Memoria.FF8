using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ZeroSpeedNativeMoveTests
{
    [Theory]
    [InlineData(1, 0, FieldNativeScriptDirectionStatus.Snapped)]
    [InlineData(2, 0, FieldNativeScriptDirectionStatus.Moving)]
    [InlineData(64, 1, FieldNativeScriptDirectionStatus.Stopped)]
    public void ZeroSpeedUsesNativeStopThenStrictSnapRules(int targetX, short stopDistance, FieldNativeScriptDirectionStatus expected)
    {
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(0, 0, targetX << 12, 0, 0, stopDistance, 48, 0, 0, 0, 0));
        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == FieldNativeScriptDirectionStatus.Snapped ? targetX << 12 : 0, result.FixedX);
    }

    [Fact]
    public void ActualRinoaReturnCanWriteZeroMspeedOverRetainedMovementAndRestore()
    {
        GameSession session = new(GameSession.Load(FindRootFile("../fixtures/external-fixture.json")));
        using GameWorld world = new(session, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        bool executed = false;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "MSPEED" && command.Arguments.Single() == 0)
            {
                executed = true;
            }
        };
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.True(executed);
        Assert.Null(world.Events.LastFault);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-zero-MSpeed-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    [Fact]
    public void Actual623ZeroSpeedMoveRetainsNativeTargetAndRestoresStationaryCallback()
    {
        GameSession session = new(GameSession.Load(FindRootFile("../fixtures/external-fixture.json")));
        using GameWorld world = new(session, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Equal(623, session.State.FieldId);
        Assert.Null(world.Events.LastFault);
        ActorState actor = world.Events.Actors[0];
        NativeScriptMovementState movement = Assert.IsType<NativeScriptMovementState>(actor.NativeScriptMovement);
        Assert.True(movement.Active);
        Assert.Equal(0, movement.Speed);
        Assert.Equal(400 << 12, movement.TargetX);
        Assert.Equal(-182 << 12, movement.TargetY);
        Assert.Equal(0, movement.TargetZ);
        Assert.Equal((ushort)1, actor.NativeMovementCompletion542);
        Assert.Equal(201, actor.X);
        Assert.Equal(-184, actor.Y);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-zero-speed-MOVE-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(movement.FixedX, actor.NativeScriptMovement!.FixedX);
            Assert.Equal(movement.FixedY, actor.NativeScriptMovement.FixedY);
        }
        finally
        {
            File.Delete(snapshot);
        }
        for (int callback = 0; callback < 180 && actor.NativeMovementRequest is null; callback++)
        {
            world.Apply(new(PlayerActionKind.Wait));
        }
        NativeMovementRequestState request = Assert.IsType<NativeMovementRequestState>(actor.NativeMovementRequest);
        Assert.Equal(0, request.SavedMovement.Speed);
        Assert.Equal(movement.Operation, request.SavedMovement.Operation);
        session.SetEventState(world.Events.Capture());
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static string FindRootFile(string relative)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Actual standalone field623 source is absent.", relative);
    }
}
