using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect290LowerTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(4)]
    public void Captured290_fragment_and_glow_preserve_native_rows_FIFOs_padding_and_each_accepted_pass(int life)
    {
        Fixture Build()
        {
            Fixture fixture = new();
            uint address = Fixture.Main + 0x1D8;
            NativeBattleEffectArena row = Put290(PutWord290(PutWord290(PutWord290(ReadBytes(fixture.Frame, address, 0x7C), 4, 2000), 6, life), 14, 4096), 24, 0x1067400);
            fixture.Frame = WriteBytes(fixture.Frame, address, row, 0x7C);
            return fixture;
        }
        Fixture nativeFixture = Build();
        Fixture capturedFixture = Build();
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108);
        var native = DrawFragments(nativeFixture.Frame, Fixture.Main, nativeFixture.Resources, nativeFixture.Inputs() with { ReadProjection = _ => projection });
        var captured = DrawFragments(capturedFixture.Frame, Fixture.Main, capturedFixture.Resources, capturedFixture.Inputs() with { ReadProjection = _ => projection, CapturePresentation = true });
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeFixture.Deliveries), NativeBattleTestSnapshots.Json(capturedFixture.Deliveries));
        var batch = Assert.Single(capturedFixture.Deliveries).Batch;
        var geometry = batch.Attempts.Select(attempt => attempt.Primitive).Where(primitive => primitive is not null && (byte)(primitive.Words[1] >> 24) is >= 0x20 and < 0x80).ToArray();
        Assert.NotEmpty(geometry);
        foreach (NativeBattleGeometryPrimitive? primitive in geometry)
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive!.PilotGeometry);
            Assert.Equal((ushort)290, visual.Identity.Effect);
            Assert.True(visual.Identity.Generation > 0);
            Assert.False(visual.ScreenSpace);
            Assert.True(visual.UseSourceDepths);
            Assert.Equal((float)primitive.VertexPayload4[2], visual.SourceDepths!.Value.Z);
        }
        Assert.All(batch.Attempts.Where(attempt => attempt.Primitive is { Words.Length: 2 }), attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
    }

    [Fact]
    public void Captured290_actual_smoke_reuse_rebinds_birth_without_changing_native_allocator_cursor_or_payload()
    {
        Fixture nativeFixture = new();
        Fixture capturedFixture = new();
        var native = NativeBattleEffect290Fragments.AllocateSmoke(nativeFixture.Frame, Fixture.Main);
        var captured = NativeBattleEffect290Fragments.AllocateSmoke(capturedFixture.Frame, Fixture.Main, true);
        Assert.Equal(native.Address, captured.Address);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Frame), NativeBattleTestSnapshots.Json(captured.Frame));
        var first = NativeBattleOffsetBodyVisuals.At(290, captured.Frame.Effect.Pools, captured.Address, SmokeSourceAddress);
        capturedFixture.Frame = WriteBytes(captured.Frame, Fixture.Main + 0x64DC, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(captured.Address)), 4);
        nativeFixture.Frame = WriteBytes(native.Frame, Fixture.Main + 0x64DC, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(native.Address)), 4);
        var nextNative = NativeBattleEffect290Fragments.AllocateSmoke(nativeFixture.Frame, Fixture.Main);
        var nextCaptured = NativeBattleEffect290Fragments.AllocateSmoke(capturedFixture.Frame, Fixture.Main, true);
        var second = NativeBattleOffsetBodyVisuals.At(290, nextCaptured.Frame.Effect.Pools, nextCaptured.Address, SmokeSourceAddress);
        Assert.Equal(captured.Address, nextCaptured.Address);
        Assert.Equal(first.Generation, second.Generation);
        Assert.NotEqual(first.ResourceGeneration, second.ResourceGeneration);
        Assert.Equal(NativeBattleTestSnapshots.Json(nextNative.Frame), NativeBattleTestSnapshots.Json(nextCaptured.Frame));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Physics_decrements_first_and_terminal_rows_skip_motion_and_publish_actual_hint(int life)
    {
        Fixture fixture = new();
        uint address = Fixture.Main + 0x1D8;
        NativeBattleEffectArena record = PutWord290(PutWord290(PutWord290(PutWord290(PutWord290(ReadBytes(fixture.Frame, address, 0x7C), 0, 100), 2, -100), 6, life), 8, 7), 10, 3);
        fixture.Frame = WriteBytes(fixture.Frame, address, record, 0x7C);
        NativeBattleEffect290Frame result = AdvanceFragments(fixture.Frame, Fixture.Main);
        NativeBattleEffectArena actual = ReadBytes(result, address, 0x7C);
        Assert.Equal(life == 0 ? 0 : life - 1, Word290(actual, 6));
        Assert.Equal(life == 2 ? 107 : 100, Word290(actual, 0));
        Assert.Equal(life == 2 ? -97 : -100, Word290(actual, 2));
        Assert.Equal(life == 2 ? 23 : 3, Word290(actual, 10));
        if (life == 1)
        {
            Assert.Equal(address, Dword290(ReadBytes(result, Fixture.Main + 0x64D8, 4), 0));
        }
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    [Fact]
    public void Bounce_uses_new_velocity_without_clamping_position_and_wraps_WORD_spin()
    {
        Fixture fixture = new();
        uint address = Fixture.Main + 0x1D8;
        NativeBattleEffectArena record = ReadBytes(fixture.Frame, address, 0x7C);
        record = PutWord290(PutWord290(PutWord290(PutWord290(PutWord290(PutWord290(record, 2, -1), 6, 3), 10, 10), 0x10, 32767), 0x14, 2), 0x16, -32768);
        fixture.Frame = WriteBytes(fixture.Frame, address, record, 0x7C);
        NativeBattleEffectArena actual = ReadBytes(AdvanceFragments(fixture.Frame, Fixture.Main), address, 0x7C);
        Assert.Equal(9, Word290(actual, 2));
        Assert.Equal(-15, Word290(actual, 10));
        Assert.Equal(-32767, Word290(actual, 0x10));
        Assert.Equal(-2, Word290(actual, 0x14));
        Assert.Equal(-32768, Word290(actual, 0x16));
    }

    [Theory]
    [InlineData(0x677550, 11, 12)]
    [InlineData(0x6775F0, 12, 13)]
    public void Child_sprite_draws_old_phase_then_moves_even_on_its_final_callback(uint callback, int phase, int terminal)
    {
        Fixture fixture = new(callback);
        NativeBattleModulePoolTask task = Read(fixture.Frame, Fixture.Main).WriteWord(12, 100).WriteWord(16, 5000).WriteWord(0x12, (ushort)phase).WriteBytes(0x14, [255, 2, 3]);
        fixture.Frame = Write(fixture.Frame, Fixture.Main, task);
        NativeBattleEffect290Step step = Advance(fixture.Frame, Fixture.Main, fixture.Resources, fixture.Inputs());
        task = Read(step.Frame, Fixture.Main);
        Assert.Equal(2, step.Result);
        Assert.Equal(terminal, task.ReadWord(0x12));
        Assert.Equal(99, task.ReadWord(12));
        Assert.Equal(2, task.ReadWord(14));
        Assert.Equal(5003, task.ReadWord(16));
        Assert.Single(fixture.Deliveries);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(phase, Word290(temporary, 4));
        Assert.Equal(0, Word290(temporary, 0x24));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Collector_zero_limit_still_projects_first_selected_group_and_consumes_gate_before_return()
    {
        Fixture fixture = new();
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 180);
        uint contextAddress = temporary + 32;
        NativeBattleEffectArena context = Put290(Put290(Put290(Put290(Context(fixture.Frame, contextAddress, 144), 0x10, 1), 0x14, 0), 0x18, 0), 0x24, 32767);
        fixture.Frame = PublishContext(fixture.Frame, contextAddress, context, 144);
        byte[] geometry = Geometry();
        NativeBattleEffect290Frame result = NativeBattleEffect290Fragments.Collect(fixture.Frame, (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1900000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 1)], Fixture.Scratch, contextAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(1, fixture.ProcessDraws);
        Assert.Equal(-1, unchecked((int)Dword290(Context(result, contextAddress, 144), 0x14)));
        Assert.Equal(new byte[2], result.Staging!.Read(Fixture.Scratch + 6, 2));
        Assert.Equal(100, result.Memory.ScriptedColorRegisters!.Mac[2]);
        Assert.Equal(fixture.Frame.State.ReadDword(InternalRandomGlobal), result.State.ReadDword(InternalRandomGlobal));
    }

    [Fact]
    public void Unselected_bone_writes_only_scratch_selector_without_reading_unknown_XYZ()
    {
        Fixture fixture = new();
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 180);
        uint contextAddress = temporary + 32;
        fixture.Frame = PublishContext(fixture.Frame, contextAddress, Put290(Put290(Context(fixture.Frame, contextAddress, 144), 0x10, 1), 0x14, 1), 144);
        byte[] geometry = Geometry();
        NativeBattleEffect290Frame result = NativeBattleEffect290Fragments.Collect(fixture.Frame, (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1900000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0)], Fixture.Scratch, contextAddress, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = NativeBattleTemporaryMemory.ReadView(result.Staging!, Fixture.Scratch, 8);
        Assert.All(row.ReadSnapshot(0, 6).Known, known => Assert.False(known));
        Assert.Equal(1, Word290(row, 6));
        Assert.Equal(0, fixture.ProcessDraws);
    }

    [Fact]
    public void Collector_keeps_material_holes_and_separate_process_and_internal_random_streams()
    {
        Fixture fixture = new();
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 180);
        uint contextAddress = temporary + 32;
        NativeBattleEffectArena context = Context(fixture.Frame, contextAddress, 144);
        foreach ((int offset, uint value) in new (int, uint)[] { (0x10, 1), (0x14, 1), (0x18, 0), (0x1C, 100), (0x20, 5000), (0x24, 32767), (0x30, Fixture.Main) })
        {
            context = Put290(context, offset, value);
        }
        fixture.Frame = PublishContext(fixture.Frame, contextAddress, context, 144);
        byte[] geometry = Geometry();
        NativeBattleEffect290Frame result = NativeBattleEffect290Fragments.Collect(fixture.Frame, (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1900000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 1)], Fixture.Scratch, contextAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(3, fixture.ProcessDraws);
        uint record = Fixture.Main + 0x1D8;
        Assert.Equal(16, Word290(ReadBytes(result, record, 0x7C), 6));
        Assert.Equal(7u, Dword290(Context(result, contextAddress, 144), 0x18));
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 }, ReadBytes(result, record + 0x1C, 8).Read(0, 8));
        Assert.Equal(new byte[] { 0x5A, 0xA5 }, ReadBytes(result, record + 0x26, 2).Read(0, 2));
        uint seed = 0;
        for (int index = 0; index < 5; index++)
        {
            seed = (seed * 125 + 14) & 0x7FFF;
        }
        Assert.Equal(seed, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    [Theory]
    [InlineData(4, 3, 60)]
    [InlineData(8, 1, 32)]
    public void Fragment_raster_keeps_actual_SZ_for_polygons_and_zero_payload_for_mode(int life, int accepted, int allocation)
    {
        Fixture fixture = new();
        uint recordAddress = Fixture.Main + 0x1D8;
        NativeBattleEffectArena row = Put290(PutWord290(PutWord290(PutWord290(ReadBytes(fixture.Frame, recordAddress, 0x7C), 4, 2000), 6, life), 14, 4096), 24, 0x1067400);
        fixture.Frame = WriteBytes(fixture.Frame, recordAddress, row, 0x7C);
        NativeBattleEffect290Frame result = DrawFragments(fixture.Frame, Fixture.Main, fixture.Resources, fixture.Inputs());
        NativeBattleEffect290DrawBatch batch = Assert.Single(fixture.Deliveries);
        Assert.Equal(allocation, batch.Batch.AllocationBytes);
        Assert.Equal(accepted, batch.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
        ImmutableArray<int> actualDepths = result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray();
        Assert.Equal(actualDepths, batch.Batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Batch.Attempts[0].WriteMask[30]);
        if (life < 8)
        {
            Assert.Equal(new[] { 0, 0, 0, 0 }, batch.Batch.Attempts[2].Primitive!.VertexPayload4);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void Zero_cross_uses_actual_trig_tail_WORD_and_x86_shift_before_native_division(ushort aliasWord, bool completes)
    {
        Fixture fixture = new();
        byte[] trig = fixture.Resources.StaticFronts[TrigonometryAddress].ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(trig.AsSpan(checked((int)(NormalizationAddress - 128u - TrigonometryAddress))), aliasWord);
        fixture.Resources = fixture.Resources with { StaticFronts = fixture.Resources.StaticFronts.SetItem(TrigonometryAddress, trig.ToImmutableArray()) };
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 180);
        uint contextAddress = temporary + 32;
        uint recordAddress = Fixture.Main + 0x1D8;
        NativeBattleEffectArena context = Context(fixture.Frame, contextAddress, 144);
        context = Put290(Put290(Put290(Put290(context, 0x1C, 100), 0x20, 5000), 0x28, recordAddress), 0x30, Fixture.Main);
        for (int corner = 0; corner < 3; corner++)
        {
            context = PutPoint(context, 0x34 + corner * 8, new(10, 20, 100));
        }
        fixture.Frame = PublishContext(fixture.Frame, contextAddress, context, 144);
        NativeBattleEffect290Inputs inputs = fixture.Inputs();
        if (!completes)
        {
            Assert.Throws<DivideByZeroException>(() => NativeBattleEffect290Fragments.Initialize6760A0(fixture.Frame, contextAddress, fixture.Resources, inputs));
            Assert.Equal(1, fixture.ProcessDraws);
            return;
        }
        uint seed = fixture.Frame.State.ReadDword(InternalRandomGlobal);
        NativeBattleEffect290Frame result = NativeBattleEffect290Fragments.Initialize6760A0(fixture.Frame, contextAddress, fixture.Resources, inputs);
        Assert.Equal(2, fixture.ProcessDraws);
        Assert.Equal(32u, result.Memory.GeometryRegisters!.Lzcr);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Memory.ScriptedColorRegisters!.Mac);
        NativeBattleEffectArena record = ReadBytes(result, recordAddress, 0x7C);
        Assert.Equal(0, Word290(record, 8));
        Assert.Equal(-200, Word290(record, 10));
        Assert.Equal(0, Word290(record, 12));
        NativeBattleEffectArena smoke = ReadBytes(result, Fixture.Main + 0x5ED8, 12);
        Assert.Equal(new byte[] { 0, 0, 0 }, smoke.Read(8, 3));
        for (int index = 0; index < 4; index++)
        {
            seed = (seed * 125 + 14) & 0x7FFF;
            int expected = index < 2 ? (int)seed : ((int)seed >> 5) - 512;
            Assert.Equal(unchecked((short)expected), Word290(record, 0x10 + index * 2));
        }
        Assert.Equal(seed, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    private static byte[] Geometry()
    {
        byte[] data = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 3);
        short[] points = [0, 0, 100, 128, 0, 100, 0, 128, 100];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(46), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(48), 2);
        new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 }.CopyTo(data, 52);
        return data;
    }

    internal sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public const uint Main = Arena + 0x1CDC;
        public const uint Scratch = 0x1900000;
        public NativeBattleEffect290Frame Frame;
        public NativeBattleEffect290Resources Resources;
        public int ProcessDraws;
        public List<NativeBattleEffect290DrawBatch> Deliveries = [];

        public Fixture(uint callback = 0x6767B0)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, Main, 0x68E4, 1);
            (pool, int slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[pool.Stride - 12]).WriteDword(0x64D8, Main + 0x1D8).WriteDword(0x64DC, Main + 0x5ED8));
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sz = [11, 22, 33, 77] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            Frame = new(memory, new(290, 289, new(290, 0x675280, 0x675290), null, null) { Pools = [pool] }, new(42), NativeBattleEffect290State.FromColdProcess().WriteDword(ArenaGlobal, Arena)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            Frame = WriteBytes(Frame, Main + 0x1D8 + 0x26, NativeBattleEffectArena.Clear().Write(0, [0x5A, 0xA5]), 2);
            byte[] normal = new byte[384];
            for (int index = 64; index < 256; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan((index - 64) * 2), (ushort)(Math.Sqrt(index) * 512));
            }
            byte[] trig = new byte[16384];
            BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(2), 4096);
            byte[] mesh = new byte[100];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 56);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 3);
            short[] xyz = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0];
            for (int index = 0; index < xyz.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), xyz[index]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(56), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(66), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(68), 4);
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty;
            foreach ((uint address, int count) in new (uint, int)[] { (FirstSpriteAddress, 12), (SecondSpriteAddress, 13), (MainSpriteAddress, 16) })
            {
                byte[] sprite = new byte[10 + (count + 1) * 2 + 4];
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), (ushort)count);
                for (int index = 0; index < count; index++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), (ushort)(sprite.Length - 4));
                }
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + count * 2), 0xFFFF);
                sprites = sprites.Add(address, new(5, address, (uint)sprite.Length, 0, sprite.ToImmutableArray()));
            }
            Resources = new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x1067400, new(0, 0x1067400, mesh.ToImmutableArray())), sprites, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(NormalizationAddress, normal.ToImmutableArray()).Add(TrigonometryAddress, trig.ToImmutableArray()));
        }

        public NativeBattleEffect290Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, null!, null!, frame =>
        {
            ProcessDraws++;
            return new(frame, 0);
        }, (frame, operation) =>
        {
            Deliveries.Add(Assert.IsType<NativeBattleEffect290DrawBatch>(operation));
            return frame;
        });
    }
}
