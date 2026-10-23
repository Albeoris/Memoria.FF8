using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModStageBoundaryTests
{
    [Fact]
    public void StageGeometryIsBlockedBeforeInstallationInsteadOfPreviewOnlyReplacement()
    {
        EditableModel stage = new("stage", "stage", [], [], [], [], JsonSerializer.SerializeToElement(new { }));
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => ModelMods.Preview(stage, new("battle/a0stg000.x", stage, true, [])));
        Assert.Contains("native stage geometry", error.Message);
        Assert.Throws<NotSupportedException>(() => ModelModInstaller.Plan([new("battle/a0stg000.x", stage, true, [])]));
    }
}
