using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedHealingTargetTests
{
    [Fact]
    public void SelectedCompletedComponentResumesOneNativeCallback()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_COMPONENT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && output is not null && directory is not null, "Select a completed component snapshot, installed resources and an evidence directory.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input!))!;
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSession direct = BattleSession.Restore(snapshot);
        BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.SerializeToUtf8Bytes(snapshot))!);
        foreach (BattleSession session in new[] { direct, cold })
        {
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            session.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond));
        }
        Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(direct, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224).ToArray());
        Directory.CreateDirectory(output!);
        File.WriteAllBytes(Path.Combine(output!, "component-next.battle.json"), JsonSerializer.SerializeToUtf8Bytes(cold.Capture()));
    }

    [Fact]
    public void SelectedZombieEnemyReceivesOrdinaryHealingAsDamageAndContinuesAfterRestore()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && output is not null && directory is not null, "Select a battle save, installed resources and an evidence directory.");
        GameState source = GameSession.Load(input!);
        BattleSnapshot initial = Assert.IsType<BattleSnapshot>(source.Battle);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSession battle = Restore(initial);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive && (battle.ReadStatusPresentation(actor.Id).Saved & 0x40) != 0);
        var choice = (from actor in battle.Combatants
                      where actor.IsPlayer && actor.IsAlive
                      from action in initial.Definition.ActionDefinitions
                      where action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == 3 && battle.GetAvailableMagicCasts(actor.Id, action.Id) > 0
                      select new { ActorId = actor.Id, Action = action }).First();
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        for (int ready = 0; ready < 512 && !battle.CanChoosePlayerCommand(choice.ActorId); ready++)
        {
            battle.AdvanceTurn();
            battle.AdvanceNativeGraphicsPresentation(callback);
        }
        Assert.True(battle.CanChoosePlayerCommand(choice.ActorId));
        battle.SelectPlayerCommandActor(choice.ActorId);
        int hp = battle.Combatants.Single(actor => actor.Id == target.Id).CurrentHp;
        battle.QueueAction(choice.ActorId, target.Id, BattleAbility.Magic, choice.Action.Id);
        string path = Path.Combine(output!, "healing-target");
        Directory.CreateDirectory(path);
        Save("admitted");
        bool damage = false;
        for (int count = 0; count < 512; count++)
        {
            battle.AdvanceNativeGraphicsPresentation(callback);
            damage |= battle.Combatants.Single(actor => actor.Id == target.Id).CurrentHp < hp;
            BattleSnapshot current = battle.Capture();
            if (damage && current.ActiveAction is null && current.QueuedActions.Length == 0 && current.NativeCommandBanks?.BoundCommands.Any() != true && current.NativeTimedAction is null && current.NativeActionScratch?.ComponentCompletion is null && current.NativeGraphics is { Requests.IsEmpty: true, Descriptors.Queue.IsEmpty: true, OriginalEffect.ReturnedDescriptor: 0 })
            {
                string saved = Save("completed");
                BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(saved))!);
                battle.AdvanceNativeGraphicsPresentation(callback);
                cold.AdvanceNativeGraphicsPresentation(callback);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                Assert.Equal(new BattleScenePresentation(assets, initial.Definition, true).Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, initial.Definition, true).Render(cold, 320, 224).ToArray());
                Save("completed-next");
                File.WriteAllText(Path.Combine(path, "result.json"), JsonSerializer.Serialize(new { source.FieldId, SourceFrame = initial.Frame, current.Frame, NativeCallback = current.NativeGraphics.Callback, choice.ActorId, choice.Action.Name, choice.Action.Id, TargetId = target.Id, BeforeHp = hp, AfterHp = current.Combatants.Single(actor => actor.Id == target.Id).CurrentHp, Callbacks = count + 1, RestoredNextEqual = true }));
                return;
            }
        }
        Assert.Fail("The ordinary healing action must damage the selected Zombie enemy and release within 512 callbacks.");

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        string Save(string name)
        {
            string file = Path.Combine(path, name + ".battle.json");
            File.WriteAllBytes(file, JsonSerializer.SerializeToUtf8Bytes(battle.Capture()));
            return file;
        }
    }
}
