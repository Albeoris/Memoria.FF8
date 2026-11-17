using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect345SourceAdmissionTests
{
    [Fact]
    public void Archived_effect345_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT345_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect345 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(462, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(121, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        NativeBattleEffect345Resources resources = game.ReadNativeEffect345Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([345])[344];
        Assert.Same(resources, binding.Effect345Resources);
        Assert.Equal(NativeBattleEffect345Profile.Entry, binding.Entry);
    }
}
