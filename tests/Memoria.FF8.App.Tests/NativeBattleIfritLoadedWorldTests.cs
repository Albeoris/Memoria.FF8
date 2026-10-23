using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleIfritLoadedWorldTests
{
    [Fact]
    public void Loaded_world_party_enters_Ifrit_form_and_completes_attack_and_Fire_after_restore()
    {
        string source = TestResources.FixturePath("App.Tests.IfritFormLoadedSave.1");
        string directory = TestResources.GameDirectory;
        using GameAssets resources = new(directory);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        GameWorld.PreparedBattleEntry entry = world.PrepareBattleEntry(512, 0, 0, BattleAtbMode.TurnBased);
        BattleDefinition definition = entry.Battle.Capture().Definition;
        // Keep this loaded-party presentation component independent of enemy turn admission.
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeCombatantFlags = (ushort)(actor.NativeCombatantFlags | 8) }).ToArray() };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, session.State.GameplayPreferences);
        battle.AttachNativeEffectResourceResolver(effect => resources.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        string? actorId = null;
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_FORM_EVIDENCE");
        output = output is null ? null : Path.Combine(output, "loaded-world-512");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        try
        {
            WaitFor(() => (actorId = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && battle.CanRequestGuardianForm(actor.Id, 2))?.Id) is not null);
            Publish("before-entry");
            battle.RequestGuardianForm(actorId!, 2);
            WaitFor(() => battle.GetGuardianForm(actorId!)?.Phase == BattleGuardianFormPhase.Active);
            RoundTripNext();
            foreach (BattleGuardianFormAction action in new[] { BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire })
            {
                WaitFor(() => battle.CanChoosePlayerCommand(actorId!));
                string target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive).Id;
                int damage = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == actorId);
                Publish("before-" + action);
                battle.QueueGuardianFormAction(actorId!, target, action);
                WaitFor(() => battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == actorId) > damage && battle.ActiveAction is null);
                RoundTripNext();
            }
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(actorId!)!.Phase);
            Publish("after-Fire");
        }
        catch
        {
            Publish("failure");
            throw;
        }

        void Publish(string name)
        {
            if (output is not null)
            {
                File.WriteAllText(Path.Combine(output, name + ".battle.json"), JsonSerializer.Serialize(battle.Capture()));
            }
        }

        void RoundTripNext()
        {
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            restored.AttachNativeEffectResourceResolver(effect => resources.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            battle = restored;
        }

        void WaitFor(Func<bool> ready)
        {
            for (int callback = 0; callback < 1500 && !ready(); callback++)
            {
                if (battle.ActiveAction is null)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(ready(), "Loaded world party did not reach the requested Ifrit boundary.");
        }
    }
}
