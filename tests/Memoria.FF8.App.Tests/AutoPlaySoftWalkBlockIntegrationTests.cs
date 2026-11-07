using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySoftWalkBlockIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void AuthenticControllerReinspectsTheStaleWalkingBlockAndUsesTheNativeRoute()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlaySoftWalkBlockIntegrationTests.1");
        string directory = TestResources.GameDirectory;
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalManifest = Hash(manifest);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        int restores = 0;
        int movements = 0;
        string capture = Path.Combine(Path.GetTempPath(), "autoplay-soft-walk-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld()
            {
                if (world is null)
                {
                    world = new GameWorld(session, directory, validateWorldPresentation: false);
                    world.Events.NativePlayerMovementProcessed += _ => movements++;
                }
                return world;
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
                restores++;
            }
            AutoPlayController controller = new(session, GetWorld, Restore, capture, deadlineTime: new RoutingClock());
            controller.ResumeFrom(manifest);
            Assert.Equal(919, session.State.FieldId);
            int startingRestores = restores;
            List<int> fields = [session.State.FieldId];
            bool reopened = false;
            int idleSteps = 0;
            for (int step = 0; step < 128 && session.State.FieldId != 920; step++)
            {
                int previousMoves = movements;
                int previousField = session.State.FieldId;
                controller.Advance(1);
                Assert.True(controller.Error is null, $"Controller failed at step{controller.State.Steps}: {controller.Error}");
                Assert.True(controller.Running);
                if (fields[^1] != session.State.FieldId)
                {
                    fields.Add(session.State.FieldId);
                }
                AutoPlayEventCoverage currentGateway = controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "918:285").Events.Single(item => item.Event.Id == "918:gateway:2");
                reopened |= currentGateway.BlockedReason is null;
                Assert.False(reopened && currentGateway.BlockedReason is not null, $"Gateway2 was reblocked after inspection at step{controller.State.Steps}: {currentGateway.BlockedReason}; nativeXYZ={world?.Events.Scene.NativePlayerPosition}");
                bool idle = session.State.FieldId == previousField && movements == previousMoves && controller.State.ActiveEvent is null && controller.State.PendingExhaustedSurvey is null && controller.State.SettleFrames >= 4 && world?.NextTrigger.Kind == PlayerTriggerKind.Playable;
                idleSteps = idle ? idleSteps + 1 : 0;
                Assert.True(idleSteps < 16, $"No planned movement for16 steps at field{session.State.FieldId}, step{controller.State.Steps}; nativeXYZ={world?.Events.Scene.NativePlayerPosition}");
            }
            AutoPlayEventCoverage gateway = controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "918:285").Events.Single(item => item.Event.Id == "918:gateway:2");
            output.WriteLine($"Fields={string.Join(',', fields)}; steps={controller.State.Steps - saved.Steps}; nativeMoves={movements}; softBlock={gateway.BlockedReason}; surveyReceipts={controller.State.ExhaustedSurveyInspections.Count}");
            Assert.Equal(new[] { 919, 918, 920 }, fields);
            Assert.Equal(startingRestores, restores);
            Assert.True(movements > 0);
            Assert.Null(gateway.BlockedReason);
            Assert.Null(gateway.BlockKind);
            Assert.True(controller.State.ExhaustedSurveyInspections.Count > saved.ExhaustedSurveyInspections.Count);
            Assert.Null(world?.Events.LastFault);
        }
        finally
        {
            world?.Dispose();
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
            if (Directory.Exists(capture))
            {
                Directory.Delete(capture, recursive: true);
            }
        }
    }

    [Fact]
    public void ARecordedNativeGatewaySampleCanFailWhileTheNextPhysicalVariantSucceeds()
    {
        string snapshot = TestResources.FixturePath("App.Tests.AutoPlaySoftWalkBlockIntegrationTests.2");
        byte[] original = Hash(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(918, session.State.FieldId);
        AutoPlayTarget[] variants = AutoPlayController.OrderAutoPlayWalkingVariants(world.DiscoverAutoPlayTargets().Where(target => target.CoverageId == "918:gateway:2"), "918:gateway:2:0:0");
        Assert.True(variants.Length > 1);
        try
        {
            Assert.True(world.WalkAutoPlayApproach(variants[0], IgnoreProgress).Arrived);
            AutoPlayWalkResult recorded = world.WalkAutoPlayContact(variants[0], IgnoreProgress);
            Assert.False(recorded.Arrived);
            Assert.False(recorded.Interrupted);
            Assert.Equal(918, session.State.FieldId);
            int? admitted = null;
            foreach (AutoPlayTarget variant in variants.Skip(1))
            {
                AutoPlayWalkResult result = world.WalkAutoPlayApproach(variant, IgnoreProgress);
                if (result.Arrived)
                {
                    result = world.WalkAutoPlayContact(variant, IgnoreProgress);
                }
                output.WriteLine($"Variant={variant.Id}; result={result}; nativeXYZ={world.Events.Scene.NativePlayerPosition}");
                if (result.GatewayIndex is int gateway)
                {
                    admitted = gateway;
                    break;
                }
                Assert.False(result.Interrupted);
            }
            Assert.Equal(2, admitted);
            Assert.Equal(920, session.State.FieldId);
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            Assert.Equal(original, Hash(snapshot));
        }
    }

    private static void IgnoreProgress()
    {
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
