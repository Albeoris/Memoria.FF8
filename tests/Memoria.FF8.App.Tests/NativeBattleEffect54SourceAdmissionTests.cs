using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect54SourceAdmissionTests
{
    [Fact]
    public void Archived_effect54_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT54_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect54 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(236, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(76, definition.GetProperty("StageId").GetInt32());
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(236);
        BattleEnemyPlacement dormant = Assert.Single(formation.Slots, actor => actor.ModelId == 83 && actor.Slot == 0);
        Assert.False(dormant.Enabled);
        Assert.True(dormant.Hidden);
        Assert.True(dormant.InitiallySuspended);
        Assert.True(dormant.Untargetable);
    }
}
