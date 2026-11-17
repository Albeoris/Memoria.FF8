using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect249SourceAdmissionTests
{
    [Fact]
    public void Archived_effect249_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT249_SOURCE_SNAPSHOT");
        Assert.SkipUnless(path is not null, "Select the archived Effect249 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(194, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(52, definition.GetProperty("StageId").GetInt32());
    }
}
