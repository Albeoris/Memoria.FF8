using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect207LowerTests
{
    [Fact]
    public void Preprocessing_writes_radial_W_and_reorders_both_current_face_groups()
    {
        Fixture fixture = new();
        NativeBattleEffect207Frame result = NativeBattleEffect207Lower.Preprocess6070(fixture.Frame, MutableMeshAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(0u, result.State.ReadDword(MutableMeshAddress + 12));
        Assert.Equal((ushort)77, result.State.ReadWord(MutableMeshAddress + 20));
        Assert.Equal((ushort)8, result.State.ReadWord(MutableMeshAddress + 22));
        Assert.Equal(new ushort[] { 3, 6, 0 }, Enumerable.Range(0, 3).Select(index => result.State.ReadWord(MutableMeshAddress + 64 + (uint)(index * 2))));
        Assert.Equal(new ushort[] { 3, 9, 0, 6 }, Enumerable.Range(0, 4).Select(index => result.State.ReadWord(MutableMeshAddress + 88 + (uint)(index * 2))));
        Assert.Equal(0x30202020u, result.State.ReadDword(MutableMeshAddress + 60));
        Assert.Equal(0x30202020u, result.State.ReadDword(MutableMeshAddress + 84));
        Assert.Equal(0xABCDEFu, result.Memory.GeometryRegisters!.Mac0);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void All_zero_XY_skips_leading_register_writes_and_preserves_unwritten_temporary_bytes()
    {
        Fixture fixture = new();
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(MutableMeshAddress + 4, 1).WriteDword(MutableMeshAddress, 16).Write(MutableMeshAddress + 16, new byte[32]), Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x54, [0xA5]).MarkUnknown(NativeBattleTemporaryMemory.Address + 0x55, 1) };
        NativeBattleEffect207Frame result = NativeBattleEffect207Lower.Preprocess6070(fixture.Frame, MutableMeshAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(0x1234u, result.Memory.GeometryRegisters!.Lzcs);
        Assert.Equal(7u, result.Memory.GeometryRegisters.Lzcr);
        Assert.Equal(0xA5, result.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x54, 1)[0]);
        Assert.False(result.Staging.ReadSnapshot(NativeBattleTemporaryMemory.Address + 0x55, 1).Known[0]);
    }

    [Theory]
    [InlineData(3, 48, 40)]
    [InlineData(7, 60, 52)]
    public void Selected_custom_quads_insert_zero_depth_E1_before_actual_depth_body(int group, int allocation, int modeOffset)
    {
        Fixture fixture = new();
        uint source = 0x1000000;
        NativeBattleStaticMeshResource mesh = Quad(source, group, 2000);
        fixture.Resources = fixture.Resources with { Geometry = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(source, mesh), StaticFronts = fixture.Resources.StaticFronts.Add(source, mesh.Bytes) };
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 108);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(source + 12)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(1024)).Write(0x18, BitConverter.GetBytes(-128)).Write(0x1C, BitConverter.GetBytes(0x20F0u)).Write(0x20, new byte[4]);
        fixture.Frame = PublishContext(fixture.Frame, temporary, context, 108);
        NativeBattleEffect207Frame result = NativeBattleEffect207Lower.Draw45E0(fixture.Frame, temporary, fixture.Resources, fixture.Inputs());
        NativeBattleEffect207DrawBatch draw = Assert.Single(fixture.Deliveries);
        Assert.Equal(allocation, draw.Batch.AllocationBytes);
        Assert.Equal(2, draw.Batch.Attempts.Length);
        NativeBattleGfGeometryAttempt mode = draw.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt body = draw.Batch.Attempts[1];
        Assert.Equal(modeOffset, mode.ByteOffsetRelative);
        Assert.Equal(0, body.ByteOffsetRelative);
        Assert.Equal(0xE1000220u, mode.Primitive!.Words[1]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, mode.Primitive.VertexPayload4);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, body.Primitive!.VertexPayload4);
        Assert.Equal(mode.Primitive.Bucket, body.Primitive.Bucket);
        Assert.DoesNotContain(true, Context(result, temporary, 108).ReadSnapshot(0x2C, 8).Known);
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(draw.Batch, 0, 4096, _ => 0xA5);
        NativeBattleEffectPacketWrite delivered = NativeBattleEffectPacketArena.Append(result.Memory, 0, NativeBattleLogicalOrdering.Clear(), resolved);
        Assert.Equal(allocation, delivered.Cursor);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Custom_near_rejection_retains_partial_body_without_mode_or_allocation(int group)
    {
        Fixture fixture = new();
        uint source = 0x1000000;
        NativeBattleStaticMeshResource mesh = Quad(source, group, 0);
        fixture.Resources = fixture.Resources with { Geometry = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(source, mesh), StaticFronts = fixture.Resources.StaticFronts.Add(source, mesh.Bytes) };
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 108);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(source + 12)).Write(0x1C, BitConverter.GetBytes(0x2030u)).Write(0x20, new byte[4]);
        fixture.Frame = PublishContext(fixture.Frame, temporary, context, 108);
        NativeBattleEffect207Frame result = NativeBattleEffect207Lower.Draw45E0(fixture.Frame, temporary, fixture.Resources, fixture.Inputs());
        NativeBattleGfGeometryBatch batch = Assert.Single(fixture.Deliveries).Batch;
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.DoesNotContain(true, Context(result, temporary, 108).ReadSnapshot(0x38, 4).Known);
    }

    [Fact]
    public void Warp_draws_color_then_texture_and_retains_outside_record_bits_and_padding()
    {
        Fixture fixture = new();
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(MutableMeshAddress + 34, 0) };
        fixture.Frame = NativeBattleEffect207Lower.Preprocess6070(fixture.Frame, MutableMeshAddress, fixture.Resources, fixture.Inputs());
        fixture.Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(fixture.Frame.Memory.GeometryRegisters!, Identity(-10, -20), true, true, null), Arena = fixture.Frame.Memory.Arena!.Write(0xEE0 + 8, [0x10, 0xAB, 0xCD, 0xEF]) } };
        NativeBattleEffect207Frame result = NativeBattleEffect207Lower.Draw5700(fixture.Frame, MutableMeshAddress, MutableMeshAddress + 8, 37, 1024, 0, fixture.Resources, fixture.Inputs());
        Assert.Equal(4, fixture.Deliveries.Count);
        Assert.Equal(0xE1000220u, fixture.Deliveries[0].Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x32, fixture.Deliveries[1].Batch.Attempts[0].Data[7]);
        Assert.Equal(0x3A, fixture.Deliveries[2].Batch.Attempts[0].Data[7]);
        Assert.Equal(0xE1000240u, fixture.Deliveries[3].Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(new byte[] { 0x15, 0xAB, 0xCD, 0xEF }, result.Memory.Arena!.Read(0xEE0 + 8, 4));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(0xEE0 + 4, 2)));
        Assert.Equal((short)-20, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(0xEE0 + 6, 2)));
        Assert.All(fixture.Deliveries.Skip(1).Take(2), operation => Assert.Equal((ushort)1, operation.Flags));
    }

    [Fact]
    public void Warp_uses_current_palette_rows_and_keeps_the_texture_padding_unwritten()
    {
        Fixture fixture = new();
        fixture.Frame = NativeBattleEffect207Lower.Preprocess6070(fixture.Frame, MutableMeshAddress, fixture.Resources, fixture.Inputs());
        fixture.Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(fixture.Frame.Memory.GeometryRegisters!, Identity(100, 100), true, true, null) } };
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(MutableMeshAddress + 14, 0) };
        NativeBattleEffect207Frame result = NativeBattleEffect207Lower.Draw5700(fixture.Frame, MutableMeshAddress, MutableMeshAddress + 8, 37, 0, 1, fixture.Resources, fixture.Inputs());
        Assert.Equal(6, fixture.Deliveries.Count);
        NativeBattleGfGeometryAttempt textured = fixture.Deliveries[4].Batch.Attempts[0];
        Assert.Equal(0x26, textured.Data[7]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[14]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[15]);
        NativeBattleGfGeometryBatch retained = NativeBattlePacketBatchWords.Resolve(fixture.Deliveries[4].Batch, 0, 4096, _ => 0xFF);
        NativeBattleGeometryPrimitive primitive = Assert.Single(retained.Attempts).Primitive!;
        Assert.Equal(0xFFFFu, primitive.Words[3] >> 16);
        Assert.Equal(0x119u, primitive.Words[5] >> 16);
        Assert.Equal(2u, (primitive.Words[5] >> 23) & 3);
        Assert.Empty(textured.Primitive!.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, fixture.Deliveries[0].Batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Bounds_use_actual_minimumY_and_truncate_negative_screen_coordinates_toward_zero()
    {
        Fixture fixture = new();
        NativeBattleEffectArena model = NativeBattleEffectArena.Clear().Write(0x1C, BitConverter.GetBytes((short)-35)).Write(0x1E, BitConverter.GetBytes((short)-35)).Write(0x20, BitConverter.GetBytes((short)4000)).Write(0x26, BitConverter.GetBytes((short)-256)).Write(0x36, BitConverter.GetBytes((short)477));
        fixture.Model = model;
        NativeBattleEffect207Inputs inputs = fixture.Inputs() with { ReadProjection = _ => new(null, 100, 0, 0) };
        NativeBattleEffect207BoundsStep result = NativeBattleEffect207Lower.Bounds5F10(fixture.Frame, 0x1D972C0, Rectangle(), 65536, fixture.Resources, inputs);
        Assert.True(result.Included);
        Assert.Equal((short)-7, unchecked((short)result.Frame.Memory.GeometryRegisters!.Sxy[2]!.Value));
        Assert.Equal((short)0, S(result.Rect8, 0));
        Assert.Equal((short)0, S(result.Rect8, 2));
        Assert.Equal(1000, result.MinimumDepth);
    }

    [Fact]
    public void Bounds_zero_depth_is_not_replaced_before_native_division()
    {
        Fixture fixture = new();
        fixture.Model = NativeBattleEffectArena.Clear();
        Assert.Throws<DivideByZeroException>(() => NativeBattleEffect207Lower.Bounds5F10(fixture.Frame, 0x1D972C0, Rectangle(), 65536, fixture.Resources, fixture.Inputs()));
    }

    [Fact]
    public void Particle_updates_after_draw_even_with_pause_flags_and_wraps_native_words()
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, 0x20DFAB8, 32, 64);
        (pool, _) = pool.Append(0x6A5650);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(12, 32767).WriteWord(0x12, 15).WriteWord(0x14, 1).WriteBytes(0x1A, [1, 0, 0]);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        fixture.Frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool] } };
        NativeBattleEffect207Step result = NativeBattleEffect207Lower.Advance(fixture.Frame, pool.BankAddress, fixture.Resources, fixture.Inputs() with { ReadFlags = _ => 0x201 });
        Assert.Single(fixture.Deliveries);
        Assert.Equal(unchecked((ushort)-32767), Read(result.Frame, pool.BankAddress).ReadWord(12));
        Assert.Equal((ushort)16, Read(result.Frame, pool.BankAddress).ReadWord(0x12));
        Assert.Equal(2, result.Result);
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Direction_helper_retains_unwritten_W_and_uses_three_native_word_stores()
    {
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(6, [0xA5]).MarkUnknown(7, 1);
        NativeBattleEffectArena result = NativeBattleEffect207Lower.Direction6020(0, 0, point);
        Assert.Equal((short)0, S(result, 0));
        Assert.Equal((short)0, S(result, 2));
        Assert.Equal((short)-4096, S(result, 4));
        Assert.Equal(0xA5, result.Read(6, 1)[0]);
        Assert.False(result.ReadSnapshot(7, 1).Known[0]);
    }

    [Fact]
    public void Current_preprocessed_mesh_and_unknown_temporary_suffix_survive_JSON()
    {
        Fixture fixture = new();
        NativeBattleEffect207Frame first = NativeBattleEffect207Lower.Preprocess6070(fixture.Frame, MutableMeshAddress, fixture.Resources, fixture.Inputs());
        NativeBattleEffect207Frame restored = JsonSerializer.Deserialize<NativeBattleEffect207Frame>(JsonSerializer.Serialize(first))!;
        Assert.Equal(first.State.Read(MutableMeshAddress, 116), restored.State.Read(MutableMeshAddress, 116));
        Assert.Equal(JsonSerializer.Serialize(first.Memory.GeometryRegisters), JsonSerializer.Serialize(restored.Memory.GeometryRegisters));
        Assert.False(restored.Staging!.ReadSnapshot(NativeBattleTemporaryMemory.Address + 0x57, 1).Known[0]);
    }

    private sealed class Fixture
    {
        public NativeBattleEffect207Frame Frame;
        public NativeBattleEffect207Resources Resources;
        public NativeBattleEffectArena Model = NativeBattleEffectArena.Clear();
        public List<NativeBattleEffect207DrawBatch> Deliveries = [];
        public Fixture()
        {
            NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess();
            NativeSoftwareDrawingEnvironmentBuffers buffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(new byte[92].ToImmutableArray());
            process = process with { Software = process.Software! with { EnvironmentBuffers = buffers } };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = process, GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(), true, true, null) with { Lzcs = 0x1234, Lzcr = 7, Mac0 = 0xABCDEF }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(Identity(), null) };
            Frame = new(memory, new(207, 206, new(207, Entry, Initializer), null, null) { Pools = [] }, new(123), NativeBattleEffect207State.FromColdProcess().Write(MutableMeshAddress, Raw()).WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(CursorGlobal, 0x20E14E4)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            byte[] sqrt = new byte[512];
            for (int offset = 0; offset < sqrt.Length; offset += 2)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sqrt.AsSpan(offset), 4096);
            }
            byte[] palette = new byte[8192];
            for (int offset = 0; offset < palette.Length; offset += 2)
            {
                BinaryPrimitives.WriteInt16LittleEndian(palette.AsSpan(offset), offset < 256 ? (short)4096 : (short)0);
            }
            Resources = new(new("fixture", []), new("fixture", []), new(ProgrammeAddress, []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, Sprite(), Raw().ToImmutableArray(), [], 0, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(SqrtTableAddress, sqrt.ToImmutableArray()).Add(ShapingTableAddress, palette.ToImmutableArray()));
        }
        public NativeBattleEffect207Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, null!, null!, (_, _) => Model, _ => NativeBattlePackedMatrix32.FromMatrix(Identity(0, 0, 2000), null), _ => new(null, 100, 160, 108), _ => 0, (_, _, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            NativeBattleEffect207DrawBatch draw = Assert.IsType<NativeBattleEffect207DrawBatch>(operation);
            Deliveries.Add(draw);
            return frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(CursorGlobal) + (uint)draw.Batch.AllocationBytes) };
        });
    }

    private static NativeBattleActionMatrix Identity(int x = 0, int y = 0, int z = 0) => NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = z };
    private static NativeBattleEffectArena Rectangle() => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32767)).Write(2, BitConverter.GetBytes((short)32767)).Write(4, BitConverter.GetBytes((short)-32767)).Write(6, BitConverter.GetBytes((short)-32767));
    private static short S(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.Read(offset, 2));
    private static NativeBattleModuleResource Sprite()
    {
        byte[] data = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), 16);
        for (int index = 0; index <= 16; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10 + index * 2), 44);
        }
        return new(0, SpriteAddress, 0, 0, data.ToImmutableArray());
    }
    private static byte[] Raw()
    {
        byte[] data = new byte[116];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 4);
        short[] xy = [0, 0, -400, 0, 400, 0, 400, 400];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10 + index * 8), xy[index * 2 + 1]);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12 + index * 8), 0xFFFF004D);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80), 1);
        Face(60, false);
        Face(84, true);
        return data;
        void Face(int offset, bool quad)
        {
            for (int index = 0; index < (quad ? 4 : 3); index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 4 + index * 2), (ushort)(index * 2));
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + (index == 0 ? 0 : 8 + index * 4)), 0x30000000u + (uint)(index + 1) * 0x101010);
            }
        }
    }
    private static NativeBattleStaticMeshResource Quad(uint address, int group, short z)
    {
        int polygon = 44;
        int size = group == 3 ? 24 : 36;
        byte[] data = new byte[polygon + 32 + size];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)polygon);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 1);
        short[] xy = [-200, -200, 200, -200, -200, 200, 200, 200];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 8), xy[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 8), z);
        }
        int face = polygon + group * 4 + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face), group == 3 ? 0x2E80A0C0u : 0x3E80A0C0u);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 24), 0x00102030);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 28), 0x00405060);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 32), 0x00708090);
        }
        return new(0, address, data.ToImmutableArray());
    }
}
