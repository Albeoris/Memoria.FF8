using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcTextureWindowPixelsTests
{
    [Fact]
    public void Direct_page_reads_continue_linearly_into_the_retained_static_tail()
    {
        NativeBattleEffectTextureState owner = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 511, 1, 1, [0xCD, 0xAB]).Upload45BD30(960, 511, 1, 1, [0x34, 0x12]);
        owner = owner with { VramReadTail = owner.VramReadTail!.Value.SetItem(0, 0x78).SetItem(1, 0x56) };
        NativeBattlePcTextureWindowPixels captured = NativeBattlePcTextureWindowPixels.Capture(95, new(0, 0, 256, 256), owner);
        Assert.Equal((ushort)0xABCD, captured.ReadWord(1024, 510));
        Assert.Equal((ushort)0x1234, captured.ReadWord(960, 511));
        Assert.Equal((ushort)0x5678, captured.ReadWord(1024, 511));
        Assert.Equal((ushort)0, captured.ReadWord(1215, 511));
        captured.ValidateMapping(95, new(0, 0, 256, 256));
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(owner))!;
        Assert.Equal(captured.Data.ToArray(), NativeBattlePcTextureWindowPixels.Capture(95, new(0, 0, 256, 256), restored).Data.ToArray());
        Assert.Equal(owner.VramReadTail, owner.Initialize500400().VramReadTail);
    }

    [Fact]
    public void Missing_warm_tail_is_required_only_when_the_actual_read_reaches_it()
    {
        NativeBattleEffectTextureState unknown = NativeBattleEffectTextureState.FromColdProcess() with { VramReadTail = null };
        NativeBattlePcTextureWindowPixels.Capture(31, new(0, 0, 256, 256), unknown);
        Assert.Throws<NotSupportedException>(() => NativeBattlePcTextureWindowPixels.Capture(95, new(0, 0, 256, 256), unknown));
        Assert.All(NativeBattleEffectTextureState.FromColdProcess().VramReadTail!.Value, value => Assert.Equal((byte)0, value));
    }

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
