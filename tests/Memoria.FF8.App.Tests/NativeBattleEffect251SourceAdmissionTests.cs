using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect251SourceAdmissionTests
{
    [Fact]
    public void Archived_effect251_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect251 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(119, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(33, definition.GetProperty("StageId").GetInt32());
        using GameAssets game = new(directory!);
        NativeBattleEffect251Resources resources = game.ReadNativeEffect251Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([251])[250];
        Assert.Same(resources, binding.Effect251Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Null(binding.InitializerResource);
    }
}
