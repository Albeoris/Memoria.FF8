using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleEffect65LowerFixture
{
    internal static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    internal static NativeBattleEffect65Frame Frame(int z = 0)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear(),
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(z).ReadMatrix(), true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAB000000 },
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096, Zsf4 = 4096 }
        };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[400]);
        return new(memory, new(65, 64, new(65, Entry, Initializer), null, null) { Pools = [] }, new(123), NativeBattleEffect65State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress)) { Staging = staging };
    }
    internal static NativeBattleEffect65Inputs Inputs(List<NativeBattleEffect65Operation>? operations = null, Func<NativeBattleEffect65Frame, NativeBattleEffect65Operation, NativeBattleEffect65Frame>? execute = null, int viewZ = 2000) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => Identity(viewZ), _ => new(null, 100, 160, 108), _ => 0, null!, null!, null!, null!, null!, null!, null!, (frame, operation) =>
    {
        operations?.Add(operation);
        return execute is null ? frame : execute(frame, operation);
    });
    internal static NativeBattleEffect65Resources Resources()
    {
        byte[] trig = new byte[16384];
        for (int index = 0; index < trig.Length; index += 4)
        {
            BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(index), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(index + 2), 4096);
        }
        return new(new("texture", []), new("vibration", [], []), new(0, SpriteAddress, 0, 0, []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(TrigonometryAddress, trig.ToImmutableArray()).Add(DistanceAddress, Enumerable.Repeat((byte)1, 16384).ToImmutableArray()).Add(0x1342D2C, ImmutableArray.Create((byte)0)), ImmutableDictionary<short, NativeBattleEffectFileAsset>.Empty);
    }
    internal static NativeBattleEffect65Frame Write(NativeBattleEffect65Frame frame, uint address, NativeBattleEffectArena bytes, int length) => NativeBattleEffect65Controller.WriteBytes(frame, address, bytes, length);
    internal static NativeBattleEffectArena Read(NativeBattleEffect65Frame frame, uint address, int length) => NativeBattleEffect65Controller.ReadBytes(frame, address, length);
    internal static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}

