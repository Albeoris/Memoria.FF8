using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect316SourceAdmissionTests
{
    [Fact]
    public void Archived_effect316_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect316 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(441, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(124, definition.GetProperty("StageId").GetInt32());
        using BattleAssets assets = new(directory!);
        BattleEnemyPlacement placement = Assert.Single(assets.LoadEncounter(441).Enemies);
        Assert.Equal(130, placement.ModelId);
        Assert.Equal(0, placement.Slot);
        Assert.True(placement.Enabled);
    }
}
