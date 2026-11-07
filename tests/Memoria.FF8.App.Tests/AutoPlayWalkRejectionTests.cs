using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWalkRejectionTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(-10, true)]
    [InlineData(10, false)]
    public void AReturnOrRegressionIsDistinctFromAnAdvancingGreeting(int afterX, bool rejected)
    {
        AutoPlayTarget target = Source().Target;
        Assert.Equal(rejected, AutoPlayController.WalkReturnedWithoutAdvance(target, new(0, 0, 0), new(afterX * 4096, 0, 0)));
    }

    [Theory]
    [InlineData("position")]
    [InlineData("navigation")]
    [InlineData("predicate")]
    [InlineData("variant")]
    [InlineData("geometry")]
    [InlineData("owner")]
    [InlineData("unproved")]
    public void AChangedSourceOrBlockingContractReopensTheAttempt(string change)
    {
        AutoPlayContactSource source = Source();
        AutoPlayWalkRejectionGuard guard = Guard();
        AutoPlayWalkRejection[] receipts = [new(source, guard, new(0, 0, 0))];
        string original = JsonSerializer.Serialize(receipts);
        AutoPlayContactSource current = change switch
        {
            "position" => source with { PlayerPosition = source.PlayerPosition with { X = 1 } },
            "navigation" => source with { NavigationKey = "other" },
            "predicate" => source with { ConditionSignature = "changed" },
            "variant" => source with { Target = source.Target with { Id = "600:gateway:0:1:0" } },
            _ => source
        };
        AutoPlayWalkRejectionGuard? currentGuard = change switch
        {
            "geometry" => guard with { Geometry = "changed" },
            "owner" => guard with { Predicate = "changed" },
            "unproved" => null,
            _ => guard
        };
        Assert.True(AutoPlayController.IsWalkRejected(source, new(0, 0, 0), guard, receipts));
        Assert.False(AutoPlayController.IsWalkRejected(current, new(0, 0, 0), currentGuard, receipts));
        Assert.Equal(original, JsonSerializer.Serialize(receipts));
    }

    [Fact]
    public void TheNativeReturnMayTurnThePlayerWithoutForgettingTheSameAttempt()
    {
        AutoPlayContactSource source = Source();
        AutoPlayWalkRejection receipt = new(source, Guard(), new(0, 0, 0));
        AutoPlayWalkRejection restored = JsonSerializer.Deserialize<AutoPlayWalkRejection>(JsonSerializer.Serialize(receipt))!;
        Assert.True(AutoPlayController.IsWalkRejected(source with { PlayerPosition = source.PlayerPosition with { Direction = 128 } }, new(0, 0, 0), Guard(), [restored]));
    }

    [Fact]
    public void ExactNativeSubpixelPositionIsRequiredAndAnOlderCacheEntryCanBeIgnored()
    {
        AutoPlayContactSource source = Source();
        AutoPlayWalkRejection receipt = new(source, Guard(), new(0, 0, 0));
        Assert.False(AutoPlayController.IsWalkRejected(source, new(1, 0, 0), Guard(), [receipt]));
        AutoPlayWalkRejection old = JsonSerializer.Deserialize<AutoPlayWalkRejection>(JsonSerializer.Serialize(new AutoPlayWalkRejection(source, Guard())))!;
        Assert.False(AutoPlayController.IsWalkRejected(source, new(0, 0, 0), Guard(), [old]));
    }

    [Fact]
    public void OlderControllerStateNeedsNoRejectionCapabilityMarker()
    {
        AutoPlayRunState restored = JsonSerializer.Deserialize<AutoPlayRunState>("{}")!;
        Assert.Empty(restored.WalkRejections);
        Assert.Null(restored.PendingWalkInterruption);
    }

    private static AutoPlayContactSource Source()
    {
        AutoPlayTarget target = new("600:gateway:0:0:0", AutoPlayTargetKind.Gateway, 100, 0, 0, 0, ApproachX: 80, ApproachY: 0);
        return new(1, "600:1", "component", 600, 1, "predicate", null, target, new(0, 0, 0, 0, 0), 48, false, "gateways");
    }

    private static AutoPlayWalkRejectionGuard Guard() => new(1, "600:line:2", 2, "owner", "geometry");
}
