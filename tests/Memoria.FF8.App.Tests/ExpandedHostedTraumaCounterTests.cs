using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedTraumaCounterTests
{
    [Fact]
    public void Installed_Trauma_counter_targets_and_activates_its_dormant_Droma()
    {
        const ushort formation = 431;
        const ushort effect = 101;
        const int callbackBudget = 1200;
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleRoster roster = new([], 1) { DonorCount = 0, AuthoredFormationHash = assets.FormationSourceHash(formation) };
        BattleSession battle = world.StartDiagnosticBattle(new(formation, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Roster = roster
        });
        try
        {
            ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
            ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
            Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
            BattleCombatant trauma = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
            Assert.Equal(109, trauma.ModelId);
            Assert.Equal(1, trauma.SceneSlot);
            string scriptSha256 = Convert.ToHexString(SHA256.HashData(battle.GetCombatantDefinition(trauma.Id).NativeAi!.Scripts[2]));
            List<BattleDiagnosticEffectDispatch> invocations = [];
            battle.DiagnosticEffectDispatched += dispatch =>
            {
                if (dispatch.RequestedEffect == effect)
                {
                    invocations.Add(dispatch);
                }
            };

            int eventStart = battle.Events.Count;
            int initialHp = trauma.CurrentHp;
            foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer))
            {
                actor.Atb = 0;
            }
            BattleCombatant? droma = null;
            int? dormantHp = null;
            bool selectedDroma = false;
            bool activatedDroma = false;
            for (int callback = 0; callback < callbackBudget; callback++)
            {
                if (droma is null && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    trauma.Atb = BattleSession.AtbMaximum;
                }
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (droma is null && battle.Events.Skip(eventStart).FirstOrDefault(entry => entry.Kind == "NativeSpawn" && entry.ActorId == trauma.Id) is { } spawn)
                {
                    droma = Assert.Single(battle.Combatants, actor => actor.Id == spawn.TargetId);
                    Assert.Equal(110, droma.ModelId);
                    Assert.Equal(0, droma.SceneSlot);
                    Assert.Equal(4, droma.RequireNativeActorIndex());
                }
                if (droma is null)
                {
                    continue;
                }
                if (droma is { IsAlive: true, Hidden: true, Suspended: true, Untargetable: true })
                {
                    dormantHp ??= droma.CurrentHp;
                }
                activatedDroma |= battle.Events.Skip(eventStart).Any(entry => entry.Kind == "Activated" && entry.ActorId == droma.Id);
                if (!selectedDroma && process.Graphics.OriginalEffect is { Parameters: { } parameters } current && current.Binding.Effect == effect)
                {
                    NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(parameters.Header);
                    Assert.Equal(battle.ReadPresentationModelOwner(droma.Id), process.ActionPackets.ReadContact(header.FirstContact).Primary.Recipient);
                    Assert.NotNull(dormantHp);
                    Assert.False(activatedDroma);
                    selectedDroma = true;
                }
                if (selectedDroma && activatedDroma && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    break;
                }
            }

            Assert.NotNull(droma);
            Assert.True(trauma.IsAlive && trauma.CurrentHp == initialHp);
            BattleDiagnosticEffectDispatch invocation = Assert.Single(invocations);
            Assert.Equal(trauma.Id, invocation.ActorId);
            Assert.Equal(290, invocation.ActionId);
            Assert.Equal((byte?)8, invocation.NativeCommand);
            Assert.Equal(effect, invocation.ExecutedEffect);
            Assert.Equal((byte?)2, invocation.NativeAiCallbackStage);
            Assert.Equal(scriptSha256, invocation.NativeAiScriptSha256);
            Assert.True(selectedDroma, "Effect 101 did not present the original dormant allocation as its recipient.");
            Assert.True(activatedDroma && droma.IsAlive && !droma.Hidden && !droma.Suspended && !droma.Untargetable);
            Assert.Equal(dormantHp, droma.CurrentHp);
            Assert.DoesNotContain(battle.Events.Skip(eventStart), entry => entry.Kind == "EffectContact" && entry.ActorId == trauma.Id && entry.TargetId == droma.Id);
            Assert.Equal(0u, trauma.AiVariables[0]);
            Assert.Empty(battle.ObserveDiagnosticDrainBlockers());
            Assert.Same(process, battle.ExpandedPresentationProcess);
        }
        finally
        {
            world.EndDiagnosticBattle();
        }
    }
}
