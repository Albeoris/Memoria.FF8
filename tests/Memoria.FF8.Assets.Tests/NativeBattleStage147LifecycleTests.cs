using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStage147LifecycleTests
{
    [Theory]
    [InlineData(136, 88)]
    [InlineData(1000, 0)]
    public void OriginalIntroLoadsPartyDescentAndEnemySequenceThenContinuesAfterJsonRestore(int encounterId, int enemyModel)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(encounterId);
        Assert.Equal(147, encounter.StageId);
        BattleEnemyPlacement enemy = Assert.Single(encounter.Enemies);
        Assert.Equal(enemyModel, enemy.ModelId);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party0", Name = "Squall", IsPlayer = true, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, ModelId = 0, WeaponId = 0 },
            new() { Id = "party1", Name = "Zell", IsPlayer = true, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, ModelId = 1, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id },
            new() { Id = "party2", Name = "Irvine", IsPlayer = true, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, ModelId = 2, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 2).Id },
            new() { Id = "enemy", Name = assets.ReadEnemyName(enemyModel), ModelId = enemyModel, SceneSlot = enemy.Slot, Information246 = assets.ReadEnemyNativeInformation246(enemyModel), Speed = 1, NativeAi = assets.ReadEnemyAi(enemyModel, 1), NativeCombatantFlags = 0x10 }
        ];
        NativeBattleGraphicsResource enemyResource = assets.ReadNativeGraphicsResource(actors[3]);
        NativeBattleMusicEntryPreparation music = NativeBattleMusicFixture.Prepare(game, encounterId);
        BattleDefinition definition = new()
        {
            EncounterId = encounterId,
            StageId = encounter.StageId,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = music.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = music.SoundProcess,
            NativeGlobalRandom = new(0),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraControl = stage.NativeCameraControl,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            Combatants = actors
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        bool[] visited = new bool[7];
        bool restoredDescent = false;
        bool sawPartySequence = false;
        bool sawEnemySequence = false;
        BattleSession? restored = null;
        for (int callback = 0; callback < 1800; callback++)
        {
            battle.Tick();
            restored?.Tick();
            NativeBattleStageState state = battle.NativeStage!;
            if (state.Task is null)
            {
                continue;
            }
            Assert.Equal(NativeBattleStage147.CallbackAddress, state.Task.Callback.Address);
            NativeBattleStage147State intro = state.Extended!.Stage147;
            if (intro.Subphase >= 0)
            {
                visited[intro.Subphase] = true;
            }
            if (intro.Subphase is >= 0 and <= 4)
            {
                Assert.Equal((byte)2, battle.NativeGraphics!.UiServices!.Windows!.RequestedVitalsMask);
            }
            if (intro.Subphase == 2 || intro.Subphase == 5 && intro.Counter is >= 1 and <= 5)
            {
                Assert.Equal((byte)1, battle.NativeGraphics!.Effects.PublishedCount);
            }
            if (intro.Subphase == 3)
            {
                Assert.Equal((byte)26, battle.NativeGraphics!.Models[0].Sequence);
                Assert.Equal((byte)26, battle.NativeGraphics.Models[2].Sequence);
                Assert.Equal((short)2048, battle.NativeGraphics.Models[0].Rotation14);
                sawPartySequence = true;
            }
            if (intro.Subphase == 4 && intro.Counter == 7 && !restoredDescent)
            {
                string json = JsonSerializer.Serialize(battle.Capture());
                restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
                Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
                restoredDescent = true;
            }
            if (intro.Subphase == 4 && intro.Counter > 0)
            {
                foreach (int index in new[] { 0, 2 })
                {
                    NativeBattleModelTransform transform = battle.NativeGraphics!.Models[index].Transform!;
                    Assert.Equal(NativeBattleStage147.Descent511EF0(transform.Initial20.Z, intro.Counter), transform.Position28.Z);
                }
            }
            if (intro.Subphase == 6 && !sawEnemySequence)
            {
                if (enemyResource.ContainsNativeIndex505850(16))
                {
                    Assert.Equal((byte)16, battle.NativeGraphics!.Models[3].Sequence);
                }
                else
                {
                    Assert.NotEqual(0, battle.NativeGraphics!.Models[3].ResourceFlags44!.Value & 0x40);
                }
                Assert.Equal((byte)7, battle.NativeGraphics.UiServices!.Windows!.RequestedVitalsMask);
                Assert.Equal((short)0, battle.NativeGraphics.Models[0].Rotation14);
                sawEnemySequence = true;
            }
            if (restored is not null)
            {
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
            if (intro.Subphase == -1 && battle.NativeEntryReady)
            {
                break;
            }
        }
        Assert.All(visited, value => Assert.True(value));
        Assert.True(sawPartySequence);
        Assert.True(sawEnemySequence);
        Assert.True(restoredDescent);
        Assert.Equal((short)-1, battle.NativeStage!.Extended!.Stage147.Subphase);
        Assert.True(battle.NativeEntryReady);
        Assert.Equal((ushort)0, battle.NativeCamera!.BlendProgress);
        Assert.NotEmpty(battle.NativeGraphics!.PrimitiveFrame!.LastCommands);
        battle.Tick();
        restored!.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
