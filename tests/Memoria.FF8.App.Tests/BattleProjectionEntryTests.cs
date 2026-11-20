using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleProjectionEntryTests
{
    [Fact]
    public void BalambEntryRecoversAnUndersizedModelProjectionWorkspace()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[3] = records[3] with { MemberFlags = (ushort)(records[3].MemberFlags | 1), Status = 0 };
        GameSession session = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            CharacterVitals = (vitals with { Records = records, CombatParty = [3, 0, 255] }).RebuildParty().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        });
        using GameWorld world = new(session, directory);
        const ushort formation = 513;
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        foreach (byte weapon in world.BattleResources.WeaponDefinitions.Where(weapon => weapon.CharacterId == 3).Select(weapon => checked((byte)weapon.Id)))
        {
            records[3] = records[3] with { Weapon = weapon };
            session.SetCharacterVitals((session.CharacterVitals! with { Records = records }).RebuildParty().Rest());
            BattleDefinition definition = world.CreateEncounterBattle(formation);
            Assert.Contains(definition.Combatants, actor => actor.IsPlayer && actor.ModelId == 3 && actor.WeaponId == weapon);
            NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, new NativeMusicProcessState(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
            definition = definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess };
            BattleSession battle = GameWorld.CreateLiveBattleSession(definition, new GameplayPreferences { AtbMode = BattleAtbMode.TurnBased }, assets, world.BattleResources);
            for (int callback = 0; callback < 600 && !battle.NativeEntryReady; callback++)
            {
                battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromMilliseconds(34));
            }
            Assert.True(battle.NativeEntryReady);

            BattlePresentationProcess presentation = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            NativeBattleModelCpuResources cpu = Assert.IsType<NativeBattleModelCpuResources>(presentation.NativeModelCpuResources);
            // Reproduce a model larger than the retained workspace on the ordinary battle callback.
            typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeModelCpuResources))!.SetValue(presentation, cpu with { ScratchMaximum = 1 });
            for (int callback = 0; callback < 60; callback++)
            {
                battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromMilliseconds(34));
            }
            ushort expanded = Assert.IsType<ushort>(presentation.NativeModelCpuResources!.ScratchMaximum);
            Assert.True(expanded > 1);
            BattleSession fork = battle.ForkForContinuation();
            fork.AdvanceNativeGraphicsPresentation(TimeSpan.FromMilliseconds(34));
            Assert.Equal(expanded, fork.Presentation!.NativeModelCpuResources!.ScratchMaximum);
        }
    }
}
