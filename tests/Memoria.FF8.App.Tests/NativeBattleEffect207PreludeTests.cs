using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect207PreludeTests
{
    [Fact]
    public void Original_ordinary_prelude_completes_its_contact_free_linked_sequences()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipe = Environment.GetEnvironmentVariable("MEMORIA_FF8_PRELUDE_RECIPE");
        Assert.SkipUnless(game is not null && recipe is not null && File.Exists(recipe), "Select installed resources and an external ordinary prelude recipe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipe!), json)!;
        request.Validate();
        Assert.Equal(120, request.Encounter);
        Assert.Equal((ushort)207, request.Driver!.ExpectedEffect);
        Assert.Equal(87, request.Prelude!.DefeatedModel);
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        world.PrepareDiagnosticEntryBeforePresentation(request);
        Assert.True(battle.NativeEntryReady);
        Assert.NotNull(world.VerifyDiagnosticTarget());
    }
}
