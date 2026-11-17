using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect233SourceAdmissionTests
{
    [Fact]
    public void Archived_effect233_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT233_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect233 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(185, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(48, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(185);
        Assert.Equal(48, formation.StageId);
        Assert.Equal((byte)0, formation.Flags);
        Assert.Equal(8, formation.Slots.Count);
        BattleEnemyPlacement enemy = Assert.Single(formation.Slots, slot => slot.Enabled);
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(27, enemy.ModelId);
        Assert.Equal(43, enemy.NativeModelId);
        Assert.Equal(255, enemy.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -1900), enemy.NativePosition);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([233])[232];
        Assert.Equal(0xA51F40u, binding.Entry);
        Assert.Equal(0xA51F10u, binding.Initializer);
    }
}
