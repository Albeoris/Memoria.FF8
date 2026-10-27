using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect209Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect209LowerTests
{
    [Fact]
    public void Grid_initialization_preserves_packet_CLUT_and_publishes_both_native_template_buffers()
    {
        Fixture fixture = new();
        fixture.Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { GuardianWorkBuffer = fixture.Frame.Memory.GuardianWorkBuffer!.Write(30, [0xEF, 0xBE]).Write(70, [0xCD, 0xAB]) } };
        NativeBattleEffect209Frame result = NativeBattleEffect209Lower.InitializeGrid7F40(fixture.Frame);
        Assert.Equal((ushort)3, result.State.ReadWord(GridAddress + 10));
        Assert.Equal((ushort)0, result.State.ReadWord(GridAddress + (41 + 1) * 12 + 10));
        Assert.Equal(GridAddress, U(result.Memory.GuardianWorkBuffer!.Read(0, 4)));
        Assert.Equal(GridAddress + 504, U(result.Memory.GuardianWorkBuffer.Read(12, 4)));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, result.Memory.GuardianWorkBuffer.Read(30, 2));
        Assert.Equal(new byte[] { 0xCD, 0xAB }, result.Memory.GuardianWorkBuffer.Read(70, 2));
        Assert.Equal(new byte[] { 8, 0 }, result.Memory.GuardianWorkBuffer.Read(36, 2));
        Assert.Equal(new byte[] { 0, 8 }, result.Memory.GuardianWorkBuffer.Read(44, 2));
        Assert.Equal((ushort)215, result.Memory.GuardianWorkBuffer.Read((27 * 40 - 1) * 96 + 16 + 37, 1)[0]);
        Assert.Equal(0x09000000u, U(result.Memory.GuardianWorkBuffer.Read(16 + 40, 4)));
    }

    [Fact]
    public void Grid_draw_links_existing_second_buffer_and_restores_scalar_focal_without_consuming_COMMON()
    {
        Fixture fixture = new();
        fixture.Frame = NativeBattleEffect209Lower.InitializeGrid7F40(fixture.Frame);
        NativeBattleEffect209Frame result = NativeBattleEffect209Lower.DrawGrid8250(fixture.Frame, 1, 0x123456, fixture.Inputs());
        Assert.Equal(1080, fixture.Addressed.Count);
        Assert.Equal(0x209FAB8u + 56, fixture.Addressed[0].Address);
        Assert.All(fixture.Addressed, draw => Assert.Equal(0x101A, draw.Bucket));
        Assert.Equal(new ushort[] { 2048, 777 }, fixture.ProjectionWrites);
        Assert.Equal(0, result.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(0x2C123456u, U(result.Memory.GuardianWorkBuffer!.Read(60, 4)));
        Assert.Equal(0x2C808080u, U(result.Memory.GuardianWorkBuffer.Read(20, 4)));
        Assert.Equal((uint)0x209FAB8, result.State.ReadDword(GuardianGlobal));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.GeometryRegisters!.Rotation20.Take(2).Select(value => value!.Value).ToArray()));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
    }

    [Fact]
    public void Deformation_keeps_edge_rows_and_retained_anchor_W_while_publishing_current_projection()
    {
        Fixture fixture = new();
        fixture.Frame = NativeBattleEffect209Lower.InitializeGrid7F40(fixture.Frame);
        NativeBattleEffect209Frame result = NativeBattleEffect209Lower.DeformGrid80F0(fixture.Frame, 10, 60, fixture.Inputs());
        Assert.Equal((ushort)0, result.State.ReadWord(GridAddress + 8));
        Assert.Contains(Enumerable.Range(0, 28 * 41), index => result.State.ReadWord(GridAddress + (uint)(index * 12 + 8)) != 0);
        Assert.Equal(1, fixture.PointQueries);
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.NotNull(result.Memory.GeometryRegisters!.Sxy[2]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.DoesNotContain(true, result.Staging.ReadSnapshot(NativeBattleTemporaryMemory.Address + 6, 2).Known);
    }

    [Fact]
    public void Screen_pause_repeats_draw_and_capture_before_clock_and_stage_visibility_updates()
    {
        Fixture fixture = new(0x637A40);
        fixture.Flags = 0x201;
        NativeBattleEffect209Step result = NativeBattleEffect209Lower.Advance(fixture.Frame, ChildBank, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal((ushort)0, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(20, fixture.Batches.Count);
        Assert.Equal(1480, result.Frame.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect209CaptureStage7D30>());
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect209SetStageFlags2>());
        Assert.Equal((ushort)1, fixture.Batches[0].Flags);
        Assert.Equal((ushort)0, fixture.Batches[1].Flags);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, fixture.Batches[0].Batch.Attempts[0].WriteMask[14]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, fixture.Batches[1].Batch.Attempts[0].Primitive!.VertexPayload4);
    }

    [Fact]
    public void Screen_age100_retires_after_restoring_stage_visibility_without_another_capture()
    {
        Fixture fixture = new(0x637A40);
        fixture.Frame = Write(fixture.Frame, ChildBank, Read(fixture.Frame, ChildBank).WriteWord(12, 100));
        NativeBattleEffect209Step result = NativeBattleEffect209Lower.Advance(fixture.Frame, ChildBank, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)101, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.True(Assert.Single(fixture.Operations.OfType<NativeBattleEffect209SetStageFlags2>()).Enabled);
        Assert.Empty(fixture.Batches);
    }

    [Theory]
    [InlineData(0x6385E0)]
    [InlineData(0x638900)]
    [InlineData(0x638A80)]
    public void Static_callbacks_draw_before_pause_and_retain_task_motion(int callback)
    {
        Fixture fixture = new((uint)callback);
        fixture.Flags = 0x201;
        NativeBattleModulePoolTask before = NativeBattleStorageSnapshots.Capture(Read(fixture.Frame, ChildBank));
        NativeBattleEffect209Step result = NativeBattleEffect209Lower.Advance(fixture.Frame, ChildBank, fixture.Resources, fixture.Inputs());
        Assert.Single(fixture.Batches);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(before).Payload, NativeBattleStorageSnapshots.Capture(Read(result.Frame, ChildBank)).Payload);
        Assert.Equal(0, result.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Explosion_uses_arithmetic_angular_decay_and_signed_truncated_scale_decay()
    {
        Fixture fixture = new(0x638A80);
        fixture.Frame = Write(fixture.Frame, ChildBank, Read(fixture.Frame, ChildBank).WriteWord(0x1A, ushort.MaxValue).WriteWord(0x1E, unchecked((ushort)-17)));
        NativeBattleEffect209Step result = NativeBattleEffect209Lower.Advance(fixture.Frame, ChildBank, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result.Frame, ChildBank);
        Assert.Equal(ushort.MaxValue, task.ReadWord(0x18));
        Assert.Equal((ushort)0, task.ReadWord(0x1A));
        Assert.Equal(unchecked((ushort)-16), task.ReadWord(0x1E));
        Assert.Equal((ushort)4079, task.ReadWord(0x1C));
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(3, 48)]
    public void Windowed_selected_faces_keep_current_OTZ_DPCS_and_leading_E2_zero_depths(int group, int allocation)
    {
        Fixture fixture = new();
        NativeBattleWindowedMesh144Step result = NativeBattleEffect209Windowed144.DrawLoaded(fixture.Frame.Memory, Mesh(0xE26340, 158, group), fixture.Projection(), WindowContext(0), fixture.Inputs().Colors);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(allocation, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, 0, 0, 0 }, packet.Primitive!.VertexPayload4);
        Assert.Equal(0xE2000000u, packet.Primitive.Words[^1]);
        Assert.Equal(17 + (int)(result.Memory.GeometryRegisters!.Otz!.Value >> 2), packet.Primitive.Bucket);
        Assert.Equal(1024u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(result.Memory.GeometryRegisters.Otz, U(result.Context144.Read(0x34, 4)));
        Assert.False(result.Context144.ReadSnapshot(0x30, 1).Known[0]);
    }

    [Fact]
    public void Triangle_negative_area_is_admitted_by_selected_F3_flags()
    {
        Fixture fixture = new();
        NativeBattleStaticMeshResource mesh = Mesh(0xE26340, 158, 2);
        byte[] bytes = mesh.Bytes.ToArray();
        int face = 8 + 158 * 8 + 2 * 4 + 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 2);
        mesh = mesh with { Bytes = bytes.ToImmutableArray() };
        NativeBattleWindowedMesh144Step result = NativeBattleEffect209Windowed144.DrawLoaded(fixture.Frame.Memory, mesh, fixture.Projection(), WindowContext(0), fixture.Inputs().Colors);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)U(result.Context144.Read(0x2C, 4))) < 0);
    }

    [Fact]
    public void Windowed_near_rejection_retains_prefix_and_unknown_area_depth_and_E2_fields()
    {
        Fixture fixture = new();
        NativeBattleWindowedMesh144Step result = NativeBattleEffect209Windowed144.DrawLoaded(fixture.Frame.Memory, Mesh(0xE26340, 158, 2, -2000), fixture.Projection(), WindowContext(0), fixture.Inputs().Colors);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[4]);
        Assert.False(result.Context144.ReadSnapshot(0x2C, 1).Known[0]);
        Assert.False(result.Context144.ReadSnapshot(0x34, 1).Known[0]);
    }

    [Fact]
    public void Triangle_zero_area_still_publishes_three_V_scratch_words_and_retains_fourth()
    {
        Fixture fixture = new();
        NativeBattleWindowedMesh144Step result = NativeBattleEffect209Windowed144.DrawLoaded(fixture.Frame.Memory, Mesh(0xE26340, 158, 2, 2000, true), fixture.Projection(), WindowContext(8), fixture.Inputs().Colors);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(130u, U(result.Context144.Read(0x70, 4)));
        Assert.Equal(130u, U(result.Context144.Read(0x78, 4)));
        Assert.False(result.Context144.ReadSnapshot(0x7C, 1).Known[0]);
        Assert.False(result.Context144.ReadSnapshot(0x34, 1).Known[0]);
    }

    [Fact]
    public void Capture_context_writes_only_reached_fields_and_retains_other_unknown_bytes()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0, [0x5A]);
        NativeBattleEffectArena context = NativeBattleEffect209Lower.CreateCaptureContext7D30(retained, 0x123456, 0x9988);
        Assert.Equal(0x123456u, U(context.Read(4, 4)));
        Assert.Equal(0x9988u, U(context.Read(16, 4)));
        Assert.Equal((ushort)320, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(24, 2)));
        Assert.Equal(0x5A, context.Read(0, 1)[0]);
        Assert.False(context.ReadSnapshot(0x1C, 1).Known[0]);
        Assert.False(context.ReadSnapshot(0x24, 1).Known[0]);
        Assert.False(context.ReadSnapshot(0x28, 1).Known[0]);
    }

    [Fact]
    public void Capture_context_transports_unknown_renderer_flag_bytes_without_consuming_or_filling_them()
    {
        NativeBattleEffectArena flags = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(0, [0x12]).Write(2, [0xA5]);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0x27, [0x5A]);
        NativeBattleEffectArena context = NativeBattleEffect209Lower.CreateCaptureContext7D30(retained, 0x123456, flags);
        (byte[] bytes, bool[] known) = context.ReadSnapshot(0x10, 4);
        Assert.Equal(new bool[] { true, false, true, false }, known);
        Assert.Equal(0x12, bytes[0]);
        Assert.Equal(0xA5, bytes[2]);
        Assert.False(context.ReadSnapshot(0x1C, 1).Known[0]);
        Assert.False(context.ReadSnapshot(0x24, 1).Known[0]);
        Assert.Equal(0x5A, context.Read(0x27, 1)[0]);
        Assert.False(context.ReadSnapshot(0x28, 1).Known[0]);
    }

    [Fact]
    public void Grid_and_retained_template_CLUT_round_trip_in_current_JSON_owners()
    {
        Fixture fixture = new();
        fixture.Frame = NativeBattleEffect209Lower.InitializeGrid7F40(fixture.Frame);
        NativeBattleEffect209Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect209Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Frame)))!);
        Assert.Equal(fixture.Frame.State.Read(GridAddress, GridLength), restored.State.Read(GridAddress, GridLength));
        Assert.Equal(fixture.Frame.Memory.GuardianWorkBuffer!.Read(0, 96), restored.Memory.GuardianWorkBuffer!.Read(0, 96));
    }

    private sealed class Fixture
    {
        public NativeBattleEffect209Frame Frame;
        public NativeBattleEffect209Resources Resources;
        public uint Flags;
        public ushort H = 100;
        public int PointQueries;
        public List<NativeBattleEffect209Operation> Operations = [];
        public List<NativeBattleEffect209DrawBatch> Batches = [];
        public List<NativeBattleEffect209DrawAddressedTemplate> Addressed = [];
        public List<ushort> ProjectionWrites = [];
        public Fixture(uint callback = 0)
        {
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GuardianWorkBuffer = NativeBattleGuardianWorkBuffer.FromColdProcess(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            if (callback != 0)
            {
                (pool, _) = pool.Append(callback);
                pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(0x1C, 4096).WriteWord(0x20, 4096));
            }
            Frame = new(memory, new(209, 208, new(209, Entry, Initializer), null, null) { Pools = [pool] }, new(123), NativeBattleEffect209State.FromColdProcess().WriteDword(GuardianGlobal, 0x209FAB8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = new uint[] { 0xE277B8, 0xE290E0, 0xE29588 }.ToImmutableDictionary(value => value, value => Mesh(value, 4, 3));
            Resources = new(new("fixture", []), new("fixture", []), new("fixture", []), meshes, Enumerable.Repeat(0x404040u, 8).ToImmutableArray(), Enumerable.Repeat(0x404040u, 24).ToImmutableArray(), Enumerable.Repeat(0x404040u, 18).ToImmutableArray(), [], [], [], ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(MeshDirectoryAddress, BitConverter.GetBytes(0xE29588u).ToImmutableArray()));
        }
        public NativeBattleCameraProjection Projection() => new(null, H, 160, 108);
        public NativeBattleEffect209Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, frame => new(frame, 0), null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => Projection(), _ => 0, _ => 777, _ => 0, (_, _) => 0, (frame, _, retained) => new(frame, retained), (frame, _, _, _, retained) =>
        {
            PointQueries++;
            return new(frame, PutPoint(retained, 0, new(0, 0, 0)));
        }, (_, _) => false, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect209SetProjectionH projection)
            {
                H = projection.Value;
                ProjectionWrites.Add(H);
            }
            if (operation is NativeBattleEffect209DrawBatch batch)
            {
                Batches.Add(batch);
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess! with { ByteCursor = frame.Memory.PrimitiveProcess.ByteCursor + batch.Batch.AllocationBytes } } };
            }
            if (operation is NativeBattleEffect209DrawAddressedTemplate draw)
            {
                Addressed.Add(draw);
                frame = frame with { Memory = frame.Memory with { GuardianWorkBuffer = frame.Memory.GuardianWorkBuffer!.Write(checked((int)(draw.Address - 0x209FAB8)), draw.Data40.Read(0, 40)) } };
            }
            return frame;
        });
    }

    private static NativeBattleEffectArena WindowContext(int v) => NativeBattleEffectArena.Clear().MarkUnknown(0, 144).Write(0, BitConverter.GetBytes(0xE26340u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(1024)).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(v)).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(0xF3u)).Write(0x24, BitConverter.GetBytes(0x4080C0u));
    private static uint U(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static NativeBattleStaticMeshResource Mesh(uint address, int vertices, int group, short z = 2000, bool collinear = false)
    {
        int polygon = 8 + vertices * 8;
        int stride = group == 2 ? 20 : 24;
        byte[] bytes = new byte[polygon + 32 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertices);
        short[] xy = collinear ? [-200, -200, 0, 0, 200, 200, 200, 200] : [-200, -200, 200, -200, -200, 200, 200, 200];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), xy[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), z);
        }
        int face = polygon + group * 4 + 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        for (int index = 0; index < (group == 2 ? 3 : 4); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x2C404040);
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xFA14);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x0000FA14);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x0000FA14);
        if (group == 3)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0xFA14FA14);
        }
        return new(0, address, bytes.ToImmutableArray());
    }
}
