using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsModelTests
{
    [Fact]
    public void ClearingAndLoadingPreserveTheNativeFieldInitializationBoundary()
    {
        NativeBattleGraphicsModel[] bank = NativeBattleGraphicsModels.ClearBank();
        Assert.Equal(7, bank.Length);
        Assert.All(bank, model => Assert.Equal((ushort)0, model.Flags0));
        Assert.All(bank, model => Assert.Null(model.ResourceFlags44));
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel loaded = NativeBattleGraphicsModels.CompleteLoad(bank[3], 77, 0x80, ref random);
        Assert.Equal((ushort)3, loaded.Flags0);
        Assert.Equal((byte)8, loaded.Sequence);
        Assert.Equal((ushort)1, loaded.ResourceFlags44);
        Assert.Equal(0x80u, loaded.Flags8);
        Assert.Null(loaded.Phase6);
        Assert.Null(loaded.Alpha7);
        Assert.Equal((byte)0, loaded.AppearanceFrame129);
        Assert.NotEqual(0u, random.State);
        Assert.Equal(loaded, JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(loaded)));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void NativeRemovalReleasesTheAllocationBeforeGraphicalCleanupAndOnlyOnce(byte kind)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartRemoval(NativeBattleGraphicsModels.CompleteLoad(new(3, 0), 77, 0, ref random), kind);
        NativeBattleGraphicsModel paused = NativeBattleGraphicsModels.AdvanceRemoval(model, true, false, 0).Model;
        Assert.Equal((byte)0, paused.Phase6);
        for (int callback = 0; callback < 15; callback++)
        {
            NativeBattleGraphicsRemoval step = NativeBattleGraphicsModels.AdvanceRemoval(model, false, false, 0);
            Assert.False(step.ReleaseParticipant);
            model = step.Model;
        }
        Assert.Equal(kind, model.Kind5);
        Assert.Equal((byte)15, model.Phase6);
        model = NativeBattleGraphicsModels.AdvanceRemoval(model, false, false, 0).Model;
        Assert.Equal((byte)5, model.Kind5);
        NativeBattleGraphicsRemoval release = NativeBattleGraphicsModels.AdvanceRemoval(model, false, true, 1);
        Assert.True(release.ReleaseParticipant);
        Assert.NotEqual((ushort)0, release.Model.Flags0);
        NativeBattleGraphicsRemoval pending = NativeBattleGraphicsModels.AdvanceRemoval(release.Model, false, true, 0);
        Assert.False(pending.ReleaseParticipant);
        Assert.Equal((byte)5, pending.Model.Kind5);
        NativeBattleGraphicsRemoval complete = NativeBattleGraphicsModels.AdvanceRemoval(pending.Model, false, false, 0);
        Assert.False(complete.ReleaseParticipant);
        Assert.Equal((ushort)0, complete.Model.Flags0);
        Assert.Equal((byte)0, complete.Model.Kind5);
    }
}
