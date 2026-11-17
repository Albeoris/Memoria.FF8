using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect31SourceAdmissionTests
{
    [Fact]
    public void Archived_effect31_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT31_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect31 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(110, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(24, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([31])[30];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Same(resources, binding.Effect31Resources);
    }
}
