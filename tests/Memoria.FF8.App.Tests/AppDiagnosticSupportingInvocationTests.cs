using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class AppDiagnosticSupportingInvocationTests
{
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "enemy-0", TargetId: "party-0", Ability: BattleAbility.EnemyAbility, ActionId: 98), 154) { ExpectedNativeCommand = 8, ExpectedNativeAction = 98 };
    private static BattleDiagnosticSupportingInvocation Invocation => new("enemy-1", 8, 82, 179, 2, "3AE4C1F057643B9B21B4AE6E63BB89FB2C2BD936D120BEA8F9FC6E34B55EE625");

    [Fact]
    public void Original_reaction_descriptors_round_trip_with_recipe_identity_and_legacy_support()
    {
        DiagnosticBattleDriverRecipe recipe = Recipe with { SupportingEffects = [180], SupportingInvocations = [Invocation, Invocation with { ActionId = 83 }, Invocation with { ActionId = 62, EffectId = 180 }] };
        recipe.Validate();
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        string json = JsonSerializer.Serialize(recipe, options);
        DiagnosticBattleDriverRecipe restored = JsonSerializer.Deserialize<DiagnosticBattleDriverRecipe>(json, options)!;
        restored.Validate();

        Assert.Equal(recipe.SupportingInvocations, restored.SupportingInvocations);
        Assert.Equal(recipe.SupportingEffects, restored.SupportingEffects);
        Assert.Equal(recipe.Input, restored.Input);
        Assert.Equal(recipe.ExpectedEffect, restored.ExpectedEffect);
        Assert.Contains("\"supporting_invocations\"", json);
    }

    [Theory]
    [InlineData("ordinary_turn")]
    [InlineData("initialization")]
    [InlineData("focal_effect")]
    [InlineData("focal_actor")]
    [InlineData("duplicate")]
    [InlineData("too_many")]
    [InlineData("null_array")]
    [InlineData("null_descriptor")]
    [InlineData("invalid_script")]
    public void Supporting_invocations_reject_unproved_or_focal_descriptors(string forgery)
    {
        BattleDiagnosticSupportingInvocation[] declarations = forgery switch
        {
            "ordinary_turn" => [Invocation with { CallbackStage = 1 }],
            "initialization" => [Invocation with { CallbackStage = 0 }],
            "focal_effect" => [Invocation with { EffectId = 154 }],
            "focal_actor" => [Invocation with { ActorId = "enemy-0" }],
            "duplicate" => [Invocation, Invocation],
            "too_many" => Enumerable.Range(0, 33).Select(index => Invocation with { ActionId = checked((ushort)(index + 1)) }).ToArray(),
            "null_array" => null!,
            "null_descriptor" => [null!],
            "invalid_script" => [Invocation with { ScriptSha256 = "unproved" }],
            _ => throw new ArgumentException("Unknown forgery.", nameof(forgery))
        };

        Assert.Throws<InvalidDataException>(() => (Recipe with { SupportingInvocations = declarations }).Validate());
    }

    [Fact]
    public void Supporting_other_actor_reaction_cannot_replace_the_required_focal_dispatch()
    {
        DiagnosticBattleDriver driver = new(Recipe with { BaselineSeconds = 0, SupportingInvocations = [Invocation] }, 0);
        driver.Advance(new(0, 0, 0, true, true, false, []));
        driver.Advance(new(0.1, 1, 1, true, true, false, []));
        driver.ObserveDispatch(new(1, 2, "enemy-1", BattleAbility.EnemyAbility, 82, 179, 179, 8));

        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        Assert.Empty(driver.Dispatches);
        driver.ObserveDispatch(new(2, 3, "enemy-0", BattleAbility.EnemyAbility, 98, 154, 154, 8));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Null(driver.Blocker);
    }
}
