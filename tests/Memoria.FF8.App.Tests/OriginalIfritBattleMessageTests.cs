using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalIfritBattleMessageTests
{
    [Fact]
    public void OriginalIfritCounterReachesItsShivaMessageAndTheHudResolvesTheCurrentGfName()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        const int model = 61;
        const int level = 6;
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(model, level);
        BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(model).Project(level);
        BattleNativeAction shiva = assets.NativeActions.Single(action => action.Command == 254 && action.ActionId == 65);
        int encounter = Enumerable.Range(0, 1024).First(id => assets.LoadEncounter(id).Enemies.Any(enemy => enemy.ModelId == model));
        BattleSession battle = new(new BattleDefinition
        {
            Name = "Original Ifrit completed GF packet component",
            EncounterId = encounter,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = 1,
            NativeSavedBattleFlags = 0,
            NativeSavedBattleFlagsKnownMask = 255,
            NativeExamTimerZero = false,
            PersistentAiVariables = new uint[8],
            Combatants =
            [
                new() { Id = "party-0", Name = "Squall", IsPlayer = true, MaximumHp = 9999, CurrentHp = 9999, Magic = 20, Level = level, InitialAtb = BattleSession.AtbMaximum, NativeCommands = ImmutableArray.Create(new BattleNativeCommandBinding(254, "Completed Shiva", shiva.TargetFlags!.Value, 65)) },
                new() { Id = "ifrit", Name = assets.ReadEnemyName(model), ModelId = model, SceneSlot = 0, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = stats.Speed, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = ai, NativeTemplate = assets.ReadEnemyTemplate(model, level) }
            ]
        }, BattleAtbMode.Normal);

        // The explicit standalone component begins at the native completed-GF packet, without replaying a game or a charge animation.
        battle.QueueNativePlayerCommand("party-0", "ifrit", 254, 65);
        for (int frame = 0; frame < 120 && !battle.Events.Any(entry => entry.Kind == "NativeText" && entry.Message == ai.Texts[3]); frame++)
        {
            battle.Tick();
        }

        Assert.Contains(battle.Events, entry => entry.Kind == "NativeText" && entry.Message == ai.Texts[3]);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, guardianForceName: id => id == 1 ? "Frost" : GameNames.DefaultName(GameNames.GuardianForceSubject(id)));
        hud.BuildView();
        Assert.Contains("Frost", hud.BattleMessage!);
        Assert.DoesNotContain("{Name", hud.BattleMessage!);
        Assert.DoesNotContain("{Color", hud.BattleMessage!);
    }
}
