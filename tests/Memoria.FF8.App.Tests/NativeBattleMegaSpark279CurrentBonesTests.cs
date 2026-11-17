using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMegaSpark279CurrentBonesTests
{
    [Fact]
    public void Installed_counter_Mega_Spark_uses_its_live_vitals_stack_and_retires()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        (int Formation, int Slot)[] placements = [(419, 0), (387, 0), (512, 1), (92, 3), (388, 2), (35, 0), (328, 1), (822, 0), (549, 0), (723, 1), (375, 2), (395, 0)];
        ImmutableArray<BattleEnemyRosterSource> donors = placements.Select(row => new BattleEnemyRosterSource(row.Formation, row.Slot, world.BattleResources.FormationSourceHash(row.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation squall = new(0, 0, [], [0]) { HitPoints = 486, MagicCommand = new(0, 20) };
        DiagnosticEnemyTrigger trigger = new("enemy-0", 30, 0, null, 2, "DC126F67451AE49CCDE6604CEEB9871C4CDE32932C7DB34622826FFDA504EE08", MaximumAttempts: 8);
        BattleInput attack = new(BattleInputKind.Action, ActorId: "party-10", TargetId: "enemy-0", Ability: BattleAbility.Attack);
        DiagnosticBattleRequest request = new(377, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [squall], true),
            Target = new("enemy-0", 127, 30, 0, 3, 0xC3, 1, null, null),
            Roster = new(donors, 0) { DonorCount = 5 },
            Driver = new(attack, 279) { ExpectedNativeCommand = 8, ExpectedNativeAction = 106, EnemyTrigger = trigger }
        };

        BattleSession battle = world.StartDiagnosticBattle(request);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        int effectStart = 0;
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ExecutedEffect == 279)
            {
                effectStart = battle.Events.Count;
                dispatches.Add(dispatch);
            }
        };
        try
        {
            for (int attempt = 0; attempt < trigger.MaximumAttempts && dispatches.Count == 0; attempt++)
            {
                TickUntil(() => battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0 && RecreateGame.DiagnosticCommandReady(battle, request.Driver!));
                _ = trigger.Prepare(battle);
                world.ApplyBattleInput(attack);
                TickUntil(() => battle.ObserveDiagnosticDrainBlockers().Count == 0);
            }
            BattleDiagnosticEffectDispatch invoked = Assert.Single(dispatches);
            Assert.Equal(trigger.ActorId, invoked.ActorId);
            Assert.Equal(trigger.Callback, invoked.NativeAiCallbackStage);
            Assert.Equal((byte)8, invoked.NativeCommand);
            Assert.Equal(106, invoked.ActionId);
            string?[] recipients = battle.Events.Skip(effectStart).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == trigger.ActorId).Select(entry => entry.TargetId).ToArray();
            Assert.Equal(11, recipients.Length);
            Assert.Equal(11, recipients.Distinct(StringComparer.Ordinal).Count());
            Assert.False(process.HasActiveAction);
            Assert.Equal(0u, process.Graphics.OriginalEffect?.ReturnedDescriptor);
        }
        catch
        {
            world.AbandonFaultedDiagnosticBattle();
            throw;
        }

        void TickUntil(Func<bool> boundary)
        {
            for (int frame = 0; frame < 1200 && !boundary(); frame++)
            {
                if (battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(boundary(), $"Mega Spark did not reach its boundary. Invocations {dispatches.Count}, effect {process.Graphics.OriginalEffect?.RequestedEffect}, blockers [{string.Join(", ", battle.ObserveDiagnosticDrainBlockers())}].");
        }
    }

}
