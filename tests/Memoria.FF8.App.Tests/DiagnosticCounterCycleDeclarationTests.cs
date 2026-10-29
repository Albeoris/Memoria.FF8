using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticCounterCycleDeclarationTests
{
    private static BattleInput Input => new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Attack, ActionId: 0);
    private static DiagnosticCounterCycleDeclaration Declaration => new(new("enemy-0", 8, 230, 178, 2, DiagnosticCounterCycleDeclaration.SupportedScriptSha256), 32);

    [Fact]
    public void Focal_role_round_trips_without_becoming_a_supporting_invocation()
    {
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        string json = JsonSerializer.Serialize(Declaration, options);
        DiagnosticCounterCycleDeclaration restored = JsonSerializer.Deserialize<DiagnosticCounterCycleDeclaration>(json, options)!;

        restored.ValidateComposition(Input, 178, 230, 8, false, false, false, [], []);

        Assert.Equal(Declaration, restored);
        Assert.Contains("\"focal\"", json);
        Assert.Contains("\"max_trigger_attempts\"", json);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    public void Attempt_budget_is_explicit_and_bounded(int attempts)
    {
        (Declaration with { MaxTriggerAttempts = attempts }).Validate(Input, 178, 230, 8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(257)]
    [InlineData(int.MaxValue)]
    public void Invalid_attempt_budget_is_rejected(int attempts)
    {
        Assert.Throws<InvalidDataException>(() => (Declaration with { MaxTriggerAttempts = attempts }).Validate(Input, 178, 230, 8));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("action")]
    [InlineData("effect")]
    [InlineData("stage")]
    [InlineData("hash")]
    [InlineData("owner")]
    [InlineData("null")]
    public void Unsupported_focal_descriptor_is_rejected(string forgery)
    {
        BattleDiagnosticSupportingInvocation focal = forgery switch
        {
            "command" => Declaration.Focal with { NativeCommand = 1 },
            "action" => Declaration.Focal with { ActionId = 232 },
            "effect" => Declaration.Focal with { EffectId = 0 },
            "stage" => Declaration.Focal with { CallbackStage = 3 },
            "hash" => Declaration.Focal with { ScriptSha256 = new string('A', 64) },
            "owner" => Declaration.Focal with { ActorId = "party-0" },
            "null" => null!,
            _ => throw new ArgumentException("Unknown forgery.", nameof(forgery))
        };

        Assert.Throws<InvalidDataException>(() => (Declaration with { Focal = focal }).Validate(Input, 178, 230, 8));
    }

    [Fact]
    public void Script_hash_is_case_insensitive()
    {
        (Declaration with { Focal = Declaration.Focal with { ScriptSha256 = DiagnosticCounterCycleDeclaration.SupportedScriptSha256.ToLowerInvariant() } }).Validate(Input, 178, 230, 8);
    }

    [Theory]
    [InlineData("smart_attack")]
    [InlineData("enemy_ability")]
    [InlineData("raw_effect")]
    [InlineData("wrong_target")]
    [InlineData("same_actor")]
    [InlineData("draw_cast")]
    public void Trigger_is_the_real_ordinary_action_route(string forgery)
    {
        BattleInput input = forgery switch
        {
            "smart_attack" => Input with { Kind = BattleInputKind.Attack },
            "enemy_ability" => Input with { Ability = BattleAbility.EnemyAbility, ActionId = 230 },
            "raw_effect" => Input with { Ability = BattleAbility.NativeCommand, ActionId = 178 },
            "wrong_target" => Input with { TargetId = "enemy-1" },
            "same_actor" => Input with { ActorId = "enemy-0" },
            "draw_cast" => Input with { DrawCast = true },
            _ => throw new ArgumentException("Unknown forgery.", nameof(forgery))
        };

        Assert.Throws<InvalidDataException>(() => Declaration.Validate(input, 178, 230, 8));
    }

    [Theory]
    [InlineData(0, 230, (byte)8)]
    [InlineData(178, 0, (byte)1)]
    [InlineData(178, null, null)]
    [InlineData(178, 230, null)]
    public void Expected_identity_belongs_to_the_counter_not_the_effect_zero_control(ushort effect, int? action, byte? command)
    {
        Assert.Throws<InvalidDataException>(() => Declaration.Validate(Input, effect, action, command));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Contradictory_coordinators_are_rejected(bool shot, bool enemy, bool limit)
    {
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, shot, enemy, limit, [], []));
    }

    [Fact]
    public void Existing_supporting_guards_preserve_the_focal_effect()
    {
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [178], []));
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [], [Declaration.Focal]));
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [], [Declaration.Focal with { ActorId = "enemy-1" }]));
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [], [Declaration.Focal with { ActorId = "party-0", EffectId = 179 }]));
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [179, 179], []));
        Assert.Throws<InvalidDataException>(() => Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [], [null!]));
        Declaration.ValidateComposition(Input, 178, 230, 8, false, false, false, [179], [Declaration.Focal with { ActorId = "enemy-1", EffectId = 179 }]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"max_trigger_attempts\":32}")]
    [InlineData("{\"focal\":null}")]
    [InlineData("{\"focal\":null,\"max_trigger_attempts\":32,\"allow_effect_bypass\":true}")]
    public void Missing_or_unknown_declaration_members_are_rejected(string json)
    {
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DiagnosticCounterCycleDeclaration>(json, options));
    }
}
