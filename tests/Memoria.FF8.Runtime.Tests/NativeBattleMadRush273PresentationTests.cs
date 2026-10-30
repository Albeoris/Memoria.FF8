using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMadRush273PresentationTests
{
    private static readonly NativeBattleActionMatrix View = NativeBattleCuragaMatrices.Identity8DD770();
    private static readonly NativeBattleCameraProjection Projection = new(View, 100, 160, 108);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_mesh_retains_native_selected_vertices_temporary_bytes_and_depths(bool reject)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508520, 0x2500000, 0x310, 3);
        (pool, int slot) = pool.Append(0x68D8B0);
        NativeBattlePilotVisualIdentity identity = NativeBattleMadRush273Presentation.MeshIdentity(pool.Slots[slot], 0x1000000, 44);
        var native = Mesh(null, reject);
        string original = NativeBattleTestSnapshots.Json(native);
        var captured = Mesh(identity, reject);
        NativeBattleGfGeometryBatch finalized = NativeBattleMadRush273Presentation.PreserveNativeDepths(captured.Batch);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(captured with { Batch = finalized }));
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        if (reject)
        {
            Assert.All(finalized.Attempts, attempt => Assert.Null(attempt.Primitive));
            return;
        }
        NativeBattleGeometryPrimitive primitive = Assert.Single(finalized.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
        Assert.Equal(new Vector3(-28, -44, 3000), geometry.A);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(Payload(primitive), geometry.SourceDepths);
        for (int corner = 0; corner < 3; corner++)
        {
            Vector3 projected = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), Projection);
            uint xy = primitive.Words[corner + 2];
            Assert.InRange(MathF.Abs(unchecked((short)xy) - projected.X), 0, 1);
            Assert.InRange(MathF.Abs(unchecked((short)(xy >> 16)) - projected.Y), 0, 1);
        }
    }

    [Fact]
    public void Actual_particle_births_keep_native_CRT_state_and_break_interpolation_on_reuse_and_restore()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508510, 0x2501000, 0x18, 1);
        (pool, int slot) = pool.Append(0x68DBE0);
        var birth = NativeBattleMadRush273Particles.Initialize(new(1), pool.Slots[slot], new(100, -200, 300));
        pool = pool.WriteSlot(slot, birth.Task);
        string before = NativeBattleTestSnapshots.Json(pool);
        NativeBattlePilotVisualIdentity identity = NativeBattleMadRush273Presentation.ParticleIdentity(pool.Slots[slot], 0x1048C94);
        var native = Sprite(null);
        var captured = Sprite(identity);
        NativeBattleGfGeometryBatch finalized = NativeBattleMadRush273Presentation.PreserveNativeDepths(captured.Batch);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured with { Batch = finalized }));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(pool));
        Assert.Equal(3403800452u, birth.Random.Seed);
        Assert.Equal(2, finalized.Attempts.Length);
        NativeBattleGeometryPrimitive primitive = Assert.Single(finalized.Attempts, attempt => attempt.Primitive is not null).Primitive!;
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(Payload(primitive), geometry.SourceDepths);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 projected = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), Projection);
            uint xy = primitive.Words[corner * 2 + 2];
            Assert.InRange(MathF.Abs(unchecked((short)xy) - projected.X), 0, 1);
            Assert.InRange(MathF.Abs(unchecked((short)(xy >> 16)) - projected.Y), 0, 1);
        }
        Assert.Equal(identity, NativeBattleMadRush273Presentation.ParticleIdentity(pool.Slots[slot], 0x1048C94));
        NativeBattleEffectTaskPool restored = NativeBattleTestSnapshots.Independent(pool);
        Assert.NotEqual(identity, NativeBattleMadRush273Presentation.ParticleIdentity(restored.Slots[slot], 0x1048C94));
        pool = pool.Advance((current, _) => new(current, 2)).Pool;
        (pool, slot) = pool.Append(0x68DBE0);
        NativeBattlePilotVisualIdentity reused = NativeBattleMadRush273Presentation.ParticleIdentity(pool.Slots[slot], 0x1048C94);
        Assert.NotEqual(identity.Generation, reused.Generation);
        Assert.Equal(geometry.A, NativeBattlePilotVisualGeometry.SampleCorner(geometry, geometry with { Identity = reused, A = geometry.A + Vector3.UnitX * 100 }, 0, 0.5f));
    }

    [Fact]
    public void Concurrent_targets_and_authored_nodes_never_share_mesh_identity()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508520, 0x2500000, 0x310, 3);
        (pool, int first) = pool.Append(0x68D8B0);
        (pool, int second) = pool.Append(0x68D8B0);
        var identities = new[]
        {
            NativeBattleMadRush273Presentation.MeshIdentity(pool.Slots[first], 0x1000000, 44),
            NativeBattleMadRush273Presentation.MeshIdentity(pool.Slots[second], 0x1000000, 44),
            NativeBattleMadRush273Presentation.MeshIdentity(pool.Slots[first], 0x1000000, 88)
        };
        Assert.Equal(3, identities.Distinct().Count());
        Assert.Equal(identities[0], NativeBattleMadRush273Presentation.MeshIdentity(pool.Slots[first], 0x1000000, 44));
    }

    [Fact]
    public void Default_foreign_explicit_depth_and_control_packets_are_untouched_and_invalid_payload_fails()
    {
        var native = Mesh(null, false).Batch;
        Assert.Same(native, NativeBattleMadRush273Presentation.PreserveNativeDepths(native));
        NativeBattlePilotVisualGeometry geometry = new(new(273, 1, 2, 3, 4), Vector3.Zero, Vector3.One, Vector3.UnitX, default, 3);
        var primitive = Assert.Single(native.Attempts).Primitive!;
        var captured = primitive with { PilotGeometry = geometry };
        var batch = native with { Attempts = [Assert.Single(native.Attempts) with { Primitive = captured }] };
        var explicitBatch = batch with { Attempts = [Assert.Single(batch.Attempts) with { Primitive = captured with { PilotGeometry = geometry with { UseSourceDepths = true, SourceDepths = Vector4.One } } }] };
        Assert.Same(explicitBatch, NativeBattleMadRush273Presentation.PreserveNativeDepths(explicitBatch));
        var foreign = batch with { Attempts = [Assert.Single(batch.Attempts) with { Primitive = captured with { PilotGeometry = geometry with { Identity = geometry.Identity with { Effect = 280 } } } }] };
        Assert.Same(foreign, NativeBattleMadRush273Presentation.PreserveNativeDepths(foreign));
        var control = batch with { Attempts = [Assert.Single(batch.Attempts) with { Primitive = captured with { Words = [0x1000000, 0xE1000220] } }] };
        Assert.Same(control, NativeBattleMadRush273Presentation.PreserveNativeDepths(control));
        var invalid = batch with { Attempts = [Assert.Single(batch.Attempts) with { Primitive = captured with { VertexPayload4 = [1, 2] } }] };
        Assert.Throws<NotSupportedException>(() => NativeBattleMadRush273Presentation.PreserveNativeDepths(invalid));
    }

    private static NativeBattleModuleMeshTemporaryResult Mesh(NativeBattlePilotVisualIdentity? identity, bool reject)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(40);
        writer.Write(4);
        foreach ((short x, short y) in new (short, short)[] { (-64, -64), (64, -64), (-64, 64), (64, 64) })
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write((short)0);
            writer.Write((short)0);
        }
        for (int group = 0; group < 8; group++)
        {
            writer.Write(group == 0 ? 1 : 0);
            if (group == 0)
            {
                writer.Write(0x00808080);
                writer.Write((ushort)0);
                writer.Write((ushort)(reject ? 4 : 2));
                writer.Write((ushort)(reject ? 2 : 4));
                writer.Write((ushort)0);
            }
        }
        ImmutableArray<byte> source = stream.ToArray().ToImmutableArray();
        byte[] selected = source.AsSpan(8, 32).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(selected, -128);
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(address, new byte[88]).Write(address, BitConverter.GetBytes(0x1000000u)).Write(address + 4, BitConverter.GetBytes(0x2000000u)).Write(address + 28, BitConverter.GetBytes(0x2000u));
        NativeBattleActionMatrix matrix = View with { X = 100, Y = 20, Z = 3000 };
        return NativeBattleModulePackets.EmitRetainedBatch572200(staging, address, 0x1000000, source, matrix, Projection, new(0x2000, null, null, null, null, null, 17, 2), new(0, 0, 1f / 4096, 4503601774854144d), selected.ToImmutableArray(), 0x2000000, identity);
    }

    private static NativeBattleModuleSpriteDraw Sprite(NativeBattlePilotVisualIdentity? identity)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), View with { Z = 2000 }, true, true, 0x1234),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0()
        };
        byte[] source = new byte[38];
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(12), ushort.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(14), 1);
        source[18] = source[20] = 2;
        source[19] = 100;
        source[21] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(34), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(36), 2048);
        NativeBattleModuleResource resource = new(0, 0x1048C94, 0, 0, source.ToImmutableArray());
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, [0, 0]).Write(36, [0, 0]);
        return NativeBattleModuleSpriteDrawing.Draw571C80(memory, temporary, _ => resource, Projection, 0, pilotIdentity: identity);
    }

    private static Vector4 Payload(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.VertexPayload4.Length == 0 ? Vector4.Zero : new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]);
    }
}
