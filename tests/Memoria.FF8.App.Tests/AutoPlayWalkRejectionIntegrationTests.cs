using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWalkRejectionIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void NativeReturningContactSourceIsCertifiedWithoutChangingItsScene()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayWalkRejectionIntegrationTests.1");
        string snapshot = SnapshotPath(manifest);
        byte[] originalManifest = Hash(manifest);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(900, world.Events.FieldId);
        Assert.Equal(285, world.Events.Variables.GetValueOrDefault(256));
        JsonElement before = world.Events.Capture();
        try
        {
            int[] boundPartyActors = Enumerable.Range(0, 3).Select(slot => NativeFieldPartyBinding.TryGetObjectId(world.Events.Scene, slot, out int actor) ? actor : -1).ToArray();
            Assert.True(GameWorld.CanCacheAutoPlayWalkRejectionSource(JsmProgram.Read(world.Field!.Jsm), "900:line:18", 18, boundPartyActors), "The native contact's bounded source closure was not certified for its actual party owners.");
            Assert.NotNull(world.ReadAutoPlayWalkRejectionState("900:line:18", 18));
            Assert.True(world.CanCacheAutoPlayWalkRejectionSource("900:line:18", 18));
            Assert.True(world.CanCacheAutoPlayWalkRejectionSource("900:line:18", 18));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
        finally
        {
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
        }
    }

    [Fact]
    public void AuthenticControllerLearnsTheReturningContactAndLeavesByANativeExit()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayWalkRejectionIntegrationTests.1");
        string directory = TestResources.GameDirectory;
        string snapshot = SnapshotPath(manifest);
        byte[] originalManifest = Hash(manifest);
        byte[] originalSnapshot = Hash(snapshot);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        string outputDirectory = Path.Combine(Path.GetTempPath(), "autoplay-walk-return-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new GameWorld(session, directory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, outputDirectory, deadlineTime: new RoutingClock());
            controller.ResumeFrom(manifest);
            Assert.Equal(900, session.State.FieldId);
            for (int step = 0; step < 256 && session.State.FieldId == 900; step++)
            {
                controller.Advance(1);
                Assert.True(controller.Error is null, $"Controller failed at step{controller.State.Steps}: {controller.Error}");
                Assert.True(controller.Running);
            }
            AutoPlayEventCoverage northExit = controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "900:285").Events.Single(item => item.Event.Id == "900:gateway:0");
            output.WriteLine($"Field={session.State.FieldId}; steps={controller.State.Steps - saved.Steps}; rejectionReceipts={controller.State.WalkRejections.Count}; northExitCompleted={northExit.Completed}; nativeXYZ={world?.Events.Scene.NativePlayerPosition}");
            Assert.Equal(916, session.State.FieldId);
            Assert.Equal(285, session.State.WorldEntryMemory!.StoryWord);
            Assert.True(controller.State.WalkRejections.Count > saved.WalkRejections.Count);
            Assert.False(northExit.Completed);
            Assert.Empty(northExit.Routes);
            Assert.Empty(northExit.Conditions!.ConsumedEntries);
            Assert.Null(world?.Events.LastFault);
        }
        finally
        {
            world?.Dispose();
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static string SnapshotPath(string manifest)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
        return Path.GetFullPath(document.RootElement.GetProperty("Snapshot").GetString()!, Path.GetDirectoryName(manifest)!);
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private sealed class RoutingClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
