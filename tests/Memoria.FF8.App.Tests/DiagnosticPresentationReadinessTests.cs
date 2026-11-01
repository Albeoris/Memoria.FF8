using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticPresentationReadinessTests
{
    [Fact]
    public void Presentation_defaults_to_selected_actor_fill_and_can_explicitly_preserve_natural_ATB()
    {
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        DiagnosticPresentationDeclaration declaration = JsonSerializer.Deserialize<DiagnosticPresentationDeclaration>("""{"signal_path":"start.json","receipt_directory":"receipts","run_id":"test"}""", options)!;
        Assert.True(declaration.FillSelectedActorAtb);
        Assert.False(JsonSerializer.Deserialize<DiagnosticPresentationDeclaration>("""{"signal_path":"start.json","receipt_directory":"receipts","run_id":"test","fill_selected_actor_atb":false}""", options)!.FillSelectedActorAtb);
    }

    [Fact]
    public void Installed_selected_player_fill_preserves_other_gauges_and_registers_without_executing_an_effect()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for diagnostic readiness preparation.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleRequest request = new(94, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [], true),
            Driver = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "party-0", Ability: BattleAbility.Magic, ActionId: 21), 1)
        };
        BattleSession active = world.StartDiagnosticBattle(request);
        int dispatches = 0;
        active.DiagnosticEffectDispatched += _ => dispatches++;
        Assert.Throws<InvalidOperationException>(() => active.PrepareDiagnosticPlayerReadiness("party-0"));
        for (int index = 0; index < request.EntryCallbackBudget && (!active.NativeEntryReady || active.ObserveDiagnosticDrainBlockers().Count != 0); index++)
        {
            active.Tick();
        }
        Assert.True(active.NativeEntryReady);
        Assert.Empty(active.ObserveDiagnosticDrainBlockers());
        BattleCombatant selected = active.Combatants.Single(actor => actor.Id == "party-0");
        NativeBattleAtbState before = selected.NativeAtb!;
        Assert.True(before.Current < before.Maximum, "The entry boundary must precede natural full-gauge readiness for this regression.");
        Dictionary<string, NativeBattleAtbState?> otherGauges = active.Combatants.Where(actor => actor.Id != selected.Id).ToDictionary(actor => actor.Id, actor => actor.NativeAtb);
        long? callbacks = active.NativeHostCallbacks;
        active.PrepareDiagnosticPlayerReadiness(selected.Id);
        Assert.Equal(before.Maximum, selected.NativeAtb!.Maximum);
        Assert.Equal(before.Maximum, selected.NativeAtb.Current);
        Assert.Equal(callbacks, active.NativeHostCallbacks);
        Assert.Equal(0, dispatches);
        foreach (BattleCombatant other in active.Combatants.Where(actor => actor.Id != selected.Id))
        {
            Assert.Equal(otherGauges[other.Id], other.NativeAtb);
        }
        string registered = JsonSerializer.Serialize(active.Capture());
        active.PrepareDiagnosticPlayerReadiness(selected.Id);
        Assert.Equal(registered, JsonSerializer.Serialize(active.Capture()));
        for (int index = 0; index < 16 && !active.CanChoosePlayerCommand(selected.Id); index++)
        {
            active.Tick();
        }
        Assert.True(active.CanChoosePlayerCommand(selected.Id));
        Assert.Equal(0, dispatches);
    }
}
