using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectPreviewRequestTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(116)]
    [InlineData(212)]
    [InlineData(400)]
    public void Numeric_registry_identity_is_independent_of_gameplay_ability_families(ushort effect)
    {
        EffectPreviewRequest request = new(1, 0, effect);
        request.Validate();
        var arena = request.CreateArena();
        var enemy = Assert.Single(arena.Enemies);
        Assert.Equal(1, enemy.ModelId);
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(0, arena.StageId);
        Assert.Equal(effect, request.EffectId);
    }

    [Fact]
    public void Three_ID_JSON_is_sufficient_and_keeps_transport_optional()
    {
        EffectPreviewRequest request = JsonSerializer.Deserialize<EffectPreviewRequest>("""{"enemy_id":1,"ally_id":0,"effect_id":2}""", new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        request.Validate();
        Assert.Null(request.Presentation);
        Assert.Equal(0, request.Seed);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EffectPreviewRequest>("{}"));
        Assert.Throws<InvalidDataException>(() => (request with { EffectId = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { EnemyId = 239 }).Validate());
    }

    [Fact]
    public void Preview_launch_uses_isolated_graphical_host_without_a_battle_recipe()
    {
        LaunchOptions options = LaunchOptions.Parse(["--effect-preview", "preview.json", "--settings-dir", "settings", "--control-dir", "control", "--save-state", "disposable/unused.json"]);
        Assert.True(options.IsDiagnosticGraphicalHost);
        Assert.Null(options.BattleTestPath);
        Assert.NotNull(options.EffectPreviewPath);
        Assert.False(StartupSequence.ShouldPlay(options));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--effect-preview", "preview.json", "--battle-test", "case.json"]));
    }

    [Fact]
    public void Whole_ability_uses_the_original_menu_namespace_and_excludes_a_raw_effect_override()
    {
        EffectPreviewRequest request = JsonSerializer.Deserialize<EffectPreviewRequest>("""{"enemy_id":1,"ally_id":0,"ability_id":1}""", new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        request.Validate();
        Assert.Null(request.EffectId);
        Assert.Equal(3, request.AbilityCommand);
        Assert.Equal(1, request.AbilityId);
        (request with { CommandId = 2 }).Validate();
        Assert.Throws<InvalidDataException>(() => (request with { EffectId = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => new EffectPreviewRequest(1, 0).Validate());
        Assert.Throws<NotSupportedException>(() => (request with { CommandId = 4 }).Validate());
    }
}
