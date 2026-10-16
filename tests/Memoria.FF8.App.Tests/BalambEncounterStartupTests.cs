using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BalambEncounterStartupTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(24)]
    public void OriginalBalambFormationsStartThroughTheCurrentGameHost(byte ground)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original encounter and participant resources are required.");
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        // This isolated component owns explicit installed initial party records; it makes no route or saved gameplay claim.
        GameSession session = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "synthetic-balamb-encounter-startup-component",
            CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame()
        });
        using GameWorld world = new(session, directory);
        NativeWorldEncounterDefinition encounters = Assert.IsType<NativeWorldEncounterDefinition>(worldAssets.ReadRandomEncounterDefinition(24576, -29406, ground, 1));

        foreach (ushort formation in encounters.Formations.Distinct())
        {
            BattleDefinition definition = world.CreateEncounterBattle(formation);
            BattleSession battle = new(definition, BattleAtbMode.Normal);

            Assert.Equal(formation, battle.EncounterId);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Equal(0, battle.Frame);
            Assert.Contains(battle.Combatants, actor => actor.IsPlayer && actor.IsActive);
            Assert.Contains(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
            Assert.All(definition.Combatants.Where(actor => !actor.IsPlayer), actor => Assert.NotNull(actor.NativeAi));
        }
    }
}
