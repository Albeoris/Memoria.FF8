using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ScriptedInterfaceClockTests
{
    [Theory]
    [InlineData(234, false)]
    [InlineData(235, true)]
    [InlineData(232, false)]
    public void Original_terminal_interfaces_keep_real_time_while_classroom_events_accelerate(int fieldId, bool scriptedInterface)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
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
