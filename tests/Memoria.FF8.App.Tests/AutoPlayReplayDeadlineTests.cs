using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayReplayDeadlineTests
{
    [Fact]
    public void AValidatedUncompletedGlobalAlternativeStartsItsOwnIdleBudgetBeforeItsNativePress()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayReplayDeadlineTests.1");
        string gameDirectory = TestResources.GameDirectory;
        byte[] originalManifest = Hash(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        ManualClock clock = new();
        int? initialField = null;
        bool chargedRestoredOwner = false;
        string output = Path.Combine(Path.GetTempPath(), "autoplay-replay-deadline-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld()
            {
                if (world is null)
                {
                    if (!chargedRestoredOwner && initialField is not null && session.State.FieldId != initialField && session.State.FieldId == saved.ActiveField)
                    {
                        clock.Advance(TimeSpan.FromSeconds(10));
                        chargedRestoredOwner = true;
                    }
                    initialField ??= session.State.FieldId;
                    world = new(session, gameDirectory, validateWorldPresentation: false);
                }
                return world;
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output, deadlineTime: clock);
            controller.ResumeFrom(manifest);

            controller.Advance(1);

            Assert.True(chargedRestoredOwner);
            Assert.Null(controller.Error);
            Assert.True(controller.Running);
            AutoPlayInputSearch search = Assert.IsType<AutoPlayInputSearch>(controller.State.InputSearch);
            AutoPlayInputSearchFrame frame = Assert.Single(search.Frames);
            AutoPlayChoiceCoverage choice = saved.Coverage.Choices.Single(choice => choice.Key == frame.Key);
            int selection = Enumerable.Range(choice.FirstOption, choice.OptionCount).First(option => !choice.CompletedOptions.Contains(option) && !choice.PolicyDeferredOptions.Contains(option));
            Assert.Equal(selection, frame.PreferredSelection);
            Assert.NotNull(frame.ActiveTrial);
            AutoPlayInputSearchTrial trial = frame.Trials[frame.ActiveTrial.Value];
            Assert.Equal(selection, trial.Selection);
            Assert.True(trial.Sampled);
            Assert.DoesNotContain(trial.Selection, choice.CompletedOptions);
            Assert.DoesNotContain(trial.Selection, choice.PolicyDeferredOptions);
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }
}
