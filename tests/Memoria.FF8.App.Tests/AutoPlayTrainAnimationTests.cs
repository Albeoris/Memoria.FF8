using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTrainAnimationTests
{
    [Fact]
    public void FreshNativeAllocationContinuesThroughAnimationAndItsOriginalChoices()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayTrainAnimationTests.1");
        AutoPlayRunState original = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        Dictionary<string, byte[]> originals = ReadFixtureBytes(manifest, original);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(263, run.Session.State.FieldId);
        Assert.Equal(205, run.World.Events.Variables.GetValueOrDefault(256));
        Assert.Null(run.Controller.State.ActiveEvent);
        AutoPlayEventCoverage? source = null;
        for (int step = 0; step < 32 && source is null; step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
            source = FindSourceEvent(run.Controller);
        }
        Assert.Equal(264, run.Session.State.FieldId);
        Assert.NotNull(source);
        Assert.False(source.Completed);
        string entrySignature = Assert.IsType<string>(source.Event.ConditionSignature);

        bool FinishedChoices()
        {
            AutoPlayChoiceCoverage[] choices = run.Controller.State.Coverage.Choices.Where(choice => IsOriginalChoice(choice.Key)).ToArray();
            return choices.Any(choice => choice.Key.StartsWith("264:205:3:94:1273:", StringComparison.Ordinal)) && choices.Any(choice => choice.Key.StartsWith("264:205:3:94:1320:", StringComparison.Ordinal)) && choices.Any(choice => choice.Key.StartsWith("264:205:3:94:1362:", StringComparison.Ordinal)) && choices.All(choice => choice.Finished && choice.CompletedOptions.Contains(2) && choice.CompletedOptions.Contains(3));
        }

        bool ReturnedControl()
        {
            AutoPlayEventCoverage item = FindSourceEvent(run.Controller)!;
            return run.AnimationObservationReturned && item.Completed && item.Conditions?.ConsumedEntries.Contains(entrySignature) == true && FinishedChoices() && run.Controller.State.PrimaryContinuation is null && run.Session.State.Scene == SceneKind.Field && run.World.Events.CanAcceptMovementInput;
        }

        for (int step = 0; step < 512 && !ReturnedControl(); step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
        }

        Assert.True(run.AnimationObservationIssued, "The original script did not reach its native animation observation.");
        Assert.True(run.AnimationObservationReturned, "The native observation did not advance its actual script owner.");
        Assert.True(ReturnedControl(), "The selected event and its original choices did not reach a genuine control-return boundary.");
        Assert.Contains(run.Outcomes, outcome => outcome is { FieldId: 264, ObjectId: 3, ScriptId: 94, Operation: 1273, Choice: 2 or 3 });
        Assert.Contains(run.Outcomes, outcome => outcome is { FieldId: 264, ObjectId: 3, ScriptId: 94, Operation: 1320, Choice: 2 or 3 });
        Assert.Contains(run.Outcomes, outcome => outcome is { FieldId: 264, ObjectId: 3, ScriptId: 94, Operation: 1362, Choice: 2 or 3 });
        Assert.False(run.Controller.State.ReachedEnding);
        foreach ((string path, byte[] bytes) in originals)
        {
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
    }

    private static bool IsOriginalChoice(string key)
    {
        return key.StartsWith("264:205:3:94:1273:", StringComparison.Ordinal) || key.StartsWith("264:205:3:94:1320:", StringComparison.Ordinal) || key.StartsWith("264:205:3:94:1362:", StringComparison.Ordinal);
    }

    private static AutoPlayEventCoverage? FindSourceEvent(AutoPlayController controller)
    {
        return controller.State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == "264:205")?.Events.SingleOrDefault(item => item.Event.Id == "264:line:6");
    }

    private static Dictionary<string, byte[]> ReadFixtureBytes(string manifest, AutoPlayRunState source)
    {
        string directory = Path.GetDirectoryName(manifest)!;
        List<string?> references = [source.Snapshot, source.ActiveBeforeCheckpoint, source.PrimaryContinuation];
        foreach (AutoPlayChoiceCoverage choice in source.Coverage.Choices.Where(choice => !choice.Finished))
        {
            references.Add(choice.BeforeCheckpoint);
            references.Add(choice.ContinuationCheckpoint);
        }
        string[] paths = references.Where(reference => reference is not null).Select(reference => Path.GetFullPath(reference!, directory)).Append(Path.GetFullPath(manifest)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return paths.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-autoplay-train-animation-" + Guid.NewGuid().ToString("N"));
        private readonly HashSet<(GameWorld World, long Sequence)> animationOwners = [];
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public AutoPlayController Controller { get; }
        public List<DialogueOutcome> Outcomes { get; } = [];
        public bool AnimationObservationIssued { get; private set; }
        public bool AnimationObservationReturned { get; private set; }

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    world.Events.DialogueCompleted += Outcomes.Add;
                    world.Events.CommandExecuted += command => Observe(observed, command);
                }
                return world;
            }
        }

        private void Observe(GameWorld observed, ScriptCommand command)
        {
            if (command is not { FieldId: 264, ObjectId: 3, ScriptId: 94 })
            {
                return;
            }
            JsonElement[] fibers = observed.Events.Capture().GetProperty("Fibers").EnumerateArray().Where(fiber => fiber.GetProperty("ObjectId").GetInt32() == 3 && fiber.GetProperty("ScriptId").GetInt32() == 94).ToArray();
            if (command is { Operation: 1327, Name: "ANIMESYNC" })
            {
                AnimationObservationIssued = true;
                JsonElement owner = Assert.Single(fibers, fiber => !fiber.GetProperty("Complete").GetBoolean() && fiber.GetProperty("Started").GetBoolean() && fiber.GetProperty("Position").GetInt32() == 1328);
                animationOwners.Add((observed, owner.GetProperty("Sequence").GetInt64()));
            }
            // A blocking issue is not re-emitted when its wait completes. Observe the same owner's subsequent command instead.
            if (command.Operation >= 1327)
            {
                AnimationObservationReturned |= fibers.Any(fiber => animationOwners.Contains((observed, fiber.GetProperty("Sequence").GetInt64())) && fiber.GetProperty("Position").GetInt32() > 1327 && fiber.GetProperty("WaitReason").GetString() != "NativeAnimationSync");
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
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
