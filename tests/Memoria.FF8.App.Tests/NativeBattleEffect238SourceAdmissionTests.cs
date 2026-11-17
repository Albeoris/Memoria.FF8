using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect238SourceAdmissionTests
{
    [Fact]
    public void Archived_effect238_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT238_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect238 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(117, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(32, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([238])[237];
        Assert.Equal(0xA1FDC0u, binding.Entry);
        Assert.Equal(0xA1FD90u, binding.Initializer);
    }
}
