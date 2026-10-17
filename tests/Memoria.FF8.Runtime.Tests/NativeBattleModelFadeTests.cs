using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelFadeTests
{
    private static readonly NativeBattleSummonColors Colors = new(0x00464B6E, 0x32808080, 1f / 4096, 4503601774854144d);

    [Fact]
    public void LinearFadePreservesAlphaAndRequiresItsActualSourceColor()
    {
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartService(new(0, 3) { Flags2 = 0, Flags8 = 0, Alpha7 = 17 }, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelFade.Advance50C560(model, false, Colors));
        NativeBattleGraphicsModel first = NativeBattleModelFade.Advance50C560(model, true, Colors with { LinearFadeSourceB8BA94 = 0x123456 });
        Assert.Equal(0x02123456u, first.Color40);
        Assert.Equal((byte)17, first.Alpha7);
        Assert.Equal((byte)0, first.Phase6);
        NativeBattleGraphicsModel complete = NativeBattleModelFade.Advance50C560(first with { Phase6 = 15 }, false, Colors);
        Assert.Equal((byte)0, complete.Kind5);
        Assert.Equal((byte)16, complete.Phase6);
        Assert.Equal(0u, complete.Color40);
    }

    [Fact]
    public void FlashConsumesRandomOnlyOnZeroPhaseAndRunsThroughGraphicsPause()
    {
        int draws = 0;
        NativeBattleGraphicsModel model = new(0, 3) { Kind5 = 8, Phase6 = 0, Color40 = 0 };
        model = NativeBattleModelFade.AdvanceFlash50C670(model, Colors, () =>
        {
            draws++;
            return 29;
        });
        Assert.Equal((byte)15, model.Phase6);
        Assert.Equal(0x007F7F7Fu, model.Color40);
        model = NativeBattleModelFade.AdvanceFlash50C670(model with { Phase6 = 1 }, Colors, () => throw new InvalidOperationException());
        Assert.Equal((byte)0, model.Phase6);
        Assert.Equal(0x00FFFFFFu, model.Color40);
        Assert.Equal(1, draws);
    }

    [Fact]
    public void StatusRedirectsAndKindThirteenCompletionWaitForNextCallbackBeforeCleanup()
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags2 = 0x10, Flags8 = 0x100000, Color40 = 0x123456 };
        Assert.Equal((byte)10, NativeBattleGraphicsModels.StartService(model, 1).Kind5);
        NativeBattleGraphicsModel disappearing = NativeBattleGraphicsModels.StartService(model, 6);
        Assert.Equal((byte)13, disappearing.Kind5);
        NativeBattleGraphicsModel complete = NativeBattleSummonModels.Advance(disappearing with { Phase6 = 15 }, false, Colors);
        Assert.Equal((byte)5, complete.Kind5);
        Assert.Equal((byte)0, complete.Phase6);
        Assert.NotEqual((ushort)0, complete.Flags0);
        Assert.Equal((byte)0, NativeBattleGraphicsModels.StartService(model, 0).Kind5);
    }

    [Fact]
    public void KindThreeCompletesItsOwnColorAndAlphaServiceWithoutReleasingTheModel()
    {
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartService(new(0, 3) { Flags2 = 0, Flags8 = 0, Kind5 = 0, Sequence = 14 }, 3);
        NativeBattleGraphicsModel paused = NativeBattleModelFade.Advance50C410(model, true, Colors);
        Assert.Equal((byte)0, paused.Phase6);
        Assert.Equal(0x02464B6Eu, paused.Color40);
        Assert.Equal(0x32808080u, paused.Color44);
        Assert.Equal((byte)255, paused.Alpha7);
        for (int callback = 0; callback < 15; callback++)
        {
            model = NativeBattleModelFade.Advance50C410(model, false, Colors);
            Assert.Equal((byte)3, model.Kind5);
            Assert.Equal((ushort)3, model.Flags0);
        }
        Assert.Equal((byte)15, model.Phase6);
        Assert.Equal((byte)17, model.Alpha7);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
        NativeBattleGraphicsModel complete = NativeBattleModelFade.Advance50C410(restored, true, Colors);
        Assert.Equal((byte)0, complete.Kind5);
        Assert.Equal((byte)15, complete.Phase6);
        Assert.Equal((ushort)7, complete.Flags0);
        Assert.Equal((ushort)0x80, complete.Flags2);
        Assert.Equal(0u, complete.Color40);
        Assert.Equal(model.Color44, complete.Color44);
        Assert.Equal(model.Alpha7, complete.Alpha7);
        Assert.Equal((byte)14, complete.Sequence);
        Assert.False(NativeBattleGraphicsModels.AdvanceRemoval(complete, false, false, 0).ReleaseParticipant);
    }

    [Fact]
    public void OriginalStatusBitRedirectsKindThreeToItsSeparateKindElevenService()
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags2 = 0, Flags8 = 0x100000, Kind5 = 0 };
        Assert.Equal((byte)11, NativeBattleGraphicsModels.StartService(model, 3).Kind5);
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsModels.StartService(model with { Flags8 = null }, 3));
        Assert.Equal((byte)16, NativeBattleModelFade.Advance50C410(model with { Kind5 = 3, Phase6 = 15 }, false, Colors).Phase6);
    }
}
