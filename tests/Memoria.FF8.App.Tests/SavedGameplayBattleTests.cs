using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SavedGameplayBattleTests
{
    [Theory]
    [InlineData(BattleAtbMode.Normal, BattleGunbladeMode.Automatic, false, BattleDifficulty.Normal, false, false, false)]
    [InlineData(BattleAtbMode.TurnBased, BattleGunbladeMode.Manual, true, BattleDifficulty.Hard, true, true, true)]
    public void Live_battle_initializes_from_its_save_choices_and_snapshot_keeps_them(BattleAtbMode mode, BattleGunbladeMode gunblade, bool guardianForms, BattleDifficulty difficulty, bool limitOncePerTurn, bool drawOncePerSpell, bool selphieCardSlots)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with
        {
            LimitOncePerTurn = limitOncePerTurn,
            SelphieCardSlots = selphieCardSlots,
            DrawOncePerSpell = drawOncePerSpell,
            Difficulty = difficulty,
            AtbMode = mode,
            GunbladeMode = gunblade,
            GuardianFormsEnabled = guardianForms
        });
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: mode, NativeSeed: ExpandedBattleInstalledEntry.Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        Assert.Equal(limitOncePerTurn, battle.Capture().Definition.LimitOncePerTurn);
        Assert.Equal(drawOncePerSpell, battle.Capture().Definition.DrawOncePerSpell);
        Assert.Equal(selphieCardSlots, battle.Capture().Definition.SelphieCardSlots);
        Assert.Equal(difficulty, battle.Difficulty);
        Assert.Equal(mode, battle.Mode);
        Assert.Equal(gunblade, battle.GunbladeMode);
        Assert.Equal(guardianForms, battle.GuardianFormsEnabled);
        BattleSession restored = battle.ForkForContinuation();
        Assert.Equal(limitOncePerTurn, restored.Capture().Definition.LimitOncePerTurn);
        Assert.Equal(drawOncePerSpell, restored.Capture().Definition.DrawOncePerSpell);
        Assert.Equal(selphieCardSlots, restored.Capture().Definition.SelphieCardSlots);
        Assert.Equal(difficulty, restored.Difficulty);
        Assert.Equal(mode, restored.Mode);
        Assert.Equal(gunblade, restored.GunbladeMode);
        Assert.Equal(guardianForms, restored.GuardianFormsEnabled);
    }
}
