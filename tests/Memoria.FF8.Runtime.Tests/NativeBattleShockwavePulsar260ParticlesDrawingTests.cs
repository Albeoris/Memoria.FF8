using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260ParticlesDrawingTests
{
    [Theory]
    [InlineData(0, 40)]
    [InlineData(1, 48)]
    public void Puff_leaf_publishes_full_GTE_without_AVSZ_and_retains_sparse_packet_padding(byte extended, int allocated)
    {
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 999, Mac0 = 777 } };
        NativeBattleShockwavePulsar260PuffStep result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(memory, Temporary(extended), Sprite(), Projection);
        Assert.Equal(allocated, result.Batch.AllocationBytes);
        Assert.Equal(999u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(0u, result.Memory.GeometryRegisters.Mac0);
        Assert.All(result.Batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(19, attempt.Primitive!.Bucket));
        NativeBattleGfGeometryAttempt packet = result.Batch.Attempts.First();
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Memory.GeometryRegisters.Sz.ToArray());
        Assert.Equal(0u, U32(result.Temporary180, 0x40));
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x76, 2));
    }

    [Fact]
    public void Rejected_puff_retains_prefix_and_does_not_require_extended_flag_or_fourth_W()
    {
        NativeBattleEffectArena temporary = Temporary(0, 0).MarkUnknown(0x25, 1).MarkUnknown(0x62, 2);
        NativeBattleShockwavePulsar260PuffStep result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), temporary, Sprite(), Projection);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x25, 1));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Puffs_deliver_each_row_before_next_draw_and_publish_retained_physical_temporary_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 2; index++)
        {
            int offset = PuffsOffset + index * 16;
            arena = arena.Write(offset, BitConverter.GetBytes(SpriteAddress)).Write(offset + 10, BitConverter.GetBytes((short)4096)).Write(offset + 15, [1]);
        }
        NativeBattleShockwavePulsar260Frame frame = Frame(arena);
        int delivered = 0;
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffs(frame, Resources(), Camera, Projection, (current, batch) =>
        {
            delivered++;
            Assert.Equal(48, batch.AllocationBytes);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 180, Read32(current.Staging!, NativeBattleTemporaryMemory.Cursor));
            if (delivered == 2)
            {
                Assert.Equal((short)0x55AA, BinaryPrimitives.ReadInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            }
            return current with { Staging = current.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x62, BitConverter.GetBytes((short)0x55AA)) };
        });
        Assert.Equal(2, delivered);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x84, 2)));
        Assert.Equal(1, result.Staging.Read(NativeBattleTemporaryMemory.Address + 0x25, 1)[0]);
        Assert.Equal(frame.Memory.Arena!.Read(PuffsOffset, 32), result.Memory.Arena!.Read(PuffsOffset, 32));
    }

    [Fact]
    public void Empty_banks_read_only_live_fields_and_do_not_require_target_or_source_data()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length);
        for (int index = 0; index < PuffsCount; index++)
        {
            arena = arena.Write(PuffsOffset + index * 16 + 15, [0]);
        }
        for (int index = 0; index < SecondaryParticlesCount; index++)
        {
            arena = arena.Write(SecondaryParticlesOffset + index * 16, [0, 0]);
        }
        NativeBattleShockwavePulsar260Frame frame = Frame(arena);
        NativeBattleShockwavePulsar260Frame first = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffs(frame, Resources(), Camera, Projection, (_, _) => throw new InvalidOperationException());
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawSecondaryParticles(first, Resources(), Camera, Projection, Constants, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(unchecked((uint)-1024), Read32(result.Staging!, NativeBattleTemporaryMemory.Address + 0x38));
        Assert.Equal(0x30u, Read32(result.Staging!, NativeBattleTemporaryMemory.Address + 0x3C));
    }

    [Fact]
    public void Secondary_uses_target_position_and_truncated_signed_midpoint_then_static30_source()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(SecondaryParticlesOffset, BitConverter.GetBytes((short)1)).Write(SecondaryParticlesOffset + 2, BitConverter.GetBytes((short)7)).Write(SecondaryParticlesOffset + 4, BitConverter.GetBytes((short)2048)).Write(SecondaryParticlesOffset + 6, BitConverter.GetBytes((short)4096)).Write(SecondaryParticlesOffset + 12, BitConverter.GetBytes(0x2500000u));
        NativeBattleEffectArena target = NativeBattleEffectArena.Clear().MarkUnknown(0, 36).Write(0, BitConverter.GetBytes(0x1D97000u)).Write(0x20, BitConverter.GetBytes((short)-3)).Write(0x22, BitConverter.GetBytes((short)-2));
        int calls = 0;
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawSecondaryParticles(Frame(arena), Resources(), Camera, Projection, Constants, pointer =>
        {
            Assert.Equal(0x2500000u, pointer);
            return target;
        }, pointer =>
        {
            Assert.Equal(0x1D97000u, pointer);
            return new(100, 10, 300);
        }, (current, batch) =>
        {
            calls++;
            Assert.Empty(batch.Attempts);
            Assert.Equal(new int?[] { 100, 15, 2300 }, current.Memory.GeometryRegisters!.Translation.ToArray());
            Assert.Null(current.Memory.GeometryRegisters.Rotation20[18]);
            Assert.Equal(0x111D314u, Read32(current.Staging!, NativeBattleTemporaryMemory.Address + 0x24));
            return current;
        });
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal((short)-4096, BinaryPrimitives.ReadInt16LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address, 2)));
        Assert.Equal((short)2048, BinaryPrimitives.ReadInt16LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 8, 2)));
    }

    [Fact]
    public void Static30_mesh_uses_real_plus8_vertices_and_preserves_selected_ACT_guards()
    {
        NativeBattleStaticMeshResource mesh = Mesh(true);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(0x18, BitConverter.GetBytes(-1024)).Write(0x1C, BitConverter.GetBytes(0x30u));
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Camera.ReadMatrix(), true, true, null) };
        NativeBattleShockwavePulsar260MeshStep result = NativeBattleShockwavePulsar260Mesh.Draw(memory, new(mesh.Address, mesh.Bytes), 0, context, Projection, Constants);
        Assert.Equal(mesh.Address + 8, U32(result.Context88, 4));
        Assert.Equal(48, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Equal(0u, U32(result.Context88, 0x2C));
        Assert.Throws<NotSupportedException>(() => NativeBattleShockwavePulsar260Mesh.Draw(memory, new(mesh.Address, mesh.Bytes), 0, context.Write(4, BitConverter.GetBytes(0x20E0000u)).Write(0x1C, BitConverter.GetBytes(0x2030u)), Projection, Constants));
    }

    [Fact]
    public void Puff_unknown_W_and_padding_have_identical_JSON_next_without_rehydration()
    {
        NativeBattleShockwavePulsar260PuffStep first = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), Temporary(1), Sprite(), Projection);
        NativeBattleShockwavePulsar260PuffStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShockwavePulsar260PuffStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleShockwavePulsar260PuffStep Next(NativeBattleShockwavePulsar260PuffStep value) => NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(value.Memory, value.Temporary180, Sprite(), Projection);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Theory]
    [InlineData(0, 2000)]
    [InlineData(1, 2000)]
    [InlineData(0, 0)]
    public void Presentation_puff_keeps_exact_separate_projection_full_native_ledger_and_lazy_fourth(byte extended, int z)
    {
        NativeBattleShockwavePulsar260PuffStep plain = Capture(null);
        NativeBattleShockwavePulsar260PuffStep captured = Capture(new(260, 0x100, 0, 123, 12));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        if (z != 0)
        {
            NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
            NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
            Assert.Equal(new System.Numerics.Vector3(0, 0, 2000), geometry.A);
            Assert.Equal(new System.Numerics.Vector3(128, 128, 2000), geometry.D);
            Assert.Equal(new System.Numerics.Vector4(2000, 2000, 2000, 2000), geometry.SourceDepths);
            Assert.NotNull(geometry.Sprite);
        }
        else
        {
            Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
            Assert.Throws<NotSupportedException>(() => captured.Temporary180.Read(0x62, 2));
        }

        NativeBattleShockwavePulsar260PuffStep Capture(NativeBattlePilotVisualIdentity? identity)
        {
            NativeBattleEffectArena temporary = Temporary(extended, z);
            if (z == 0)
            {
                temporary = temporary.MarkUnknown(0x25, 1).MarkUnknown(0x62, 2);
            }
            return NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), temporary, Sprite(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), pilotIdentity: identity);
        }
    }

    [Fact]
    public void Presentation_puff_phase_shape_is_discrete_while_actual_row_origin_moves()
    {
        NativeBattlePilotVisualIdentity identity = new(260, 0x100, 0, 123, 12);
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleShockwavePulsar260PuffStep first = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), Temporary(0), Sprite(), projection, pilotIdentity: identity);
        byte[] changed = Sprite().Bytes.ToArray();
        changed[16] = 10;
        BinaryPrimitives.WriteInt16LittleEndian(changed.AsSpan(24), 4);
        NativeBattleEffectArena later = Temporary(0).Write(0x98, BitConverter.GetBytes(100));
        NativeBattleShockwavePulsar260PuffStep second = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), later, Sprite() with { Bytes = changed.ToImmutableArray() }, projection, pilotIdentity: identity);
        NativeBattlePilotVisualGeometry a = Assert.Single(first.Batch.Attempts).Primitive!.PilotGeometry!;
        NativeBattlePilotVisualGeometry b = Assert.Single(second.Batch.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(a.Identity, b.Identity);
        Assert.Equal(0, a.Sprite!.Origin.X);
        Assert.Equal(100, b.Sprite!.Origin.X);
        Assert.NotEqual(a.Sprite.LocalA, b.Sprite.LocalA);
        Assert.Equal(50, NativeBattlePilotVisualGeometry.SampleCorner(a, b, 0, 0.5f).X);
        Assert.NotEqual(System.Numerics.Vector3.Lerp(a.A, b.A, 0.5f), NativeBattlePilotVisualGeometry.SampleCorner(a, b, 0, 0.5f));
    }

    [Fact]
    public void Presentation_covers_all128_native_puff_rows_with_independent_row_birth_identities()
    {
        List<NativeBattleGfGeometryBatch> plainBatches = [];
        List<NativeBattleGfGeometryBatch> capturedBatches = [];
        NativeBattleShockwavePulsar260Frame plain = Capture(null, plainBatches);
        NativeBattleShockwavePulsar260Frame captured = Capture(new(260, 0x100, 0, 123, 12), capturedBatches);
        Assert.Equal(PuffsCount, capturedBatches.Count);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(plainBatches), NativeBattleTestSnapshots.Json(capturedBatches));
        NativeBattlePilotVisualIdentity[] identities = capturedBatches.SelectMany(batch => batch.Attempts).Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).ToArray();
        Assert.Equal(PuffsCount, identities.Distinct().Count());

        NativeBattleShockwavePulsar260Frame Capture(NativeBattlePilotVisualIdentity? identity, List<NativeBattleGfGeometryBatch> batches)
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            for (int index = 0; index < PuffsCount; index++)
            {
                int row = PuffsOffset + index * 16;
                arena = arena.Write(row, BitConverter.GetBytes(SpriteAddress)).Write(row + 10, BitConverter.GetBytes((short)4096)).Write(row + 15, [1]);
            }
            return NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffs(Frame(arena), Resources(), Camera, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), (current, batch) =>
            {
                batches.Add(batch);
                return current;
            }, pilotIdentity: identity);
        }
    }

    [Fact]
    public void Presentation_covers_all64_secondary_rows_and_preserves_full_native_context_and_packets()
    {
        List<NativeBattleGfGeometryBatch> plainBatches = [];
        List<NativeBattleGfGeometryBatch> capturedBatches = [];
        NativeBattleShockwavePulsar260Frame plain = Capture(null, plainBatches);
        string nativeFrame = NativeBattleTestSnapshots.Json(plain);
        string nativeBatches = NativeBattleTestSnapshots.Json(plainBatches);
        Assert.Equal(SecondaryParticlesCount, plainBatches.Count);
        Assert.All(plainBatches, batch =>
        {
            NativeBattleGeometryPrimitive polygon = Assert.Single(batch.Attempts, attempt => attempt.Primitive is { } primitive && primitive.Words[1] >> 24 == 0x2C).Primitive!;
            Assert.Equal(new[] { 5000, 5000, 5000, 5000 }, polygon.VertexPayload4);
            Assert.Null(polygon.PilotGeometry);
        });
        NativeBattleShockwavePulsar260Frame captured = Capture(new(260, 0x100, 0, 123, 13), capturedBatches);
        Assert.Equal(SecondaryParticlesCount, capturedBatches.Count);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeBatches, NativeBattleTestSnapshots.Json(capturedBatches));
        NativeBattlePilotVisualIdentity[] identities = capturedBatches.SelectMany(batch => batch.Attempts).Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).ToArray();
        Assert.Equal(SecondaryParticlesCount, identities.Distinct().Count());
        Assert.All(capturedBatches, batch =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.Single(batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!.PilotGeometry!;
            Assert.Equal(new System.Numerics.Vector3(500, -2, 3000), geometry.A);
            Assert.Equal(new System.Numerics.Vector3(-300, -2, 3000), geometry.B);
            Assert.Equal(new System.Numerics.Vector3(500, 398, 3000), geometry.C);
            Assert.Equal(new System.Numerics.Vector3(-300, 398, 3000), geometry.D);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(5000, 5000, 5000, 5000), geometry.SourceDepths);
        });

        NativeBattleShockwavePulsar260Frame Capture(NativeBattlePilotVisualIdentity? identity, List<NativeBattleGfGeometryBatch> batches)
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            for (int index = 0; index < SecondaryParticlesCount; index++)
            {
                int row = SecondaryParticlesOffset + index * 16;
                arena = arena.Write(row, BitConverter.GetBytes((short)1)).Write(row + 4, BitConverter.GetBytes((short)2048)).Write(row + 6, BitConverter.GetBytes((short)4096)).Write(row + 12, BitConverter.GetBytes(0x2500000u));
                Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(row, 2)));
            }
            NativeBattleEffectArena target = NativeBattleEffectArena.Clear().MarkUnknown(0, 36).Write(0, BitConverter.GetBytes(0x1D972C0u)).Write(0x20, BitConverter.GetBytes((short)-3)).Write(0x22, BitConverter.GetBytes((short)-2));
            NativeBattleShockwavePulsar260Resources resources = Resources() with { ParticleMesh = Mesh(face: true) };
            int targetReads = 0;
            int positionReads = 0;
            NativeBattleShockwavePulsar260Frame frame = Frame(arena);
            NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawSecondaryParticles(frame, resources, Camera, new(Camera.ReadMatrix(), 100, 160, 108), Constants, address =>
            {
                Assert.Equal(0x2500000u, address);
                targetReads++;
                return target;
            }, model =>
            {
                Assert.Equal(0x1D972C0u, model);
                positionReads++;
                return new(100, 200, 3000);
            }, (current, batch) =>
            {
                batches.Add(batch);
                return current;
            }, pilotIdentity: identity);
            Assert.Equal(SecondaryParticlesCount, targetReads);
            Assert.Equal(SecondaryParticlesCount, positionReads);
            Assert.Equal(frame.Memory.Arena!.Read(SecondaryParticlesOffset, SecondaryParticlesCount * 16), result.Memory.Arena!.Read(SecondaryParticlesOffset, SecondaryParticlesCount * 16));
            Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
            return result;
        }
    }

    [Theory]
    [InlineData((byte)0x2C, 2000, 2000)]
    [InlineData((byte)0x24, 2000, 0)]
    [InlineData((byte)0, 0, 0)]
    public void Presentation_retains_opcode_specific_native_puff_depth_sidecars(byte opcode, int firstDepth, int fourthDepth)
    {
        byte[] bytes = Sprite().Bytes.ToArray();
        bytes[19] = opcode;
        NativeBattleModuleResource source = Sprite() with { Bytes = bytes.ToImmutableArray() };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleShockwavePulsar260PuffStep plain = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), Temporary(0), source, projection);
        NativeBattleShockwavePulsar260PuffStep captured = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), Temporary(0), source, projection, pilotIdentity: new(260, 0x100, 0, 123, 12));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        Assert.Equal(firstDepth, primitive.VertexPayload4[0]);
        Assert.Equal(fourthDepth, primitive.VertexPayload4[3]);
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), primitive.PilotGeometry!.SourceDepths);
    }

    private const uint SpriteAddress = 0x111CF4C;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static NativeBattlePackedMatrix32 Camera => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleShockwavePulsar260Frame Frame(NativeBattleEffectArena arena)
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, new byte[180]));
        return new(Memory() with { Arena = arena }, null!, new(1), NativeBattleShockwavePulsar260State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress)) { Staging = staging };
    }
    private static NativeBattleShockwavePulsar260Resources Resources() => new(null!, [], [Sprite()], Mesh(), [], [], [], []);
    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[36];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x80000001);
        bytes[16] = 8;
        bytes[17] = 0x80;
        bytes[18] = 8;
        bytes[19] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), 4096);
        return new(0, SpriteAddress, 0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleEffectArena Temporary(byte extended, int z = 2000)
    {
        NativeBattleEffectArena value = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0x25, [extended]).Write(0x2C, BitConverter.GetBytes(SpriteAddress + 16)).Write(0x30, BitConverter.GetBytes(1));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, 0);
        value = value.Write(0x84, packed.Bytes.AsSpan());
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            value = value.Write(offset, [0, 0]);
        }
        return value;
    }
    private static NativeBattleStaticMeshResource Mesh(bool face = false)
    {
        int directory = face ? 40 : 8;
        byte[] bytes = new byte[directory + 32 + (face ? 24 : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, directory);
        if (face)
        {
            short[] values = [-400, -400, 0, 1, 400, -400, 0, 2, -400, 400, 0, 3, 400, 400, 0, 4];
            for (int index = 0; index < values.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), values[index]);
            }
            int at = directory + 12;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 4), 0x2C112233);
            for (int index = 0; index < 4; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 8 + index * 2), (ushort)(index * 2));
            }
        }
        return new(0, 0x111D30C, bytes.ToImmutableArray());
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint Read32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
