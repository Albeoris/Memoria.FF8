using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ScriptedInterfaceClockTests
{
    [Fact]
    public void Original_second_coupling_keeps_button_delays_with_visible_characters()
    {
        string snapshot = TestResources.FixturePath("App.Tests.ScriptedInterfaceClockTests.1");
        using GameWorld world = new(new GameSession(GameSession.Load(snapshot)), TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(903, world.Events.FieldId);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Contains(world.Events.Actors, actor => actor.Active && actor.Visible && actor.RenderModelId >= 0);
        Assert.NotNull(world.Events.DiscoverScriptedInput());
        Assert.True(world.Events.IsScriptedInterface);
        GameplayClock clock = new();
        clock.ToggleSpeed();
        TimeSpan sample = TimeSpan.FromTicks(333334);
        Assert.Equal(sample, clock.Advance(sample, SceneKind.Field, world.Events.IsScriptedInterface));
        Assert.Equal(3, clock.SpeedMultiplier);
    }

    [Theory]
    [InlineData("App.Tests.ScriptedInterfaceClockTests.2", false)]
    [InlineData("App.Tests.ScriptedInterfaceClockTests.3", true)]
    public void Original_train_briefing_accelerates_while_code_practice_keeps_real_time(string resource, bool scriptedInterface)
    {
        string snapshot = TestResources.FixturePath(resource);
        byte[] original = File.ReadAllBytes(snapshot);
        using GameWorld world = new(new GameSession(GameSession.Load(snapshot)), TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        EventEngine engine = world.Events;
        Assert.Equal(896, engine.FieldId);
        Assert.NotNull(engine.DiscoverScriptedInput());
        engine.Step();
        Assert.Equal(scriptedInterface, engine.IsScriptedInterface);

        GameplayClock clock = new();
        clock.ToggleSpeed();
        long initialFrame = engine.Frame;
        TimeSpan sample = TimeSpan.FromTicks(333334);
        for (int update = 0; update < 30; update++)
        {
            engine.Update(clock.Advance(sample, SceneKind.Field, engine.IsScriptedInterface));
            Assert.Equal(scriptedInterface, engine.IsScriptedInterface);
            Assert.Null(engine.LastFault);
        }
        Assert.Equal(scriptedInterface ? 30 : 90, engine.Frame - initialFrame);
        Assert.Equal(original, File.ReadAllBytes(snapshot));
    }
    [Theory]
    [InlineData(234, false)]
    [InlineData(235, true)]
    [InlineData(232, false)]
    public void Original_terminal_interfaces_keep_real_time_while_classroom_events_accelerate(int fieldId, bool scriptedInterface)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(fieldId);
        EventEngine engine = new(GameSession.CreateColdProcess());
        engine.LoadField(fieldId, field.Jsm, field.Messages, nativeRuntimeProfile: assets.NativeRuntimeProfile);
        if (fieldId == 235)
        {
            // Run the original synchronous allocation only; this fixture does not reconstruct a gameplay route.
            engine.Update(TimeSpan.FromSeconds(1d / EventEngine.SimulationFps));
        }
        JsmProgram program = JsmProgram.Read(field.Jsm);
        int polls = program.Operations.Count(operation => operation.Opcode.ToString() is "KEYSCAN" or "KEYSCAN2" or "KEYON");
        Assert.True(scriptedInterface == engine.IsScriptedInterface, $"field{fieldId}: actors={program.NativeActorCount}, models={field.Models.Count}, triangles={field.Walkmesh.Triangles.Count}, polls={polls}");

        GameplayClock clock = new();
        TimeSpan frame = TimeSpan.FromTicks(166667);
        TimeSpan ordinary = clock.Advance(frame, SceneKind.Field, engine.IsScriptedInterface);
        clock.ToggleSpeed();
        TimeSpan accelerated = clock.Advance(frame, SceneKind.Field, engine.IsScriptedInterface);
        Assert.Equal(scriptedInterface ? ordinary : ordinary * 3, accelerated);
    }
}
