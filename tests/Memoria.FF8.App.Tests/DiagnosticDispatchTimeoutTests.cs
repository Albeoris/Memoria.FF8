using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticDispatchTimeoutTests
{
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, ActionTimeoutSeconds = 180, OverallTimeoutSeconds = 300 };
    private static DiagnosticDriverObservation Point(double seconds) => new(seconds, (long)seconds, (long)seconds, false, false, false, ["native-action"]);

    private static DiagnosticBattleDriver Started(DiagnosticBattleDriverRecipe recipe)
    {
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(new(0, 0, 0, true, true, false, []));
        Assert.True(driver.Advance(new(0, 0, 1, true, true, false, [])));
        return driver;
    }

    [Fact]
    public void Default_recipe_preserves_the_existing_ten_second_dispatch_budget()
    {
        Assert.Equal(10, Recipe.DispatchTimeoutSeconds);
        DiagnosticBattleDriver driver = Started(Recipe);
        driver.Advance(Point(9.999));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.Advance(Point(10));
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
    }

    [Fact]
    public void Explicit_budget_accepts_a_late_focal_without_changing_its_source_header_identity()
    {
        DiagnosticBattleDriver driver = Started(Recipe with { DispatchTimeoutSeconds = 30 });
        driver.Advance(Point(20));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        BattleDiagnosticEffectDispatch source = new(7, 20, "party0", BattleAbility.Magic, 1, 2, 2, 2) { NativeHeaderIndex = 0, NativeHeaderGeneration = 9, NativeActorIndex = 0, NativeTargetMask = 8 };
        BattleDiagnosticEffectDispatch before = source with { };
        driver.ObserveDispatch(source);
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Equal(before, source);
        Assert.Same(source, Assert.Single(driver.Dispatches));
        Assert.Null(driver.Blocker);
    }

    [Fact]
    public void Explicit_dispatch_budget_expires_at_its_boundary()
    {
        DiagnosticBattleDriver driver = Started(Recipe with { DispatchTimeoutSeconds = 30 });
        driver.Advance(Point(29.999));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.Advance(Point(30));
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
    }

    [Fact]
    public void Explicit_dispatch_budget_cannot_exceed_the_whole_action_cap()
    {
        DiagnosticBattleDriver driver = Started(Recipe with { DispatchTimeoutSeconds = 30, ActionTimeoutSeconds = 12 });
        driver.Advance(Point(11.999));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.Advance(Point(12));
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
    }

    [Fact]
    public void Explicit_dispatch_budget_cannot_exceed_the_overall_cap()
    {
        DiagnosticBattleDriver driver = Started(Recipe with { DispatchTimeoutSeconds = 30, OverallTimeoutSeconds = 15 });
        driver.Advance(Point(14.999));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.Advance(Point(15.001));
        Assert.Equal("OVERALL_TIMEOUT", driver.Blocker);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Dispatch_budget_must_be_finite_and_positive(double seconds)
    {
        Assert.Throws<InvalidDataException>(() => (Recipe with { DispatchTimeoutSeconds = seconds }).Validate());
    }

    [Fact]
    public void Optional_snake_case_recipe_property_preserves_default_and_explicit_values()
    {
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        JsonObject original = JsonSerializer.SerializeToNode(Recipe with { DispatchTimeoutSeconds = 30 }, json)!.AsObject();
        Assert.Equal(30, original.Deserialize<DiagnosticBattleDriverRecipe>(json)!.DispatchTimeoutSeconds);
        Assert.True(original.Remove("dispatch_timeout_seconds"));
        Assert.Equal(10, original.Deserialize<DiagnosticBattleDriverRecipe>(json)!.DispatchTimeoutSeconds);
    }
}
