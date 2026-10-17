using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelLoaderTests
{
    [Fact]
    public void Singleton_queue_keeps_selected_BYTEs_signed_IO_gate_and_resource_only_completion()
    {
        NativeBattleModelDatSource source = Source(0, false, 2);
        NativeBattleModelLoader queue = NativeBattleModelLoader.Queue507080(0, 1, 0x11223344, 7, 2);
        NativeBattleModelLoadStep request = queue.Poll507070(99, [source], NativeBattleFileStaging.Unknown());
        Assert.Equal(new NativeBattleModelFileRequest(310, "test.dat", 0x508470, source.Bytes.Length), request.FileRequest);
        Assert.Equal((byte)1, request.State.Task!.Phase);
        Assert.Equal(0x11223344u, request.State.Task.Argument);
        Assert.Equal(request.State, request.State.Poll507070(-1, [source], request.Staging).State);
        Assert.Equal(request.State, request.State.Poll507070(int.MinValue, [source], request.Staging).State);
        string json = JsonSerializer.Serialize(request.State);
        NativeBattleModelLoader restored = JsonSerializer.Deserialize<NativeBattleModelLoader>(json)!;
        NativeBattleFileStaging restoredStaging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(request.Staging))!;
        NativeBattleModelLoadStep texture = restored.Poll507070(source.Bytes.Length, [source], restoredStaging);
        Assert.NotNull(texture.TextureUpload);
        Assert.Null(texture.Bind);
        Assert.Equal(1, texture.Remaining);
        NativeBattleModelLoadStep complete = texture.State.Poll507070(source.Bytes.Length, [source], texture.Staging);
        Assert.Equal(0, complete.Remaining);
        Assert.Null(complete.State.Task);
        Assert.NotNull(complete.Bind);
        Assert.Equal((byte)1, complete.Bind.Task.PhysicalIndex);
        Assert.Null(complete.TextureUpload);
        Assert.Equal(0, complete.State.Poll507070(-1, [], complete.Staging).Remaining);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelLoader.Queue507080(0, 0, 0, 0, 9).Poll507070(0, [source], NativeBattleFileStaging.Unknown()));
    }

    [Theory]
    [InlineData(0, 0x5077B0u)]
    [InlineData(7, 0x5079B0u)]
    [InlineData(0x1000, 0x507BF0u)]
    [InlineData(0x1001, 0x507E20u)]
    [InlineData(0x1009, 0x507E20u)]
    public void Queue_routes_original_callbacks_without_initializing_the_actor(ushort request, uint callback)
    {
        NativeBattleModelLoader queue = NativeBattleModelLoader.Queue507080(request, 0, 0, 4, 5);
        Assert.Equal(callback, queue.Task!.Callback);
        Assert.Equal(request >= 0x1000 ? (byte)4 : (byte)5, queue.Task.Selector);
        Assert.Null(NativeBattleModelLoader.Queue507080(0x1007, 0, 0, 0, 0).Task);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelLoader.Queue507080(16, 0, 0, 0, 0));
    }

    [Fact]
    public void CPU_banks_preserve_unknown_tails_and_exact_full_bank_copy_and_JSON_aliases()
    {
        NativeBattleModelCpuBanks cpu = NativeBattleModelCpuBanks.Unknown();
        cpu = cpu with { Banks = cpu.Banks.SetItem(2, cpu.Banks[2].Write(0, new byte[] { 1, 2, 3, 4 })) };
        NativeBattleModelCpuBanks copiedUnknown = cpu.Copy64K50B2A0(2, 0);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, copiedUnknown.Banks[0].Read(0, 4));
        Assert.Throws<NotSupportedException>(() => copiedUnknown.Banks[0].Read(4, 1));
        Assert.Throws<NotSupportedException>(() => cpu.Banks[2].Read(2, 4));
        NativeBattleModelCpuBank bank = cpu.Banks[2].Write(2, new byte[] { 9, 8, 7 });
        Assert.Equal(new byte[] { 1, 2, 9, 8, 7 }, bank.Read(0, 5));
        byte[] complete = new byte[NativeBattleModelCpuBank.Length];
        complete[0] = 1;
        complete[^1] = 255;
        cpu = cpu with { Banks = cpu.Banks.SetItem(2, bank.Write(0, complete)) };
        cpu = cpu.Copy64K50B2A0(2, 0);
        NativeBattleModelCpuBanks restored = JsonSerializer.Deserialize<NativeBattleModelCpuBanks>(JsonSerializer.Serialize(cpu))!;
        Assert.Equal(complete, restored.Banks[0].Read(0, complete.Length));
    }

    [Fact]
    public void Body_then_shared_weapon_copy_preserves_body_alias_and_patches_the_existing_geometry_directory()
    {
        NativeBattleModelDatSource body = Source(1, false, 0);
        NativeBattleModelDatSource weapon = Source(1, true, 0);
        NativeBattleModelResourceBind bodyBind = Complete(1, body);
        ImmutableArray<NativeBattleModelCpuResourceRow> rows = Enumerable.Range(0, 11).Select(_ => NativeBattleModelCpuResourceRow.Unknown()).ToImmutableArray();
        NativeBattleModelResourceCompletion first = NativeBattleModelResourceCopies.Complete(bodyBind, NativeBattleModelCpuBanks.Unknown(), rows, 4, null);
        Assert.Equal(new NativeBattleModelCpuReference(0, 0), first.Rows[4].Require(16));
        Assert.Equal(new NativeBattleModelCpuReference(0, 16), first.Rows[4].Require(8));
        NativeBattleModelResourceBind linkedBind = Complete(0x1001, weapon);
        NativeBattleModelResourceCompletion second = NativeBattleModelResourceCopies.Complete(linkedBind, first.Cpu, first.Rows, 4, null);
        Assert.Equal(first.Rows[4].Require(16), second.Rows[4].Require(16));
        Assert.Equal(24, BinaryPrimitives.ReadInt32LittleEndian(second.Cpu.Banks[0].Read(8, 4)));
        Assert.Equal(new NativeBattleModelCpuReference(0, 20), second.Rows[4].Require(24));
        Assert.Null(second.ActiveLinkedRow);
        Assert.Null(second.AnimationCopy);
        Assert.Throws<NotSupportedException>(() => second.Cpu.Copy64K50B2A0(0, 2).Banks[2].Read(0, 65536));
    }

    private static NativeBattleModelResourceBind Complete(ushort request, NativeBattleModelDatSource source)
    {
        NativeBattleModelLoader state = NativeBattleModelLoader.Queue507080(request, 0, 0, 0, 0);
        NativeBattleModelLoadStep requested = state.Poll507070(-1, [source], NativeBattleFileStaging.Unknown());
        NativeBattleModelLoadStep prepared = requested.State.Poll507070(source.Bytes.Length, [source], requested.Staging);
        return prepared.State.Poll507070(source.Bytes.Length, [source], prepared.Staging).Bind!;
    }

    private static NativeBattleModelDatSource Source(byte model, bool linked, byte selector)
    {
        int[] offsets = linked ? [28, 32, 36, 40, 40] : [40, 40, 56, 56, 56, 56, 64, 68];
        byte[] bytes = new byte[linked ? 48 : 72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, offsets.Length);
        for (int section = 0; section < offsets.Length; section++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + section * 4), offsets[section]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + offsets.Length * 4), bytes.Length);
        int texture = offsets[linked ? 4 : 5];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(texture + 4), 8);
        return new(model, linked, selector, 310, "test.dat", bytes.ToImmutableArray());
    }
}
