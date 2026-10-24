using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActDepthRoutingTests
{
    [Fact]
    public void Programme227_retains_native_negative_depth_for_an_actual_polygon()
    {
        NativeBattleEffect227Frame frame = NativeBattleEffect227RecordsTests.Frame();
        NativeBattleGfAnimationSource source = ProgrammeSource(0x112DCF8);
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, source.Data.AsSpan().Slice(16).ToArray().ToImmutableArray());
        NativeBattleEffect227Resources resources = NativeBattleEffect227RecordsTests.Resources() with { Geometry = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        NativeBattleEffect227RecordsTests.Harness harness = new();
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().Write(8, BitConverter.GetBytes(-128));
        NativeBattleEffect227Frame result = NativeBattleEffect227Programme.DrawNode(frame, source, Node(2000), caller, resources, harness.Inputs());
        AssertBias(result.Memory, Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(harness.Operations)).Batch);
        Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88).Read(0x18, 4)));
    }

    [Fact]
    public void Raw227_retains_native_negative_depth_for_an_actual_polygon()
    {
        NativeBattleEffect227Frame frame = NativeBattleEffect227RecordsTests.Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508718, 0x20DFAB8, 24, 16);
        (pool, int slot) = pool.Append(0x694360);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[12]).WriteWord(12, 4)) };
        frame = frame with { Effect = frame.Effect with { Pools = [pool] }, State = frame.State.WriteView(0x2508758, NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)2000)), 8) };
        frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]) };
        byte[] geometry = Geometry(false).ToArray();
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(8 + vertex * 8), (short)((vertex & 1) == 0 ? -80 : 80));
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(10 + vertex * 8), (short)(vertex < 2 ? -80 : 80));
        }
        NativeBattleStaticMeshResource mesh = new(0, 0x112DAA8, geometry.ToImmutableArray());
        NativeBattleEffect227Resources resources = NativeBattleEffect227RecordsTests.Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        NativeBattleEffect227RecordsTests.Harness harness = new();
        NativeBattleEffect227Step result = NativeBattleEffect227Lower.Advance(frame, pool.BankAddress, resources, harness.Inputs());
        AssertBias(result.Frame.Memory, Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(harness.Operations)).Batch);
    }

    [Fact]
    public void Programme319_retains_native_negative_depth_for_an_actual_polygon()
    {
        NativeBattleEffect319LowerTests.Fixture fixture = new(0x65F2B0);
        NativeBattleGfAnimationSource source = ProgrammeSource(0xFF24F8);
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, source.Data.AsSpan().Slice(16).ToArray().ToImmutableArray());
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffectArena caller = NativeBattleEffect319Controller.PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null)).Write(0x20, BitConverter.GetBytes(-128));
        NativeBattleEffect319Frame result = NativeBattleEffect319Programme.DrawNode(fixture.Frame, source, Node(0), caller, fixture.Resources, fixture.Inputs());
        AssertBias(result.Memory, Assert.IsType<NativeBattleEffect319DrawBatch>(Assert.Single(fixture.Operations)).Batch);
    }

    private static void AssertBias(NativeBattleEffectMemory memory, NativeBattleGfGeometryBatch batch)
    {
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        Assert.NotNull(primitive);
        int otz = checked((int)memory.GeometryRegisters!.Otz!.Value);
        Assert.True(otz > 128);
        Assert.Equal(17 + ((otz - 128) >> 2), primitive.Bucket);
        Assert.Equal(4, primitive.VertexPayload4.Length);
    }

    internal static NativeBattleGfAnimationNode Node(short z)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x200);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), z);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }

    internal static NativeBattleGfAnimationSource ProgrammeSource(uint address)
    {
        ImmutableArray<byte> geometry = Geometry(true);
        byte[] bytes = new byte[16 + geometry.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        geometry.AsSpan().CopyTo(bytes.AsSpan(16));
        return new(address, bytes.ToImmutableArray());
    }

    internal static ImmutableArray<byte> Geometry(bool act)
    {
        int header = act ? 12 : 8;
        int directory = header + 32;
        byte[] bytes = new byte[directory + 32 + 24];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, directory);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        if (act)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        }
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(header + i * 8), (short)((i & 1) == 0 ? -400 : 400));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(header + i * 8 + 2), (short)(i < 2 ? -400 : 400));
        }
        int face = directory + 16;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x2C112233);
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + i * 2), (ushort)(i * 2));
        }
        return bytes.ToImmutableArray();
    }
}
