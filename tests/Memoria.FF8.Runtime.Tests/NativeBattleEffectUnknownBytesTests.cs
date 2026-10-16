using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectUnknownBytesTests
{
    [Fact]
    public void PointerByteReadsFailAndPartialWritesClearOnlyTheirCoveredUnknownSpans()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(100, [1, 2, 3, 4, 5, 6, 7, 8]).MarkUnknown(102, 3);
        Assert.Equal(new byte[] { 1, 2 }, arena.Read(100, 2));
        Assert.Throws<NotSupportedException>(() => arena.Read(100, 8));
        arena = arena.Write(103, [0x77]);
        Assert.Equal((byte)0x77, arena.Read(103, 1)[0]);
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(102, 1), new(104, 1) }, arena.UnknownRegions.ToArray());
        Assert.Throws<NotSupportedException>(() => arena.Read(102, 1));
        Assert.Throws<NotSupportedException>(() => arena.Read(104, 1));
        //An adjacent write merges physical storage spanning both unknown bytes; it must preserve their masks.
        arena = arena.Write(108, [9, 10]);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(arena))!;
        Assert.Equal(arena.UnknownRegions.ToArray(), restored.UnknownRegions.ToArray());
        Assert.Throws<NotSupportedException>(() => restored.Read(104, 1));
        restored = restored.Write(102, [0xA1, 0xA2, 0xA3]);
        Assert.Empty(restored.UnknownRegions);
        Assert.Equal(new byte[] { 1, 2, 0xA1, 0xA2, 0xA3, 6, 7, 8, 9, 10 }, restored.Read(100, 10));
    }

    [Fact]
    public void UnknownMasksMergeAndActual571870ClearEstablishesAllZeroAgain()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(10, 3).MarkUnknown(15, 2).MarkUnknown(13, 2);
        Assert.Equal(new NativeBattleEffectUnknownRegion(10, 7), Assert.Single(arena.UnknownRegions));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { Arena = arena };
        Assert.Equal(new byte[7], memory.BeginLoad(116).Arena!.Read(10, 7));
        Assert.Throws<InvalidDataException>(() => arena.MarkUnknown(NativeBattleEffectArena.Length - 1, 3));
    }
}
