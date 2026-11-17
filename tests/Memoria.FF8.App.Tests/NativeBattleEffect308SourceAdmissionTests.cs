using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect308SourceAdmissionTests
{
    [Fact]
    public void Archived_effect308_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT308_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect308 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(794, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(65, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(794);
        Assert.Equal(65, formation.StageId);
        Assert.Equal(3, formation.Enemies.Count);
        NativeBattleEffect308Resources resources = game.ReadNativeEffect308Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([308])[307];
        Assert.Same(resources, binding.Effect308Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource(TextureName, InitializerGlobal), binding.InitializerResource);
    }
}
