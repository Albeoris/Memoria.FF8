using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect256SourceAdmissionTests
{
    [Fact]
    public void Archived_effect256_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT256_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect256 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(378, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(98, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([256])[255];
        Assert.Equal(0x979400u, binding.Entry);
        Assert.Equal(0x9793D0u, binding.Initializer);
    }
}
