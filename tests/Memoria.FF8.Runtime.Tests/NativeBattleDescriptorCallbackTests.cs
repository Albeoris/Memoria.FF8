using System.Text.Json;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDescriptorCallbackTests
{
    private static readonly NativeBattleGraphicsState Empty = OriginalGraphics([]);

    [Fact]
    public void EveryRetainedSignedWordCanReachAuthenticatedColdStorageWithoutBecomingAModelIndex()
    {
        NativeBattleFileStaging memory = NativeBattleFileStaging.FromColdProcess();
        for (int index = short.MinValue; index <= short.MaxValue; index++)
        {
            Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, (short)index, memory.ReadByte));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void LiveModelOwnershipWinsOverColdStorageAndPreservesShortCircuitReads(short index)
    {
        BattleModelOwner owner = OriginalOwner(index);
        NativeBattleGraphicsModel model = new(2) { Flags8 = 0, Kind5 = 1 };
        NativeBattleGraphicsState graphics = OriginalGraphics([(index, owner, model)]);
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels, graphics.NativeBindings, index, NativeBattleFileStaging.FromColdProcess().ReadByte));
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels.Set(owner, model with { Flags0 = 10, Flags8 = null }), graphics.NativeBindings, index, _ => throw new NotSupportedException()));
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels.Set(owner, model with { Flags8 = 0x1A, Kind5 = null }), graphics.NativeBindings, index, _ => throw new NotSupportedException()));
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels.Set(owner, model with { Flags8 = null }), graphics.NativeBindings, index, _ => throw new NotSupportedException()));
    }

    [Theory]
    [InlineData(-32768)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(60)]
    [InlineData(32767)]
    public void ExternalRowsReadActualBytesAndResourceFlagsAcrossJsonRestore(short index)
    {
        uint address = checked((uint)(0x1D972C0 + 156 * index));
        uint resource = 0x1900000;
        NativeBattleFileStaging memory = NativeBattleFileStaging.FromColdProcess().Write(address, new byte[] { 2 }).Write(address + 5, new byte[] { 1 });
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, index, memory.ReadByte));
        memory = memory.Write(address + 5, new byte[] { 0 }).Write(address + 116, BitConverter.GetBytes(resource));
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, index, memory.ReadByte));
        memory = memory.Write(resource + 44, BitConverter.GetBytes((ushort)0x40));
        memory = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory)))!);
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, index, memory.ReadByte));
        memory = memory.Write(address + 8, BitConverter.GetBytes(0x1Au)).MarkUnknown(address + 116, 4);
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, index, memory.ReadByte));
        memory = memory.Write(address + 8, new byte[4]);
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, index, memory.ReadByte));
    }

    [Fact]
    public void ExpandedModelsDoNotTurnExternalNativeWordOperandsIntoActorAliases()
    {
        BattleModelOwner extra = new BattlePartyActorModel(new("unaliased"));
        BattleActorModels models = Empty.ActorModels.Add(extra, new(2) { Flags8 = 0, Kind5 = 1 });
        Assert.True(NativeBattleDescriptorCallbacks.IsBlocked502F30(models.Get(extra)));

        const short externalWord = 7;
        List<uint> reads = [];
        Assert.False(NativeBattleDescriptorCallbacks.IsBlocked502F30(models, Empty.NativeBindings, externalWord, address =>
        {
            reads.Add(address);
            return 0;
        }));
        Assert.Equal(0x1D972C0u + 156 * externalWord, Assert.Single(reads));
    }

    [Fact]
    public void ExternalUnknownBytesAreNotTreatedAsIdle()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, 60, NativeBattleFileStaging.Unknown().ReadByte));
        Assert.Throws<NotSupportedException>(() => NativeBattleDescriptorCallbacks.IsBlocked502F30(Empty.ActorModels, Empty.NativeBindings, 60, _ => throw new NotSupportedException()));
    }
}
