using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticTerminalHoldTests
{
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 1), 2);
    private static BattleDiagnosticTerminalHoldDeclaration Hold => new("Declared repeat after party defeat", ["party-0", "party-1"], 10000);

    [Fact]
    public void A_terminal_hold_requires_explicit_repairs_for_every_declared_party_actor()
    {
        Assert.Throws<InvalidDataException>(() => (Recipe with { TerminalHold = Hold }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { TerminalHold = Hold, Repairs = [new("party-0", 100, Revive: true)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { TerminalHold = Hold, Repairs = [new("party-0", 100, Revive: true), new("party-2", 100, Revive: true)] }).Validate());
        (Recipe with { TerminalHold = Hold, Repairs = [new("party-0", 100, Revive: true), new("party-1", 100, Revive: true)] }).Validate();
    }

    [Fact]
    public void A_terminal_hold_declaration_round_trips_with_the_driver_request()
    {
        DiagnosticBattleRequest request = new(94, 0, 0x10, 0, 0)
        {
            Driver = Recipe with { TerminalHold = Hold, Repairs = [new("party-0", 100, Revive: true), new("party-1", 100, Revive: true)] }
        };
        DiagnosticBattleRequest restored = JsonSerializer.Deserialize<DiagnosticBattleRequest>(JsonSerializer.Serialize(request))!;
        restored.Validate();
        Assert.Equal(Hold.Reason, restored.Driver!.TerminalHold!.Reason);
        Assert.Equal(Hold.PartyActorIds.ToArray(), restored.Driver.TerminalHold.PartyActorIds.ToArray());
        Assert.Equal(Hold.MaximumCallbacks, restored.Driver.TerminalHold.MaximumCallbacks);
        Assert.Equal(request.Driver.Repairs, restored.Driver.Repairs);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Reason\":\"repeat\",\"PartyActorIds\":[\"party-0\"]}")]
    public void A_terminal_hold_does_not_default_required_declarations(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BattleDiagnosticTerminalHoldDeclaration>(json));
    }

    [Fact]
    public void Invalid_terminal_hold_bounds_are_rejected_before_launch()
    {
        Assert.Throws<InvalidDataException>(() => (Recipe with { TerminalHold = Hold with { MaximumCallbacks = 0 }, Repairs = [new("party-0", 100), new("party-1", 100)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { TerminalHold = Hold with { PartyActorIds = ["party-0", "party-0"] }, Repairs = [new("party-0", 100)] }).Validate());
    }

    [Fact]
    public void A_driver_without_an_explicit_terminal_hold_keeps_the_natural_terminal_blocker()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        Assert.Null(Recipe.TerminalHold);
        Assert.False(driver.Advance(new(0, 1, 1, false, true, true, [])));
        Assert.Equal(DiagnosticDriverStage.Blocked, driver.Stage);
        Assert.Equal("REPEAT_PRECONDITION_UNSATISFIED", driver.Blocker);
    }

    [Fact]
    public void Installed_direct_entry_arms_only_the_explicitly_declared_hold_before_callbacks()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for diagnostic terminal hold entry.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleRequest request = new(94, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Driver = Recipe with { TerminalHold = Hold with { PartyActorIds = ["party-0"] }, Repairs = [new("party-0", 100, Revive: true)] }
        };
        BattleSession active = world.StartDiagnosticBattle(request);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Armed, active.DiagnosticTerminalHold!.Phase);
        Assert.Empty(active.DiagnosticTerminalHoldReceipts);
        Assert.Equal(BattleOutcome.Active, active.Outcome);
    }
}
