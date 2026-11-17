using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect231SourceAdmissionTests
{
    [Fact]
    public void Archived_effect231_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT231_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect231 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(67, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(14, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(67);
        Assert.Equal(14, formation.StageId);
        Assert.Equal((byte)0, formation.Flags);
        Assert.Equal(8, formation.Slots.Count);
        BattleEnemyPlacement enemy = Assert.Single(formation.Slots, slot => slot.Enabled);
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(25, enemy.ModelId);
        Assert.Equal(41, enemy.NativeModelId);
        Assert.Equal(255, enemy.Level);
        Assert.Equal(new NativeBattleVector(-100, 0, -1400), enemy.NativePosition);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([231])[230];
        Assert.Equal(0xA664A0u, binding.Entry);
        Assert.Equal(0xA66470u, binding.Initializer);
    }
}
