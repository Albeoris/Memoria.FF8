using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BadBreathLateTargetTests
{
    [Fact]
    public void Quistis_completes_Bad_Breath_when_target_ACT_outlasts_the_strip()
    {
        string source = TestResources.FixturePath("bad-breath-late-target-source");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(source, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        RandomEncounterSessionState random = session.State.RandomEncounters!;
        session.SetRandomEncounters(random with { Enabled = true, Pending = new(SceneKind.WorldMap, session.State.FieldId, scene.State.Frame, 693, scene.AllocationProgramSha256) });
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 100));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        Assert.DoesNotContain(battle.QueuedActions, command => command.NativePlayerReaction == BattleNativePlayerReaction.AutomaticGuardian);

        BattleCombatant quistis = Assert.Single(battle.Combatants, actor => actor.Id == "party-3");
        BattleCombatant target = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-0" && actor.IsActive);
        quistis.Atb = BattleSession.AtbMaximum;
        quistis.CurrentHp = 1;
        int action = (15 << 16) | 10;
        for (int roll = 0; roll < 128 && battle.GetNativeLimitLevel(quistis.Id) != 3; roll++)
        {
            quistis.LimitTurnCrisis = null;
            battle.RefreshNativeLimit(quistis.Id);
        }
        Assert.Equal(3, battle.GetNativeLimitLevel(quistis.Id));
        Assert.Contains(battle.GetNativePlayerActionChoices(quistis.Id, true), choice => choice.Id == action);
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: quistis.Id, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: action));
        Assert.Equal((byte)3, Assert.Single(battle.QueuedActions, command => command.ActorId == quistis.Id).NativeAction!.CrisisLevel);

        bool drewLateAct = false;
        for (int tick = 0; tick < 320 && quistis.IsBusy; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.Presentation?.Graphics.OriginalEffect is { RequestedEffect: 300, Pools.Length: 4 } effect && effect.Pools[1].Head is null && effect.Pools[2].Head is not null)
            {
                NativeBattlePreparedPresentation prepared = Assert.IsType<NativeBattlePreparedPresentation>(battle.PreparedPresentation);
                drewLateAct |= prepared.Primitives.LastCommands.Any(command => command.Command.PilotGeometry?.Identity is { Effect: 300 } identity && identity.Bank == effect.Pools[2].DescriptorAddress);
            }
        }

        Assert.True(drewLateAct);
        Assert.False(quistis.IsBusy);
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == quistis.Id);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == quistis.Id && entry.TargetId == target.Id);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
    }
}
