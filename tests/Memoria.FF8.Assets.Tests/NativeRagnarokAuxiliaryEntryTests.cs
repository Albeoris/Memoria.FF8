using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRagnarokAuxiliaryEntryTests
{
    [Fact]
    public void OriginalRowFourConsumesOnlyItsSelectorBeforeControlledShipPublicationAndRestoresExactly()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        using GameAssets game = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        WorldTransition packet = new(32, 50, -1);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 3500);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { GardenPose3 = new(1024, -2048, -128, 128) };
        NativeEventMemory memory = NativeEventMemory.Empty.Write(269, 1, NativeScalarValue.Exact(4)).Write(271, 1, NativeScalarValue.Exact(91));
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokAllocation(packet, entry, memory, saved, game.ReadNewGameCharacterVitals());
        NativeWorldAuxiliaryRelocationReceipt receipt = Assert.IsType<NativeWorldAuxiliaryRelocationReceipt>(allocation.AuxiliaryEntry);
        Assert.Equal("EF7E3A3CAE12D63A0A95714FC033DA12D1F7055189BCE48234C3EE89C6E660A8", receipt.ResourceHash);
        Assert.Equal(new NativeWorldAuxiliaryRelocation(0, 4, 50, new(41873, -756, -48239), 1), Assert.Single(receipt.Relocations));
        Assert.All(allocation.Objects.Where(item => item.ShapeId is 0 or 1), item => Assert.Equal(new NativeWorldPathPosition(allocation.LoadPose.X, allocation.LoadPose.Height, -allocation.LoadPose.RawZ), item.Position));
        Assert.NotEqual(receipt.Relocations[0].Position, allocation.Objects[allocation.RagnarokIndex].Position);
        NativeEventMemory consumed = receipt.ApplyTo(memory);
        Assert.Equal(4, memory.Read(269, 1).RequireExact());
        Assert.Equal(0, consumed.Read(269, 1).RequireExact());
        Assert.Equal(91, consumed.Read(271, 1).RequireExact());
        NativeRagnarokWorldAllocation restored = assets.ReadRagnarokAllocation(packet, entry, consumed, saved, game.ReadNewGameCharacterVitals(), retainedAuxiliary: receipt);
        Assert.Equal(JsonSerializer.Serialize(allocation), JsonSerializer.Serialize(restored));
        NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, 24, 112);
        NativeRagnarokWorldRuntimeState cold = JsonSerializer.Deserialize<NativeRagnarokWorldRuntimeState>(JsonSerializer.Serialize(runtime))!;
        cold.Validate();
        Assert.Equal(NativeRagnarokMotor.Step(runtime.Motor, 0, 0, 0), NativeRagnarokMotor.Step(cold.Motor, 0, 0, 0));
        Assert.Throws<InvalidDataException>(() => assets.ReadRagnarokAllocation(packet, entry, memory, saved, game.ReadNewGameCharacterVitals(), retainedAuxiliary: receipt));
        Assert.Throws<InvalidDataException>(() => assets.ValidateWorldAuxiliaryRelocations(receipt with { ResourceHash = new string('A', 64) }));
    }

    [Fact]
    public void OrderedGardenAndShipRowsRetainIndependentTargetsAndTheOriginalSignedYaw()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        using GameAssets game = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 3500);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { GardenPose3 = new(1024, -2048, -128, 128) };
        NativeEventMemory memory = NativeEventMemory.Empty.Write(269, 2, NativeScalarValue.Exact(0x0401));
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokAllocation(new(32, 50, -1), entry, memory, saved, game.ReadNewGameCharacterVitals());
        Assert.Equal(new[] { 0, 1 }, allocation.AuxiliaryEntry!.Relocations.Select(row => row.Channel));
        Assert.All(allocation.Objects.Where(item => item.ShapeId is 64 or 65), item =>
        {
            Assert.Equal(new NativeWorldPathPosition(15769, -659, 29052), item.Position);
            Assert.Equal((short)9, item.Yaw);
        });
        Assert.Equal((byte)2, allocation.Objects.Single(item => item.ShapeId == 65).Flags);
        Assert.Equal(0, allocation.AuxiliaryEntry.ApplyTo(memory).Read(269, 2).RequireExact());
        Assert.Throws<NotSupportedException>(() => allocation.AuxiliaryEntry.RequireGardenPublication(allocation.Packet, allocation.LoadPose));
    }
}
