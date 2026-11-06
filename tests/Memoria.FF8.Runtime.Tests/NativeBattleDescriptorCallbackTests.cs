using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDescriptorCallbackTests
{
    private static ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray();

    [Fact]
    public void EveryRetainedSignedWordCanReachAuthenticatedColdStorageWithoutBecomingAModelIndex()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        NativeBattleFileStaging memory = NativeBattleFileStaging.FromColdProcess();
        for (int index = short.MinValue; index <= short.MaxValue; index++)
        {
            Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, (short)index, memory.ReadByte));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void LiveModelOwnershipWinsOverColdStorageAndPreservesShortCircuitReads(short index)
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        NativeBattleGraphicsModel model = models[index] with { Flags0 = 2, Flags8 = 0, Kind5 = 1 };
        models = models.SetItem(index, model);
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, index, NativeBattleFileStaging.FromColdProcess().ReadByte));
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(models.SetItem(index, model with { Flags0 = 10, Flags8 = null }), index, _ => throw new NotSupportedException()));
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(models.SetItem(index, model with { Flags8 = 0x1A, Kind5 = null }), index, _ => throw new NotSupportedException()));
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(models.SetItem(index, model with { Flags8 = null }), index, _ => throw new NotSupportedException()));
    }

    [Theory]
    [InlineData(-32768)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(60)]
    [InlineData(32767)]
    public void ExternalRowsReadActualBytesAndResourceFlagsAcrossJsonRestore(short index)
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        uint address = checked((uint)(0x1D972C0 + 156 * index));
        uint resource = 0x1900000;
        NativeBattleFileStaging memory = NativeBattleFileStaging.FromColdProcess().Write(address, new byte[] { 2 }).Write(address + 5, new byte[] { 1 });
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, index, memory.ReadByte));
        memory = memory.Write(address + 5, new byte[] { 0 }).Write(address + 116, BitConverter.GetBytes(resource));
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, index, memory.ReadByte));
        memory = memory.Write(resource + 44, BitConverter.GetBytes((ushort)0x40));
        memory = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory)))!);
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, index, memory.ReadByte));
        memory = memory.Write(address + 8, BitConverter.GetBytes(0x1Au)).MarkUnknown(address + 116, 4);
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, index, memory.ReadByte));
        memory = memory.Write(address + 8, new byte[4]);
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(models, index, memory.ReadByte));
    }

    [Fact]
    public void ExternalUnknownBytesAreNotTreatedAsIdle()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(Models(), 60, NativeBattleFileStaging.Unknown().ReadByte));
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(Models(), 60, _ => throw new NotSupportedException()));
    }
}
