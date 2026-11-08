using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiagnosticDuelCycleTests
{
    [Fact]
    public void Original_command11_PunchRush_two_cycles_close_by_native_timer_and_preserve_cold_input_owner()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_CYCLE_NATIVE") == "1" && directory is not null, "Select installed resources and opt in to the genuine ordinary Duel cycle regression.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 1, 274);
        BattleCombatantDefinition actor = source.Combatants[0];
        BattleNativeLimitDefinition rules = actor.NativeLimit!;
        int hp = Enumerable.Range(1, actor.MaximumHp).MaxBy(value => Enumerable.Range(0, 256).Count(random => NativeBattleLimitRules.CrisisLevel(rules, value, actor.MaximumHp, 0, 0, 0, (byte)random) == 1));
        source = source with { Combatants = [actor with { CurrentHp = hp }, source.Combatants[1]], LimitProgress = source.LimitProgress! with { DuelFlags = (ushort)(source.LimitProgress.DuelFlags | 0x3F), Options978 = (ushort)(source.LimitProgress.Options978!.Value & ~2) } };
        BattleSession battle = BattleSession.Restore(new BattleSession(source, BattleAtbMode.TurnBased).Capture());
        Attach(battle);
        battle.ConfigureDiagnosticScheduling(true);
        DiagnosticDuelCycleDriver? cycle = null;
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            Assert.NotNull(cycle);
            cycle.Observe(Observation(), false);
            NativeBattleActionPacketsState packets = dispatch.ActionId == 65532 ? battle.ObserveDiagnosticDuelClosingPackets(dispatch) : battle.ObserveDiagnosticActionPackets(dispatch);
            BattleDiagnosticEffectInvocation invocation = battle.ObserveDiagnosticEffectInvocation(dispatch);
            NativeBattleActionHeaderSlot header = packets.Headers[dispatch.NativeHeaderIndex!.Value];
            cycle.ObserveDispatch(dispatch, new(dispatch.Serial, dispatch.Callback, dispatch.NativeHeaderIndex.Value, dispatch.NativeHeaderGeneration!.Value, packets)
            {
                Parameter80 = invocation.Parameter80,
                Parameter82 = invocation.Parameter82,
                HeaderEffect = (ushort)(header.ReadByte(6) | header.ReadByte(7) << 8),
                RetainedEffect = invocation.ExecutedEffect,
                Duel = battle.GetNativeDuel("party")
            });
        };
        DiagnosticDuelCycleReceipt? first = null;
        bool coldChecked = false;
        for (int execution = 1; execution <= 2; execution++)
        {
            if (execution == 2)
            {
                battle.Tick();
            }
            for (int callback = 0; callback < 1000 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            BattleDiagnosticLimitSelectionReceipt selection = battle.SelectDiagnosticLimit("party", new(1, 1, 256));
            Assert.True(selection.Accepted);
            cycle = new(new(execution, "party", "enemy", 0, 3, new(0), selection, Observation()));
            battle.QueueNativePlayerCommand("party", "enemy", 11, 0);
            for (int callback = 0; callback < 2400 && !cycle.Completed; callback++)
            {
                battle.Tick();
                bool drained = battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0;
                cycle.Observe(Observation(), drained);
                BattleNativeDuelState? state = battle.GetNativeDuel("party");
                if (cycle.NeedsInput && state is { Phase: BattleNativeDuelPhase.Ready })
                {
                    BattleNativeDuelChoice choice = RecreateGame.FindDiagnosticDuelChoice(rules, state.MoveSetId, 0, battle.GetNativeDuelChoices("party"));
                    cycle.BeginInput(choice, Observation());
                }
                battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60 + 1), state is null ? (ushort)0 : cycle.Buttons(state));
                cycle.Observe(Observation(), false);
                if (!coldChecked && cycle.Receipt.Inputs.Length > 0)
                {
                    BattleSnapshot actual = battle.Capture();
                    BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(actual))!);
                    Attach(cold);
                    Assert.NotNull(cold.GetNativeDuel("party"));
                    battle.Tick();
                    cold.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                    coldChecked = true;
                }
            }
            Assert.True(cycle.Completed);
            DiagnosticDuelCycleValidation.ValidateReceipt(cycle.Receipt, new(BattleInputKind.Action, ActorId: "party", TargetId: "enemy", Ability: BattleAbility.LimitBreak, ActionId: 11 << 16), 274, cycle.Receipt.Dispatches, cycle.Receipt.BeforeOpening.Callback, cycle.Receipt.Completed!.Callback);
            Assert.Null(battle.GetNativeDuel("party"));
            Assert.Empty(battle.ObserveDiagnosticDrainBlockers());
            Assert.Equal(65532, cycle.Receipt.Dispatches[^1].ActionId);
            Assert.Equal((ushort)0, cycle.Receipt.DispatchPackets[^1].HeaderEffect);
            Assert.Equal((byte)8, cycle.Receipt.DispatchPackets[^1].Parameter80);
            if (first is null)
            {
                first = cycle.Receipt;
            }
            else
            {
                DiagnosticDuelCycleValidation.ValidateChain(first, cycle.Receipt);
            }
        }
        Assert.True(coldChecked);

        DiagnosticDuelObservation Observation() => new(battle.NativeHostCallbacks!.Value, battle.GetNativeDuel("party"));
        void Attach(BattleSession session) => session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
    }
}
