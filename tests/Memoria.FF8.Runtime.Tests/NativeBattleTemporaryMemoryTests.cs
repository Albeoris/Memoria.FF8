using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTemporaryMemoryTests
{
    [Fact]
    public void RoundedAllocationsReleaseTheirCursorAndRetainActualBytes()
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleTemporaryAllocation first = NativeBattleTemporaryMemory.Allocate5082B0(staging, 3);
        Assert.Equal(NativeBattleTemporaryMemory.Address, first.Address);
        staging = first.Staging.Write(first.Address, new byte[] { 1, 2, 3 });
        NativeBattleTemporaryAllocation second = NativeBattleTemporaryMemory.Allocate5082B0(staging, 400);
        Assert.Equal(first.Address + 4, second.Address);
        staging = NativeBattleTemporaryMemory.Release5082D0(second.Staging, 400);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 3);
        Assert.Equal(first.Address, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new byte[] { 1, 2, 3 }, staging.Read(first.Address, 3));
        Assert.Throws<NotSupportedException>(() => staging.Read(first.Address + 3, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleTemporaryMemory.Release5082D0(staging, 4));
    }

    [Fact]
    public void SlicesPreserveUnknownPaddingAndIndependentRestore()
    {
        uint address = NativeBattleTemporaryMemory.Address + 64;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[] { 1, 2, 3, 4, 5, 6 }).Write(address + 8, new byte[] { 9, 10 });
        NativeBattleEffectArena view = NativeBattleTemporaryMemory.ReadView(staging, address, 10);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, view.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => view.Read(6, 2));
        view = view.Write(6, new byte[] { 7, 8 }).MarkUnknown(1, 2);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging))!;
        NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(restored, address, view, 10);
        Assert.Equal(new byte[] { 4, 5, 6, 7, 8, 9, 10 }, published.Read(address + 3, 7));
        Assert.Throws<NotSupportedException>(() => published.Read(address + 1, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(published), NativeBattleTestSnapshots.Json(NativeBattleTemporaryMemory.PublishView(staging, address, view, 10)));
    }
}
