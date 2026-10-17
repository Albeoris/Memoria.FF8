using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayFingerprintVersionTests
{
    [Fact]
    public void LegacyPredicateDomainsRequestARefreshWithoutEarningChangedFrontierCredit()
    {
        string fixture = TestResources.FixturePath("App.Tests.NativeDreamPlayerAbsenceTests.2");
        byte[] original = File.ReadAllBytes(fixture);
        GameSession session = new(GameSession.Load(fixture));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        AutoPlayController controller = new(session, () => world, _ => throw new InvalidOperationException("This read-only guard comparison cannot restore gameplay."), TestResources.ArtifactDirectory);
        AutoPlayVisit visit = new("742:223", "city");
        MethodInfo needsRefresh = typeof(AutoPlayController).GetMethod("NeedsVisitRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string hash = world.ReadAutoPlayFieldProgramHash(742);
        string fingerprint = world.Events.ReadPersistentPredicateFingerprint([])!;
        foreach (int legacyVersion in new[] { 1, 2 })
        {
            controller.State.VisitGuards[visit.Key] = new(legacyVersion, 742, hash, [], "legacy-fingerprint");
            Assert.False(Assert.IsType<bool>(needsRefresh.Invoke(controller, [world, visit, false])));
            Assert.True(Assert.IsType<bool>(needsRefresh.Invoke(controller, [world, visit, true])));
        }

        controller.State.VisitGuards[visit.Key] = new(AutoPlayVisitGuard.CurrentVersion, 742, hash, [], fingerprint);
        Assert.False(Assert.IsType<bool>(needsRefresh.Invoke(controller, [world, visit, false])));
        controller.State.VisitGuards[visit.Key] = new(AutoPlayVisitGuard.CurrentVersion, 742, hash, [], "changed-current-domain");
        Assert.True(Assert.IsType<bool>(needsRefresh.Invoke(controller, [world, visit, false])));
        Assert.Equal(original, File.ReadAllBytes(fixture));
    }
}
