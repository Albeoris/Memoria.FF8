using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeCarWorldRuntimeStateTests
{
    [Theory]
    [InlineData(32, 74)]
    [InlineData(33, 73)]
    [InlineData(34, 81)]
    [InlineData(35, 82)]
    [InlineData(36, 83)]
    [InlineData(37, 84)]
    [InlineData(38, 85)]
    [InlineData(39, 86)]
    [InlineData(40, 87)]
    public void OrdinaryCarRetainsItsShapeAllocationAndReturnAuthority(byte entity, byte shape)
    {
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0));
        NativeTrainWorldAllocation? allocation = entity == 33 ? null : Allocation();
        NativeWorldFootCameraState? camera = entity == 33 ? null : new(0, 0, 0, 0, 0, 0, 0, -256, 0, -1024);
        NativeCarWorldRuntimeState runtime = NativeCarWorldRuntimeState.Create(NativeSavedWorldState.CreateNewGame(), cache, NativeWorldProcess.FromColdProcess(), entity, allocation, camera);
        Assert.True(NativeCarWorldRuntimeState.IsSupportedEntity(entity));
        Assert.Equal(shape, runtime.ShapeId);
        Assert.Equal(entity, NativeTrainWorldObject.ClassifyShape(shape));
        NativeCarWorldRuntimeState restored = JsonSerializer.Deserialize<NativeCarWorldRuntimeState>(JsonSerializer.Serialize(runtime))!;
        restored.Validate();
        Assert.Equal(entity, restored.EntityType);
        Assert.Equal(shape, restored.ShapeId);
        Assert.Throws<InvalidDataException>(() => (restored with { ShapeId = 0 }).Validate());
        if (entity != 33)
        {
            Assert.Throws<InvalidDataException>(() => (restored with { Allocation = null }).Validate());
            Assert.Throws<InvalidDataException>(() => (restored with { ProjectionCamera = null }).Validate());
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => (restored with { Allocation = Allocation() }).Validate());
        }
        WorldState world = new() { Frame = 12, NativeCarRuntime = restored };
        NativeSavedWorldCarReturn receipt = NativeSavedWorldCarReturn.Create(world, 1, 2, 0);
        receipt.Validate();
        Assert.Equal(entity, receipt.EntityType105);
        NativeWorldConfiguration configuration = NativeWorldConfiguration.FromWorldReturn(world, 1);
        configuration.Validate();
        Assert.Equal(entity, configuration.EntityByte);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(41)]
    [InlineData(132)]
    public void EntityWithoutOrdinaryCarShapeCannotBeAdmitted(byte entity)
    {
        Assert.False(NativeCarWorldRuntimeState.IsSupportedEntity(entity));
        Assert.Throws<NotSupportedException>(() => NativeCarWorldRuntimeState.ShapeForEntity(entity));
    }

    private static NativeTrainWorldAllocation Allocation()
    {
        string hash = new('0', 64);
        NativeTrainWorldObject foot = new(0, 0, new(0, 0, 0, 0), 0, ImmutableArray.Create(new byte[16]));
        return new(1, NativeTrainWorldAllocation.AllocationRule, hash, hash, hash, 0, 0, 1, 0, 0, [0xFF01, 0, 0xFF02, 0, 0xFF03, 1, 0xFF04, 0, 0xFF13, 0, 0xFF05, 0], [foot], 0);
    }
}
