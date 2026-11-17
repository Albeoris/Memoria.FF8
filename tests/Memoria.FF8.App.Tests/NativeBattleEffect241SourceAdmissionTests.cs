using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect241SourceAdmissionTests
{
    [Fact]
    public void Archived_effect241_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT241_SOURCE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && directory is not null, "Select the archived Effect241 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(164, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(25, definition.GetProperty("StageId").GetInt32());
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(164);
        Assert.Equal(25, formation.StageId);
        Assert.Equal((byte)137, formation.Flags);
        Assert.Equal((byte)0, formation.PrimaryCamera);
        Assert.Equal((byte)0, formation.AlternativeCamera);
        Assert.Equal(8, formation.Slots.Count);
    }
}
