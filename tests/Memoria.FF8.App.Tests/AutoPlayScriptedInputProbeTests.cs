using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayScriptedInputProbeTests
{
    [Fact]
    public void AProbeCheckpointRequiresAnExactCurrentFullyNeutralResolvedSample()
    {
        NativeFieldButtonInput neutral = new(3, 0, 0, 315, 235, 0, 0, 0, 0);
        neutral.Validate();
        Assert.True(AutoPlayController.HasNeutralScriptedInput(neutral, 235, 315));
        (string Reason, NativeFieldButtonInput? Input)[] unavailable =
        [
            ("Missing sampler", null),
            ("Unsampled process", NativeFieldButtonInput.FromColdProcess()),
            ("Previous callback", neutral with { SampledFrame = 314 }),
            ("Future callback", neutral with { SampledFrame = 316 }),
            ("Another field", neutral with { SampledFieldId = 234 }),
            ("Physical request not sampled", neutral with { PhysicalHeld = 16 }),
            ("Pending logical pulse", neutral with { PendingPulse = 16 }),
            ("Raw held sample", neutral with { RawHeld = 16 }),
            ("Raw rising edge", neutral with { RawHeld = 16, RawPressed = 16 }),
            ("Mapped held sample", neutral with { RawHeld = 16, Held = 16 }),
            ("Mapped rising edge", neutral with { RawHeld = 16, RawPressed = 16, Held = 16, Pressed = 16 }),
            ("Unresolved mapping", neutral with { Held = null, Pressed = null })
        ];
        foreach ((string reason, NativeFieldButtonInput? input) in unavailable)
        {
            input?.Validate();
            Assert.False(AutoPlayController.HasNeutralScriptedInput(input, 235, 315), reason);
        }
    }

    [Fact]
    public void ARealQueryProbePreservesItsNeutralBeforeStateAndSamplesTheOriginalPhysicalAlternative()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayScriptedInputProbeTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(235, run.Session.State.FieldId);
        Assert.Equal(315, run.World.Events.Frame);
        Assert.False(run.World.Events.IsUserControlEnabled);
        int[] party = run.World.Events.Scene.Party.ToArray();
        ScriptedInputMetadata input = Assert.IsType<ScriptedInputMetadata>(run.World.Events.DiscoverScriptedInput());
        Assert.StartsWith("2E9AC", input.Fingerprint);
        Assert.Equal(16, input.Masks[0]);
        string key = $"input:235:{input.Fingerprint}";
        Assert.DoesNotContain(run.Controller.State.Coverage.Choices, choice => choice.Key == key);
        string? activeEvent = run.Controller.State.ActiveEvent;

        run.Controller.Advance(1);

        Assert.Null(run.Controller.Error);
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.Controller.Running);
        Assert.Equal(235, run.Session.State.FieldId);
        Assert.False(run.World.Events.IsUserControlEnabled);
        Assert.Equal(activeEvent, run.Controller.State.ActiveEvent);
        Assert.Equal(party, run.World.Events.Scene.Party);
        Assert.All(run.PartySamples, sample => Assert.Equal(party, sample));
        Assert.Contains(run.Queries, sample => sample.Command is { FieldId: 235, Name: "KEYSCAN" } && sample.Input is { SampledFieldId: 235, SampledFrame: 316, PhysicalHeld: 16 } && input.Queries.Any(query => query.ObjectId == sample.Command.ObjectId && query.ScriptId == sample.Command.ScriptId && query.Operation == sample.Command.Operation && query.Opcode == sample.Command.Name && query.Mask == sample.Command.Arguments[0]) && (sample.Command.Arguments[0] & sample.Input.Held.GetValueOrDefault()) != 0);
        AutoPlayChoiceCoverage choice = Assert.Single(run.Controller.State.Coverage.Choices, item => item.Key.StartsWith("input:235:", StringComparison.Ordinal) && saved.Coverage.Choices.All(previous => previous.Key != item.Key));
        key = choice.Key;
        Assert.Equal(0, choice.ActiveOption);
        Assert.Empty(choice.CompletedOptions);
        Assert.False(choice.Finished);
        Assert.True(choice.InputCheckpointPrepared);
        ScriptedInputMetadata observed = run.Controller.State.ScriptedChoiceInputs[key];
        Assert.True(observed.ObservedQueriesProven);
        Assert.Equal($"input:235:{observed.Fingerprint}", key);
        Assert.All(observed.Queries, query => Assert.Contains(query, input.Queries));
        string beforePath = Assert.Single(Directory.GetFiles(run.Output, choice.BeforeCheckpoint!, SearchOption.AllDirectories));
        GameState before = GameSession.Load(beforePath);
        Assert.Equal(315, before.EventState!.Value.GetProperty("Frame").GetInt64());
        Assert.True(AutoPlayController.HasNeutralScriptedInput(before.FieldButtonInput, 235, 315));
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(run.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
        AutoPlayChoiceCoverage restored = Assert.Single(cold.Controller.State.Coverage.Choices, item => item.Key == key);
        Assert.Equal(0, restored.ActiveOption);
        Assert.Empty(restored.CompletedOptions);
        Assert.Empty(cold.Queries);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-scripted-input-probe-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<(ScriptCommand Command, NativeFieldButtonInput Input)> Queries { get; } = [];
        public List<int[]> PartySamples { get; } = [];

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    observed.Events.CommandExecuted += command =>
                    {
                        PartySamples.Add(observed.Events.Scene.Party.ToArray());
                        if (command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON")
                        {
                            Queries.Add((command, Session.State.FieldButtonInput!));
                        }
                    };
                }
                return world;
            }
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