public sealed class NativeBattleEffect65LowerTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(513)]
    public void Staged_reads_keep_live_transaction_payload_unknownness_and_detachment_without_pointer_metadata(int length)
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        uint address = NativeBattleTemporaryMemory.Address + 64;
        byte[] bytes = Enumerable.Range(0, length).Select(index => unchecked((byte)(index + 1))).ToArray();
        NativeBattleFileStaging source = frame.Staging!.Write(address, bytes);
        using NativeBattleFileStagingTransaction transaction = new(source);
        transaction.View.WriteByte(address, 9).MarkUnknown(address + 2, 1).WriteResourcePointer(address + 4, null);
        var expected = transaction.View.ReadSnapshot(address, length);
        frame = frame with { Staging = transaction.View };

        NativeBattleEffectArena result = NativeBattleEffect65Controller.ReadBytes(frame, address, length);
        transaction.View.Write(address, new byte[length]);

        Assert.Equal(expected.Data, result.ReadSnapshot(0, length).Data);
        Assert.Equal(expected.Known, result.ReadSnapshot(0, length).Known);
        Assert.False(result.TryGetResourcePointer(4, out _));
        Assert.Throws<NotSupportedException>(() => result.Read(2, 1));
        Assert.Equal(new byte[4], result.Read(length, 4));
        Assert.Equal(NativeBattleEffectArena.Length, result.LogicalLength);
        Assert.Equal((byte)1, source.ReadByte(address));
    }

    [Fact]
    public void Staged_read_failure_keeps_original_range_source_order_and_retained_owners_unchanged()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        NativeBattleFileStagingTransaction transaction = new(frame.Staging!);
        NativeBattleFileStaging disposed = transaction.View;
        transaction.Dispose();
        NativeBattleEffect65Frame invalid = frame with { Staging = disposed };
        uint address = NativeBattleTemporaryMemory.Address + 64;

        Assert.Equal("length", Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleEffect65Controller.ReadBytes(invalid, uint.MaxValue, 2)).ParamName);
        Assert.Throws<ObjectDisposedException>(() => NativeBattleEffect65Controller.ReadBytes(invalid, address, 8));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect65Controller.ReadBytes(frame with { Staging = null }, address, 8));
        Assert.Equal(new byte[8], frame.Staging!.Read(address, 8));
        Assert.Equal(new byte[8], frame.Memory.Arena!.Read(0, 8));
        Assert.False(frame.Memory.Arena.Working.HasCheckpoint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Copy_pointer_check_keeps_explicit_null_numeric_symbolic_and_contained_span_metadata(bool frozen)
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().WriteResourcePointer(0, new(null, 0x11223344)).WriteResourcePointer(4, null).WriteResourcePointer(8, new(null, 0x12345678)).WriteResourcePointer(12, new(null, null) { CpuRow = 2 });
        if (frozen)
        {
            source = NativeBattleStorageSnapshots.Capture(source);
        }
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear();

        NativeBattleEffect65Controller.CopyBytes(source, 4, destination, 16, 12);

        Assert.True(destination.TryGetResourcePointer(16, out NativeBattleResourcePointer? explicitNull));
        Assert.Null(explicitNull);
        Assert.Equal(source.ReadResourcePointer(8), destination.ReadResourcePointer(20));
        Assert.Equal(source.ReadResourcePointer(12), destination.ReadResourcePointer(24));
        Assert.False(destination.TryGetResourcePointer(12, out _));
        Assert.Equal(source.ReadSnapshot(4, 12).Data, destination.ReadSnapshot(16, 12).Data);
        Assert.Equal(source.ReadSnapshot(4, 12).Known, destination.ReadSnapshot(16, 12).Known);
    }

    [Fact]
    public void Copy_pointer_check_observes_pointer_metadata_created_only_in_the_active_overlay()
    {
        NativeBattleEffectArena owner = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena window = NativeBattleEffectArena.BorrowWorking(owner.Working, 32, 16);
        using NativeBattleEffectArenaTransaction transaction = new(window);
        window.WriteResourcePointer(4, null).WriteResourcePointer(8, new(null, 0x12345678));
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear();

        NativeBattleEffect65Controller.CopyBytes(window, 4, destination, 0, 8);

        Assert.Equal(0, owner.Working.ResourcePointerCount);
        Assert.True(destination.TryGetResourcePointer(0, out NativeBattleResourcePointer? explicitNull));
        Assert.Null(explicitNull);
        Assert.Equal(0x12345678u, destination.ReadResourcePointer(4)!.StagingAddress);
    }

    [Fact]
    public void Empty_pointer_copy_avoids_an_iterator_per_steady_state_copy()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]);
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 32; index++)
        {
            NativeBattleEffect65Controller.CopyBytes(source, 0, destination, 0, 4);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            NativeBattleEffect65Controller.CopyBytes(source, 0, destination, 0, 4);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 1024, $"Empty-pointer copies allocated {allocated} bytes across 64 calls.");
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, destination.Read(0, 4));
    }

    [Fact]
    public void Fresh_read_copy_covers_mixed_regions_and_keeps_outside_span_known_zero_and_detached()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        uint address = 0x20DFAB8u + NativeBattleEffectArena.Length - 4u;
        frame.Memory.Arena!.Write(NativeBattleEffectArena.Length - 4, [1, 2, 3, 4]).MarkUnknown(NativeBattleEffectArena.Length - 3, 1);
        frame.Staging!.Write(address + 4, [5, 6, 7, 8]).MarkUnknown(address + 6, 1);

        NativeBattleEffectArena view = NativeBattleEffect65Controller.ReadBytes(frame, address, 8);
        NativeBattleEffect65Controller.WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[8]), 8);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, view.ReadSnapshot(0, 8).Data);
        Assert.Equal(new[] { true, false, true, true, true, true, false, true }, view.ReadSnapshot(0, 8).Known);
        Assert.Equal(NativeBattleEffectArena.Length, view.LogicalLength);
        Assert.Equal(new byte[8], view.Read(8, 8));
        Assert.Equal(new byte[4], view.Read(NativeBattleEffectArena.Length - 4, 4));
        Assert.Equal(new byte[8], NativeBattleEffect65Controller.ReadBytes(frame, address, 8).Read(0, 8));
    }

    [Fact]
    public void Common_and_inactive_root_stores_keep_canonical_frame_and_memory_identity()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]).MarkUnknown(1, 1);

        NativeBattleEffect65Frame common = NativeBattleEffect65Controller.WriteBytes(frame, 0x20DFAB8 + 64, bytes, 4);
        NativeBattleEffect65Frame root = NativeBattleEffect65Controller.WriteBytes(common, RootBank + 4, bytes, 4);

        Assert.Same(frame, common);
        Assert.Same(frame, root);
        Assert.Same(frame.Memory, root.Memory);
        Assert.Same(frame.State, root.State);
        Assert.Equal(bytes.ReadSnapshot(0, 4).Data, root.Memory.Arena!.ReadSnapshot(64, 4).Data);
        Assert.Equal(bytes.ReadSnapshot(0, 4).Known, root.Memory.Arena.ReadSnapshot(64, 4).Known);
        Assert.Equal(bytes.ReadSnapshot(0, 4).Data, root.State.RootBankStorage!.ReadSnapshot(4, 4).Data);
        Assert.Equal(bytes.ReadSnapshot(0, 4).Known, root.State.RootBankStorage.ReadSnapshot(4, 4).Known);
    }

    [Fact]
    public void Common_store_identity_elision_preserves_operation_rollback_and_resource_pointer_publication()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        arena.AdmitRetained(new());
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().WriteResourcePointer(0, new(null, 0x12345678));
        using (NativeBattleTaskOperation operation = new([]))
        {
            Assert.Same(frame, NativeBattleEffect65Controller.WriteBytes(frame, 0x20DFAB8 + 64, source, 4));
            Assert.Equal(0x12345678u, arena.ReadUInt32(64));
            Assert.True(arena.TryGetResourcePointer(64, out NativeBattleResourcePointer? pointer));
            Assert.Equal(source.ReadResourcePointer(0), pointer);
        }

        Assert.Equal(0u, arena.ReadUInt32(64));
        Assert.False(arena.TryGetResourcePointer(64, out _));
        Assert.False(arena.Working.HasCheckpoint);
    }

    [Fact]
    public void Camera_interpolation_publishes_saturated_IR_and_final_GTE_state()
    {
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        NativeBattleEffect65Frame result = NativeBattleEffect65Camera.Interpolate6FC250(frame, new(100, -100, 50), new(300, 100, -50), new(300, 100, -50), new(100, -100, 50), -10, 10, 1, 3, NativeBattleEffect65LowerFixture.Inputs(operations));
        Assert.Equal((short)0, Assert.IsType<NativeBattleEffect65CameraRoll>(operations[0]).Roll);
        NativeBattleEffect65CameraPoseXYZ pose = Assert.IsType<NativeBattleEffect65CameraPoseXYZ>(operations[1]);
        Assert.Equal(new NativeBattleCameraPoint(200, 0, 0), pose.Target);
        Assert.Equal(new NativeBattleCameraPoint(200, 0, 0), pose.Eye);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 200, 0, 0 }, result.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(frame.Random, result.Random);
    }
}
