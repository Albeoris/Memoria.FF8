using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect140LowerFixture;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140MeshTests
{
    [Fact]
    public void Mirrored_quads_keep_nonsequential_E2_links_full_SZ_and_unknown_caller_matrix()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleEffect140MirroredStep result = NativeBattleEffect140Meshes.DrawLoaded(Frame().Memory, mesh, new(null, 100, 160, 108), Context(mesh), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(152, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 52, 0, 64, 128, 76, 140 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt first = result.Batch.Attempts[1];
        NativeBattleGfGeometryAttempt mirrored = result.Batch.Attempts[4];
        Assert.Equal(13, first.Primitive!.Words.Length);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, first.Primitive.VertexPayload4);
        Assert.NotEqual(first.Primitive.Words[2], mirrored.Primitive!.Words[2]);
        Assert.Equal(0x3E808080u, first.Primitive.Words[1]);
        Assert.DoesNotContain(true, result.Context152.ReadSnapshot(0x78, 32).Known);
        Assert.Equal((byte)130, result.Context152.Read(0x6C, 1)[0]);
        Assert.Equal(0x1234u, U(result.Context152, 0x6C) >> 16);
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, U(result.Context152, 0x30));
    }

    [Fact]
    public void Zero_area_is_accepted_and_uses_no_NCLIP_command()
    {
        NativeBattleStaticMeshResource mesh = Mesh(degenerate: true);
        NativeBattleEffect140Frame frame = Frame();
        NativeBattleEffect140MirroredStep result = NativeBattleEffect140Meshes.DrawLoaded(frame.Memory, mesh, new(null, 100, 160, 108), Context(mesh), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(152, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
    }

    [Fact]
    public void Near_rejection_preserves_partial_UV_stores_depth_and_serialized_next_state()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleEffect140MirroredStep result = NativeBattleEffect140Meshes.DrawLoaded(Frame(1).Memory, mesh, new(null, 100, 160, 108), Context(mesh), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        });
        Assert.Equal(0xDEADBEEFu, U(result.Context152, 0x30));
        NativeBattleEffect140MirroredStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect140MirroredStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect140Meshes.DrawLoaded(result.Memory, mesh, new(null, 100, 160, 108), result.Context152, NativeBattleGteColorInterpolation.CodeConstants45F270))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect140Meshes.DrawLoaded(restored.Memory, mesh, new(null, 100, 160, 108), restored.Context152, NativeBattleGteColorInterpolation.CodeConstants45F270))));
    }

    [Fact]
    public void Low_IR_bytes_supply_the_faded_packet_color_independently_of_FIFO()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleEffect140MirroredStep result = NativeBattleEffect140Meshes.DrawLoaded(Frame().Memory, mesh, new(null, 100, 160, 108), Context(mesh, -4094), NativeBattleGteColorInterpolation.CodeConstants45F270);
        uint color = result.Batch.Attempts[1].Primitive!.Words[1];
        Assert.Equal(0x3Eu, color >> 24);
        Assert.Equal(0x00010101u, color & 0xFFFFFF);
    }

    [Fact]
    public void Initializer_writes_the_ramp_and_retains_nearest_ties_and_previous_selection_for_unconnected_rows()
    {
        NativeBattleStaticMeshResource mesh = Mesh(89);
        byte[] bytes = mesh.Bytes.ToArray();
        int face = 8 + 89 * 8 + 32;
        ushort[] indices = [87, 2, 4, 6];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), indices[corner]);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + 87 * 8), 1002);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + 4 * 8), 1002);
        mesh = mesh with { Bytes = bytes.ToImmutableArray() };
        NativeBattleEffect140Frame frame = Frame();
        NativeBattleFileStaging staging = frame.Staging!.Rebase(0x11B537C).Write(0x11B537C, new byte[24]);
        foreach (uint address in new uint[] { 0x11D04B4, 0x11D2594, 0x11D3778 })
        {
            staging = staging.Write(address, new byte[8]);
        }
        frame = frame with { Staging = staging.Write(mesh.Address, mesh.Bytes.AsSpan()), State = frame.State with { StaticSourcesBound = true } };
        NativeBattleEffect140Frame result = NativeBattleEffect140Meshes.InitializeW(frame, mesh.Address, Resources(mesh), Inputs((_, _) => throw new InvalidOperationException()));
        for (int vertex = 0; vertex < 87; vertex++)
        {
            Assert.Equal((short)(4096 - vertex * 47), S(ReadBytes(result, mesh.Address + 8u + (uint)(vertex * 8), 8), 6));
        }
        Assert.Equal(4002, S(ReadBytes(result, mesh.Address + 8 + 87 * 8, 8), 6));
        Assert.Equal(4002, S(ReadBytes(result, mesh.Address + 8 + 88 * 8, 8), 6));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4).ToArray(), result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4).ToArray());
        Assert.NotNull(result.Memory.ScriptedColorRegisters!.Mac[0]);
    }
}
