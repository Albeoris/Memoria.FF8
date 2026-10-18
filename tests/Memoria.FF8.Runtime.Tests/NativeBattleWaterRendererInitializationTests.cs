using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterRendererInitializationTests
{
    [Fact]
    public void OS_zero_and_actual_renderer_initialization_are_distinct_and_do_not_reconstruct_imported_controls()
    {
        NativeBattleScriptedGraphicsMemory cold = NativeBattleScriptedGraphicsMemory.FromColdProcess();
        Assert.Equal((short)0, cold.Zsf3);
        Assert.Equal((short)0, cold.Zsf4);
        NativeBattleScriptedGraphicsMemory initialized = (cold with { Dqa = -7, Dqb = 900 }).InitializeGeom56BAA0();
        Assert.Equal((short)341, initialized.Zsf3);
        Assert.Equal((short)256, initialized.Zsf4);
        Assert.Equal((short)0, initialized.Dqa);
        Assert.Equal(0, initialized.Dqb);
        Assert.Same(cold.Depths, initialized.Depths);
        string oldJson = JsonSerializer.Serialize(cold with { Zsf3 = null, Zsf4 = null });
        Assert.Null(JsonSerializer.Deserialize<NativeBattleScriptedGraphicsMemory>(oldJson)!.Zsf3);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleScriptedGraphicsMemory>(oldJson)!.Zsf4);
        Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleScriptedGraphicsMemory>(JsonSerializer.Serialize(initialized))));
    }
}
