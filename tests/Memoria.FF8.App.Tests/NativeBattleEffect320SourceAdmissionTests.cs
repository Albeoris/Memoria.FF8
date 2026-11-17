using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect320SourceAdmissionTests
{
    [Fact]
    public void Archived_effect320_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT320_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect320 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(483, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(139, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([320])[319];
        Assert.Same(resources, binding.Effect320Resources);
        Assert.Equal(NativeBattleEffect320Profile.Entry, binding.Entry);
    }
}
