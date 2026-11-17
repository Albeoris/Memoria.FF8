using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect287SourceAdmissionTests
{
    [Fact]
    public void Archived_effect287_source_identifies_its_original_arena()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT287_SOURCE_SNAPSHOT");
        Assert.SkipUnless(path is not null, "Select the archived Effect287 source.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path!));
        JsonElement definition = document.RootElement.GetProperty("Definition");
        Assert.Equal(431, definition.GetProperty("EncounterId").GetInt32());
        Assert.Equal(126, definition.GetProperty("StageId").GetInt32());
    }
}
