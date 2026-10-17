using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleInitialStatusPaletteTests
{
    [Fact]
    public void Initial_petrify_projects_both_original_party_palette_rows_through_509D10()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        int neighborWeapon = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id;
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, NativeStatus0 = 4, NativeDeathCount = 0 },
            new() { Id = "neighbor", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = neighborWeapon, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        BattleSession session = new(new()
        {
            EncounterId = 94,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        }, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && (session.NativeGraphics!.Models[0].Flags0 & 2) == 0; callback++)
        {
            session.Tick();
        }

        NativeBattleGraphicsModel model = session.NativeGraphics!.Models[0];
        Assert.NotEqual(0, model.Flags0 & 2);
        Assert.Equal(0x10u, model.Flags8!.Value & 0x410);
        Assert.Equal((byte?)0, model.StatusTimers[4]);
        byte[] original = session.NativeModelCpuResources!.Palettes.Read(0, 1024);
        byte[] expected = NativeBattleStatusPalette.Transform509E00(original, model.Flags8.Value).ToArray();
        Assert.False(original.AsSpan().SequenceEqual(expected));
        NativeBattleEffectTextureState textures = session.NativeEffectMemory!.Textures!;
        Assert.Equal(0, textures.Count);
        NativeBattleEffectArena vram = new(textures.VramRegions);
        Assert.Equal(expected[..512], vram.Read(224 * 2048, 512));
        Assert.Equal(expected[512..], vram.Read(225 * 2048, 512));

        string json = JsonSerializer.Serialize(session.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(expected[..512], new NativeBattleEffectArena(restored.NativeEffectMemory!.Textures!.VramRegions).Read(224 * 2048, 512));
        Assert.Equal(expected[512..], new NativeBattleEffectArena(restored.NativeEffectMemory.Textures.VramRegions).Read(225 * 2048, 512));
    }
}
