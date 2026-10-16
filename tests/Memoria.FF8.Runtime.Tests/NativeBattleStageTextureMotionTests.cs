using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageTextureMotionTests
{
    [Fact]
    public void WordRelativeSelectorAdvancesQuarterFrameCounterAndWrapsAcrossCountPlusOneFrames()
    {
        byte[] table = new byte[40];
        Word(2, 4);
        Word(8, 100);
        Word(10, 20);
        Word(12, 8);
        Word(14, 2);
        table[16] = 2;
        table[17] = 3;
        Word(18, 6);
        Word(24, 300);
        Word(26, 40);
        NativeBattleStageTextureStep first = NativeBattleStageTextureMotion.Advance50CB20(table.ToImmutableArray(), 1);
        Assert.Equal(new NativeBattleTextureCopy(300, 296, 8, 2, 100, 276), Assert.Single(first.Copies));
        Assert.Equal((ushort)9, BinaryPrimitives.ReadUInt16LittleEndian(first.Source.AsSpan().Slice(18)));
        ImmutableArray<byte> restored = JsonSerializer.Deserialize<ImmutableArray<byte>>(JsonSerializer.Serialize(first.Source));
        NativeBattleStageTextureStep sameFrame = NativeBattleStageTextureMotion.Advance50CB20(restored, 1);
        Assert.Empty(sameFrame.Copies);
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(sameFrame.Source.AsSpan().Slice(18)));
        byte[] wrap = sameFrame.Source.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(wrap.AsSpan(18), 23);
        Word(20, 400);
        Word(22, 50);
        table.AsSpan(20, 4).CopyTo(wrap.AsSpan(20));
        NativeBattleStageTextureStep wrapped = NativeBattleStageTextureMotion.Advance50CB20(wrap.ToImmutableArray(), 1);
        Assert.Equal(new NativeBattleTextureCopy(400, 306, 8, 2, 100, 276), Assert.Single(wrapped.Copies));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(wrapped.Source.AsSpan().Slice(18)));

        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(offset), value);
    }

    [Fact]
    public void ScrollQueuesBothOriginalForwardCopiesInOrderAndKnownNullSelectorDoesNothing()
    {
        byte[] table = new byte[20];
        Word(0, 2);
        Word(4, 64);
        Word(6, 100);
        Word(8, 16);
        Word(10, 8);
        table[13] = 2;
        NativeBattleStageTextureStep step = NativeBattleStageTextureMotion.Advance50CB20(table.ToImmutableArray(), 0);
        Assert.Equal(new NativeBattleTextureCopy[] { new(64, 356, 16, 8, 64, 354), new(64, 354, 16, 8, 64, 362) }, step.Copies.ToArray());
        Assert.Equal(table, step.Source.ToArray());
        Assert.Empty(NativeBattleStageTextureMotion.Advance50CB20(table.ToImmutableArray(), 1).Copies);
        Assert.Throws<NotSupportedException>(() => NativeBattleStageTextureMotion.Advance50CB20(null, 0));

        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(offset), value);
    }
}
