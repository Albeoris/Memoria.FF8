using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayContactInterruptionTests
{
    [Fact]
    public void AnActuallyAdmittedGatewayDoesNotCreditThePlannedPushAndColdPersistsOnlyItsInterruptedAttempt()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayContactInterruptionTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(717, run.Session.State.FieldId);
        Assert.Equal(30, run.World.Events.Frame);
        Assert.Null(run.Controller.State.ActiveEvent);
        AutoPlayEventCoverage Push() => run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "717:223").Events.Single(item => item.Event.Id == "717:push:7");
        string Credit() => JsonSerializer.Serialize(new { Push().Completed, Push().BlockedReason, Push().UnavailableAttempts, Push().ParticipantPolicyDeferrals, Push().Conditions, Push().Routes });
        string credit = Credit();
        string routes = JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRoutes);
        string signature = run.World.ReadAutoPlayConditions("717:push:7", 7).Fingerprint;

        run.Controller.Advance(1);

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Equal(736, run.Session.State.FieldId);
        Assert.Null(run.Controller.State.ActiveEvent);
        Assert.Null(run.Controller.State.ActiveNavigationKey);
        Assert.Equal(credit, Credit());
        Assert.Equal(routes, JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRoutes));
        AutoPlayContactInterruption attempt = Assert.Single(run.Controller.State.ContactInterruptions);
        Assert.Equal("717:push:7:1", attempt.Source.Target.Id);
        Assert.Equal(signature, attempt.Source.ConditionSignature);
        Assert.Equal(717, attempt.Source.FieldId);
        Assert.Equal(223, attempt.Source.Story);
        Assert.Equal(0, attempt.AdmittedGatewayIndex);
        Assert.Equal(new FieldTransition(736, 265, 285, 7, 192), attempt.Destination);
        Assert.Null(attempt.WorldPacket);
        Assert.True(AutoPlayController.IsContactInterrupted(attempt.Source, run.Controller.State.ContactInterruptions));

        run.World.EnsureField();
        for (int callback = 0; callback < 8 && run.World.Events.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed }; callback++)
        {
            Assert.True(run.World.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(NativeFieldInitializationStage.Completed, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        string interruptions = JsonSerializer.Serialize(run.Controller.State.ContactInterruptions, AutoPlayController.JsonOptions);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(interruptions, JsonSerializer.Serialize(cold.Controller.State.ContactInterruptions, AutoPlayController.JsonOptions));
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void OnlyTheSameVariantAndSourceContextAreSuppressedWithoutCompletingItsEvent()
    {
        AutoPlayContactSource source = Source();
        AutoPlayContactInterruption attempt = new(1, source, 0, new FieldTransition(601, 10, 20, 3, 0));
        AutoPlayContactSource other = source with { Target = source.Target with { Id = "600:push:7:1", X = 11 } };
        AutoPlayPlanner planner = new();
        planner.Observe(new("600:1", "test"), [new("600:push:7", AutoPlayEventKind.Interaction, ConditionSignature: source.ConditionSignature, Variants: [source.Target.Id, other.Target.Id])]);
        string before = JsonSerializer.Serialize(planner.State);

        Assert.True(AutoPlayController.IsContactInterrupted(source, [attempt]));
        Assert.False(AutoPlayController.IsContactInterrupted(other, [attempt]));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        AutoPlayEventCoverage coverage = Assert.Single(Assert.Single(planner.State.Visits).Events);
        Assert.False(coverage.Completed);
        Assert.Null(coverage.BlockedReason);
        Assert.Empty(coverage.Conditions!.ConsumedEntries);
        Assert.Empty(coverage.Routes);
        Assert.Empty(coverage.UnavailableAttempts);
    }

    [Theory]
    [InlineData("navigation")]
    [InlineData("predicate")]
    [InlineData("availability")]
    [InlineData("target")]
    [InlineData("approach")]
    [InlineData("player")]
    [InlineData("radius")]
    [InlineData("gateways")]
    [InlineData("disabled")]
    public void AChangedActualContextReopensTheVariant(string change)
    {
        AutoPlayContactSource source = Source();
        AutoPlayContactInterruption attempt = new(1, source, 0, new FieldTransition(601, 10, 20, 3, 0));
        AutoPlayContactSource changed = change switch
        {
            "navigation" => source with { NavigationKey = "other-component" },
            "predicate" => source with { ConditionSignature = "changed-source" },
            "availability" => source with { AvailabilityFingerprint = "changed-eligibility" },
            "target" => source with { Target = source.Target with { X = 12 } },
            "approach" => source with { Target = source.Target with { ApproachX = 8, ApproachY = 9, ApproachTriangle = 3 } },
            "player" => source with { PlayerPosition = source.PlayerPosition with { X = 5 } },
            "radius" => source with { PushRadius = source.PushRadius + 1 },
            "gateways" => source with { GatewaysFingerprint = "changed-original-gateway" },
            "disabled" => source with { MapJumpDisabled = true },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };

        Assert.False(AutoPlayController.IsContactInterrupted(changed, [attempt]));
    }

    [Fact]
    public void GatewayIdentityUsesTheActualListIndexAndNeverThePushObjectId()
    {
        AutoPlayTarget push = Source().Target;
        AutoPlayTarget gateway = push with { Id = "600:gateway:0:0:0", Kind = AutoPlayTargetKind.Gateway, ObjectId = -1 };

        Assert.False(AutoPlayController.IsPlannedGateway(push, 7));
        Assert.True(AutoPlayController.IsPlannedGateway(gateway, 0));
        Assert.False(AutoPlayController.IsPlannedGateway(gateway, 1));
        Assert.Throws<InvalidDataException>(() => AutoPlayController.IsPlannedGateway(gateway with { Id = "600:gateway:unknown" }, 0));
    }

    [Fact]
    public void AnExplicitApproachRetainsTheReceiptPoseButMakesDifferentEarlierPositionsEquivalent()
    {
        AutoPlayContactSource source = Source();
        source = source with { Target = source.Target with { ApproachX = 8, ApproachY = 9, ApproachZ = 30, ApproachTriangle = 3 } };
        AutoPlayContactInterruption attempt = new(1, source, 0, new FieldTransition(601, 10, 20, 3, 0));
        AutoPlayContactSource otherPose = source with { PlayerPosition = new(100, 200, 300, 4, 64) };

        Assert.True(AutoPlayController.IsContactInterrupted(otherPose, [attempt]));
        Assert.Equal(source.PlayerPosition, attempt.Source.PlayerPosition);
        Assert.NotEqual(otherPose.PlayerPosition, attempt.Source.PlayerPosition);
    }

    private static AutoPlayContactSource Source()
    {
        return new(1, "600:1", "source-component", 600, 1, "actual-source", null, new("600:push:7:0", AutoPlayTargetKind.Contact, 10, 20, 30, 3, ObjectId: 7), new(1, 2, 3, 4, 0), 48, false, "actual-gateway-definitions");
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-autoplay-contact-interruption-" + Guid.NewGuid().ToString("N"));

        internal InstalledRun()
        {
            Controller = new(Session, () => World, state =>
            {
                world?.Dispose();
                world = null;
                Session.Restore(state);
            }, output);
        }

        internal GameSession Session { get; } = GameSession.CreateColdProcess();
        internal GameWorld World => world ??= new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
        internal AutoPlayController Controller { get; }

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
