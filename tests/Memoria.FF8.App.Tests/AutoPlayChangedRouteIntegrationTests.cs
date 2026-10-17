using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayChangedRouteIntegrationTests
{
    [Fact]
    public void AChangedNativeChoiceRetiresOnlyItsRecordedRouteAndBeginsAnOrdinaryCheckpointedAlternative()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayChangedRouteIntegrationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(177, run.Session.State.FieldId);
        Assert.Equal(301, run.World.Events.Frame);
        Assert.False(run.World.Events.IsUserControlEnabled);
        int[] party = run.World.Events.Scene.Party.ToArray();
        AutoPlayEventRoute oldRoute = Assert.IsType<AutoPlayEventRoute>(run.Controller.State.ActiveTravelRoute);
        AutoPlayChoiceSelection[] recordedSelections = oldRoute.Selections!.ToArray();
        AutoPlayChoiceSelection[] prefix = run.Controller.State.ActiveSelections!.ToArray();
        Assert.Single(prefix);
        Assert.StartsWith("input:165:", prefix[0].Key);
        Assert.StartsWith("input:177:103859", recordedSelections[prefix.Length].Key);
        ScriptedInputMetadata input = Assert.IsType<ScriptedInputMetadata>(run.World.Events.DiscoverScriptedInput());
        Assert.StartsWith("E366BBAB", input.Fingerprint);
        Assert.Equal(16, input.Masks[0]);
        string key = $"input:177:{input.Fingerprint}";
        ScriptedInputMetadata recordedInput = saved.ScriptedChoiceInputs[recordedSelections[prefix.Length].Key];
        Assert.Equal(recordedInput.Masks, input.Masks);
        AutoPlayEventCoverage Entry(InstalledRun current) => current.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "165:16").Events.Single(item => item.Event.Id == "165:line:26");
        string conditions = JsonSerializer.Serialize(Entry(run).Conditions, AutoPlayController.JsonOptions);
        bool completed = Entry(run).Completed;
        int completedOptions = run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count);
        int routes = Entry(run).Routes.Count;
        int retired = Entry(run).RetiredRoutes.Count;
        string routeJson = JsonSerializer.Serialize(oldRoute, AutoPlayController.JsonOptions);
        string[] inspected = run.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(inspected);

        run.Controller.Advance(1);

        Assert.Null(run.Controller.Error);
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.Controller.Running);
        Assert.Equal(inspected, run.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal));
        Assert.Null(run.Controller.State.ActiveTravelRoute);
        Assert.Null(run.Controller.State.RefreshTargetVisit);
        Assert.True(Entry(run).RouteNeedsRediscovery);
        Assert.Equal(routes - 1, Entry(run).Routes.Count);
        Assert.Equal(retired + 1, Entry(run).RetiredRoutes.Count);
        AutoPlayRetiredRoute retirement = Entry(run).RetiredRoutes[^1];
        Assert.Equal(routeJson, JsonSerializer.Serialize(retirement.Route, AutoPlayController.JsonOptions));
        key = retirement.ActualChoiceKey;
        ScriptedInputMetadata scoped = run.Controller.State.ScriptedChoiceInputs[key];
        Assert.True(scoped.ObservedQueriesProven);
        Assert.Equal($"input:177:{scoped.Fingerprint}", key);
        Assert.All(scoped.Queries, query => Assert.Contains(query, input.Queries));
        Assert.Equal(0, retirement.FirstChoice);
        Assert.Equal(scoped.Masks.Count - 1, retirement.LastChoice);
        Assert.Equal(prefix, retirement.ExecutedPrefix);
        Assert.DoesNotContain(Entry(run).Routes, route => JsonSerializer.Serialize(route, AutoPlayController.JsonOptions) == routeJson);
        Assert.Equal(completed, Entry(run).Completed);
        Assert.Equal(conditions, JsonSerializer.Serialize(Entry(run).Conditions, AutoPlayController.JsonOptions));
        Assert.Equal(completedOptions, run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count));
        AutoPlayChoiceCoverage choice = Assert.Single(run.Controller.State.Coverage.Choices, item => item.Key == key);
        Assert.Equal(0, choice.ActiveOption);
        Assert.Empty(choice.CompletedOptions);
        Assert.False(choice.Finished);
        Assert.True(choice.InputCheckpointPrepared);
        Assert.Equal(prefix.Append(new AutoPlayChoiceSelection(key, 0)), run.Controller.State.ActiveSelections);
        Assert.DoesNotContain(run.Controller.State.ActiveSelections!, selection => selection.Key == recordedSelections[prefix.Length].Key);
        Assert.DoesNotContain(run.Queries, sample => sample.Input.PhysicalHeld == recordedInput.Masks[recordedSelections[prefix.Length].Selection]);
        Assert.Contains(run.Queries, sample => sample.Input.PhysicalHeld == 16 && sample.Input.SampledFrame == 302 && input.Queries.Any(query => query.ObjectId == sample.Command.ObjectId && query.ScriptId == sample.Command.ScriptId && query.Operation == sample.Command.Operation && query.Opcode == sample.Command.Name && query.Mask == sample.Command.Arguments[0]) && (sample.Command.Arguments[0] & sample.Input.Held.GetValueOrDefault()) != 0);
        string beforePath = Assert.Single(Directory.GetFiles(run.Output, choice.BeforeCheckpoint!, SearchOption.AllDirectories));
        GameState before = GameSession.Load(beforePath);
        Assert.Equal(301, before.EventState!.Value.GetProperty("Frame").GetInt64());
        Assert.True(AutoPlayController.HasNeutralScriptedInput(before.FieldButtonInput, 177, 301));
        Assert.Equal(18, run.Session.State.EventMemory.KnownValues[434]);
        Assert.Equal(party, run.World.Events.Scene.Party);
        Assert.All(run.PartySamples, sample => Assert.Equal(party, sample));
        string retirements = JsonSerializer.Serialize(Entry(run).RetiredRoutes, AutoPlayController.JsonOptions);
        AutoPlayChoiceSelection[] actualPrefix = run.Controller.State.ActiveSelections!.ToArray();
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(retirements, JsonSerializer.Serialize(Entry(cold).RetiredRoutes, AutoPlayController.JsonOptions));
        Assert.True(Entry(cold).RouteNeedsRediscovery);
        Assert.Equal(actualPrefix, cold.Controller.State.ActiveSelections);
        Assert.Null(cold.Controller.State.ActiveTravelRoute);
        AutoPlayChoiceCoverage restored = Assert.Single(cold.Controller.State.Coverage.Choices, item => item.Key == key);
        Assert.Equal(0, restored.ActiveOption);
        Assert.Empty(restored.CompletedOptions);
        Assert.Equal(run.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-changed-route-" + Guid.NewGuid().ToString("N"));
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
