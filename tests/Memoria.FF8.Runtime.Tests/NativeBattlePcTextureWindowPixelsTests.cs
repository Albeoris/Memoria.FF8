using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcTextureWindowPixelsTests
{
    [Fact]
    public void SameImmutableOwnerAndEqualRegionReturnTheSameCapturedPixelObject()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner);
        Assert.Same(first, NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner));
        Assert.NotSame(first, NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 4, 2), owner));
        Assert.NotSame(first, NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner with { }));
        Assert.Equal((ushort)0x1234, first.ReadWord(0, 0));
        NativeBattlePcTextureWindowPixels[] concurrent = new NativeBattlePcTextureWindowPixels[16];
        Parallel.For(0, concurrent.Length, index => concurrent[index] = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 2), owner));
        Assert.All(concurrent, capture => Assert.Same(first, capture));
    }

    [Fact]
    public void ChangedOwnerProducesNewWordsWhilePriorCaptureAndRestoredWordsRemainUnchanged()
    {
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x34, 0x12]);
        NativeBattlePcTextureWindowPixels original = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), before);
        NativeBattleEffectTextureState after = before.Upload45BD30(0, 0, 1, 1, [0xCD, 0xAB]);
        NativeBattlePcTextureWindowPixels changed = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), after);
        Assert.NotSame(original, changed);
        Assert.Equal((ushort)0x1234, original.ReadWord(0, 0));
        Assert.Equal((ushort)0xABCD, changed.ReadWord(0, 0));
        Assert.Same(original, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), before));
        Assert.Same(changed, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), after));
        string json = JsonSerializer.Serialize(original);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattlePcTextureWindowPixels>(json)));
        NativeBattleEffectTextureState restoredOwner = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(after))!;
        NativeBattlePcTextureWindowPixels restoredCapture = NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), restoredOwner);
        Assert.NotSame(changed, restoredCapture);
        Assert.Equal(changed.Data.ToArray(), restoredCapture.Data.ToArray());
        Assert.Same(restoredCapture, NativeBattlePcTextureWindowPixels.Capture(64, new(0, 0, 8, 2), restoredOwner));
    }
}
