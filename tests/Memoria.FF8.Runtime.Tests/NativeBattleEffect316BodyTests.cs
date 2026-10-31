using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect316Body;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect316BodyTests
{
    [Fact]
    public void Projection_helpers_preserve_distinct_packed_depth_and_padding_footprints()
    {
        Fixture fixture = new();
        uint raw = Fixture.Base + 0x2000;
        fixture.Store(raw, PutWord(PutWord(NativeBattleEffectArena.Clear(), 0, 1), 6, 4000), 8);
        foreach (bool expanded in new[] { false, true })
        {
            uint context = Fixture.Base + (expanded ? 0x300u : 0x100u);
            uint output = Fixture.Base + (expanded ? 0x700u : 0x500u);
            int cursor = expanded ? 0x74 : 0x54;
            fixture.Store(context, PutDword(PutDword(NativeBattleEffectArena.Clear(), cursor, raw), cursor + 4, output), expanded ? 0x90 : 0x64);
            fixture.Store(output, NativeBattleEffectArena.Clear().Write(7, [0xA5]).MarkUnknown(expanded ? 14 : 6, expanded ? 2 : 1), expanded ? 16 : 8);
            fixture.Frame = expanded ? ProjectGroup662630(fixture.Frame, fixture.Inputs(), context) : ProjectGroup663760(fixture.Frame, fixture.Inputs(), context);
            NativeBattleEffectArena row = fixture.Read(output, expanded ? 16 : 8);
            Assert.Equal(1000, Word(row, 4));
            Assert.Equal(64, row.Read(6, 1)[0]);
            Assert.Equal(expanded ? (byte)0xA5 : (byte)0, row.Read(7, 1)[0]);
            if (expanded)
            {
                Assert.Equal(4000, Word(row, 12));
                Assert.DoesNotContain(true, row.ReadSnapshot(14, 2).Known);
            }
            Assert.Equal(raw + 8, Dword(fixture.Read(context, 0x90), cursor));
            Assert.Equal(output + (expanded ? 16u : 8u), Dword(fixture.Read(context, 0x90), cursor + 4));
        }
    }

    [Fact]
    public void Packed8_failed_projection_only_clears_depth_WORD_after_the_CPU_XYZ_write()
    {
        Fixture fixture = new();
        uint context = Fixture.Base + 0x100;
        uint output = Fixture.Base + 0x500;
        uint raw = Fixture.Base + 0x2000;
        fixture.Store(raw, PutWord(PutWord(PutWord(PutWord(NativeBattleEffectArena.Clear(), 0, 1), 2, 123), 4, 456), 6, 0), 8);
        fixture.Store(context, PutDword(PutDword(NativeBattleEffectArena.Clear(), 0x54, raw), 0x58, output), 0x64);
        fixture.Store(output, NativeBattleEffectArena.Clear().Write(6, [0x55, 0xA5]), 8);
        fixture.Frame = ProjectGroup663760(fixture.Frame, fixture.Inputs(), context);
        NativeBattleEffectArena row = fixture.Read(output, 8);
        Assert.Equal(123, Word(row, 0));
        Assert.Equal(456, Word(row, 2));
        Assert.Equal(0, Word(row, 4));
        Assert.Equal(new byte[] { 0x55, 0xA5 }, row.Read(6, 2));
    }

    [Fact]
    public void Quad_uniform_classification_uses_three_flags_in_packed8_and_all_four_in_packed16()
    {
        Assert.True(UniformGray(new byte[] { 128, 128, 128, 64 }, false));
        Assert.False(UniformGray(new byte[] { 128, 128, 128, 64 }, true));
        Assert.True(UniformGray(new byte[] { 128, 128, 128, 128 }, true));
        Assert.Equal((byte)0, Gray(short.MinValue, short.MaxValue));
        Assert.Equal((byte)128, Gray(1000, 0));
    }

    [Theory]
    [InlineData(false, 1, 0x2C)]
    [InlineData(true, 3, 0x3E)]
    public void Quad_fourth_gray_changes_only_the_packed16_packet_path(bool expanded, int packets, byte opcode)
    {
        Fixture fixture = new();
        uint contextAddress = Fixture.Base + 0x1000;
        uint vertices = Fixture.Base + 0x2000;
        uint faceAddress = Fixture.Base + 0x3000;
        int stride = expanded ? 16 : 8;
        uint[] screens = [0, 10, 10u << 16, (10u << 16) | 10];
        for (int corner = 0; corner < 4; corner++)
        {
            NativeBattleEffectArena row = PutWord(PutDword(NativeBattleEffectArena.Clear(), 0, screens[corner]), 4, 1000).Write(6, [corner == 3 ? (byte)64 : (byte)128]);
            fixture.Store(vertices + (uint)(corner * stride), row, stride);
        }
        NativeBattleEffectArena face = NativeBattleEffectArena.Clear();
        for (int corner = 0; corner < 4; corner++)
        {
            face = PutWord(face, corner * 2, corner);
        }
        fixture.Store(faceAddress, face, 20);
        NativeBattleEffectArena context = PutWord(PutDword(PutDword(NativeBattleEffectArena.Clear(), 0, faceAddress), 4, vertices), 10, 1);
        int cursor = expanded ? 0x88 : 0x5C;
        context = PutDword(PutDword(context, cursor, Fixture.Base + 0x40000), cursor + 4, Fixture.Base + 0x50000);
        if (expanded)
        {
            context = PutDword(context, 0x84, Fixture.Base);
            fixture.Store(Fixture.Base + 0x818, PutDword(NativeBattleEffectArena.Clear(), 0, Fixture.Base + 0x18), 4);
            for (int index = 0; index < 128; index++)
            {
                fixture.Store(Fixture.Base + 0x18 + (uint)(index * 16), PutWord(NativeBattleEffectArena.Clear(), 6, 1), 16);
            }
        }
        fixture.Store(contextAddress, context, expanded ? 0x90 : 0x64);
        NativeBattleEffect316Frame result = expanded ? Raster661EB0(fixture.Frame, null!, fixture.Inputs(), contextAddress, 17, 0) : Raster661720(fixture.Frame, null!, fixture.Inputs(), contextAddress, 17, 0);
        Assert.Equal(packets, fixture.Draws.Count);
        Assert.Equal(opcode, fixture.Draws[0].Batch.Attempts[0].Data[7]);
        Assert.Equal(expanded ? 14u : 0u, result.State.ReadDword(0x2505568));
        Assert.Equal(0x1D8E054u, fixture.Draws[0].CursorGlobal);
        if (expanded)
        {
            Assert.Equal(0x2505564u, fixture.Draws[1].CursorGlobal);
            Assert.Equal(0x3A, fixture.Draws[1].Batch.Attempts[0].Data[7]);
            Assert.Equal(8, fixture.Draws[2].Batch.AllocationBytes);
            Assert.Equal(0xE10002C0u, BitConverter.ToUInt32(fixture.Draws[2].Batch.Attempts[0].Data.AsSpan().Slice(4)));
        }
    }

    [Fact]
    public void Full_record_pool_consumes_no_birth_RNG_and_never_reads_the_unused_point()
    {
        Fixture fixture = new();
        uint main = Fixture.Base;
        fixture.Store(main + 0x818, PutDword(NativeBattleEffectArena.Clear(), 0, main + 0x18), 4);
        for (int index = 0; index < 128; index++)
        {
            fixture.Store(main + 0x18 + (uint)(index * 16), PutWord(NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 6, 1), 16);
        }
        NativeBattleEffect316Frame result = Birth662590(fixture.Frame, fixture.Inputs(), main, null!);
        Assert.Same(fixture.Frame, result);
    }

    [Fact]
    public void Birth_prefers_free_hint_consumes_four_private_draws_and_preserves_gaps()
    {
        Fixture fixture = new();
        uint main = Fixture.Base;
        uint hint = main + 0x18 + 5 * 16;
        fixture.Store(main + 0x818, PutDword(NativeBattleEffectArena.Clear(), 0, hint), 4);
        fixture.Store(hint, NativeBattleEffectArena.Clear().MarkUnknown(10, 2).MarkUnknown(15, 1), 16);
        NativeBattleEffect316Frame expected = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame));
        expected = NextRandom662570(expected, out int size);
        expected = NextRandom662570(expected, out int vx);
        expected = NextRandom662570(expected, out int vy);
        expected = NextRandom662570(expected, out int vz);
        fixture.Frame = Birth662590(fixture.Frame, fixture.Inputs(), main, PutWord(NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 4, 3000));
        NativeBattleEffectArena row = fixture.Read(hint, 16);
        Assert.Equal(11, Word(row, 6));
        Assert.Equal(3000, Word(row, 4));
        Assert.Equal(1024 + (size >> 12), Word(row, 8));
        Assert.Equal(unchecked((byte)((vx >> 13) - 2)), row.Read(12, 1)[0]);
        Assert.Equal(unchecked((byte)(-3 - (vy >> 11))), row.Read(13, 1)[0]);
        Assert.Equal(unchecked((byte)((vz >> 13) - 2)), row.Read(14, 1)[0]);
        Assert.Equal(expected.State.ReadDword(0x2505568), fixture.Frame.State.ReadDword(0x2505568));
        Assert.Equal(expected.Random, fixture.Frame.Random);
        Assert.Equal(hint + 16, Dword(fixture.Read(main + 0x818, 4), 0));
        Assert.Equal(0, Word(fixture.Read(main + 0x18, 8), 6));
        Assert.DoesNotContain(true, row.ReadSnapshot(10, 2).Known);
        Assert.DoesNotContain(true, row.ReadSnapshot(15, 1).Known);
    }

    [Fact]
    public void Private_rng_wraps_DWORD_before_the_15bit_mask_and_leaves_CRT_alone()
    {
        Fixture fixture = new();
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x2505568, 0xFFFFFFFE) };
        NativeBattleEffect316Frame result = NextRandom662570(fixture.Frame, out int value);
        Assert.Equal(unchecked(0xFFFFFFFEu * 125 + 14) & 0x7FFF, (uint)value);
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    [Fact]
    public void Terminal_record_draws_the_actual_two_quad_prefix_then_moves_once()
    {
        Fixture fixture = new();
        uint row = Fixture.Base + 0x18;
        fixture.Store(row, PutWord(PutWord(PutWord(NativeBattleEffectArena.Clear(), 4, 2000), 6, 1), 8, 4096).Write(12, [0xFF, 2, 1]), 16);
        fixture.Frame = AdvanceRecords662720(fixture.Frame, fixture.Resources(), fixture.Inputs(), Fixture.Base);
        NativeBattleEffect316DrawBatch draw = Assert.Single(fixture.Draws);
        Assert.Equal(80, draw.Batch.AllocationBytes);
        Assert.All(draw.Batch.Attempts, attempt => Assert.Equal(19, attempt.Primitive!.Bucket));
        Assert.Equal(0x2505564u, draw.CursorGlobal);
        Assert.Equal(0, Word(fixture.Read(row, 16), 6));
        Assert.Equal(-4, Word(fixture.Read(row, 16), 0));
        Assert.Equal(8, Word(fixture.Read(row, 16), 2));
        Assert.Equal(2004, Word(fixture.Read(row, 16), 4));
        Assert.DoesNotContain(true, fixture.Read(Fixture.Base + 0x9000 + 0x96, 2).ReadSnapshot(0, 2).Known);
    }

    [Fact]
    public void Empty_record_scan_demands_only_life_and_preserves_unwritten_temporary_bytes()
    {
        Fixture fixture = new();
        fixture.Store(Fixture.Base + 0x9000, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 180);
        for (int index = 0; index < 128; index++)
        {
            fixture.Store(Fixture.Base + 0x18 + (uint)(index * 16), PutWord(NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 6, 0), 16);
        }
        NativeBattleEffect316Frame result = AdvanceRecords662720(fixture.Frame, null!, fixture.Inputs(), Fixture.Base);
        Assert.Equal(fixture.Frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Empty(fixture.Draws);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(result.Memory.Arena!, 0x9000, NativeBattleEffectArena.Clear(), 0, 180);
        Assert.Equal(0, Word(temporary, 0x48));
        Assert.Equal(0, Word(temporary, 0x86));
        Assert.DoesNotContain(true, temporary.ReadSnapshot(0x84, 2).Known);
        Assert.DoesNotContain(true, temporary.ReadSnapshot(0x96, 2).Known);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Captured_body_keeps_native_projection_scratch_and_accepted_deformed_world_quarter_depth(bool expanded)
    {
        Fixture Build(bool capture)
        {
            Fixture fixture = new();
            fixture.View = NativeBattleCuragaMatrices.Identity8DD770();
            uint raw = Fixture.Base + 0x2000;
            uint context = Fixture.Base + 0x1000;
            uint output = Fixture.Base + 0x3000;
            uint face = Fixture.Base + 0x4000;
            int cursor = expanded ? 0x74 : 0x54;
            int stride = expanded ? 16 : 8;
            NativeBattleEffectArena points = PutWord(NativeBattleEffectArena.Clear(), 0, 3);
            for (int index = 0; index < 3; index++)
            {
                points = PutWord(PutWord(points, 2 + index * 6, index == 0 ? -20 : index == 1 ? 20 : 0), 6 + index * 6, 4000);
            }
            fixture.Store(raw, points, 20);
            fixture.Store(output, NativeBattleEffectArena.Clear().MarkUnknown(0, stride * 3), stride * 3);
            fixture.Store(context, PutDword(PutDword(NativeBattleEffectArena.Clear(), cursor, raw), cursor + 4, output), expanded ? 0x90 : 0x64);
            fixture.Store(context, PutWord(fixture.Read(context, expanded ? 0x90 : 0x64), expanded ? 0x6C : 0x4C, -256), expanded ? 0x90 : 0x64);
            if (capture)
            {
                fixture.Frame = fixture.Frame with { CapturePresentation = true, PilotIdentity = new(316, 0x2505510, 0, 55, 1) };
            }
            fixture.Frame = expanded ? ProjectGroup662630(fixture.Frame, fixture.Inputs(), context) : ProjectGroup663760(fixture.Frame, fixture.Inputs(), context);
            NativeBattleEffectArena faceBytes = PutWord(PutWord(NativeBattleEffectArena.Clear(), 2, 1), 4, 2);
            fixture.Store(face, faceBytes, 16);
            NativeBattleEffectArena raster = PutDword(PutDword(PutWord(PutDword(NativeBattleEffectArena.Clear(), 0, face), 8, 1), 4, output), 0x14, 0x112233);
            fixture.Store(context, raster, expanded ? 0x90 : 0x64);
            fixture.Frame = expanded ? Raster661EB0(fixture.Frame, null!, fixture.Inputs(), context, 17, 0) : Raster661720(fixture.Frame, null!, fixture.Inputs(), context, 17, 0);
            return fixture;
        }
        Fixture plain = Build(false);
        string expected = System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain.Frame));
        string packets = System.Text.Json.JsonSerializer.Serialize(plain.Draws);
        Fixture captured = Build(true);
        Assert.Equal(expected, System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        Assert.Equal(packets, System.Text.Json.JsonSerializer.Serialize(captured.Draws));
        var primitive = Assert.Single(Assert.Single(captured.Draws).Batch.Attempts).Primitive!;
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-20, 0, 4000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(0, 0, 4000), visual.C);
        Assert.Equal(4, visual.DepthDivisor);
        Assert.Equal(new System.Numerics.Vector4(1000, 1000, 1000, 0), visual.SourceDepths!.Value);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.UseSourceDepths);
    }

    private sealed class Fixture
    {
        public const uint Base = 0x20DFAB8;
        public NativeBattleEffect316Frame Frame;
        public NativeBattleActionMatrix? View;
        public List<NativeBattleEffect316DrawBatch> Draws = [];

        public Fixture()
        {
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, new(316, 315, new(316, 0x6610B0, 0x6610C0), null, null), new(42), NativeBattleEffect316State.FromColdProcess().WriteDword(0x2505564, Base + 0x1881C));
            Store(Base + 0x9000, NativeBattleEffectArena.Clear().MarkUnknown(0x96, 2).MarkUnknown(0x76, 2).MarkUnknown(0x4A, 2).MarkUnknown(0x52, 2).MarkUnknown(0x5A, 2).MarkUnknown(0x62, 2), 180);
        }

        public NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Frame.Memory.Arena!, checked((int)(address - Base)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        public void Store(uint address, NativeBattleEffectArena data, int length) => Frame = WriteArena(Frame, address, data, length);
        private static NativeBattleEffect316Frame WriteArena(NativeBattleEffect316Frame frame, uint address, NativeBattleEffectArena data, int length) => frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(data, 0, frame.Memory.Arena!, checked((int)(address - Base)), length) } };

        public NativeBattleEffect316Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(View, 100, 160, 108), _ => 0, (frame, address, length) => NativeBattleEffectNodeContextMemory.Copy(frame.Memory.Arena!, checked((int)(address - Base)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length), null!, null!, (frame, _) => new(frame, Base + 0x9000), (frame, _) => frame, (frame, operation) =>
        {
            switch (operation)
            {
                case NativeBattleEffect316WriteArena write:
                    return WriteArena(frame, write.Address, write.Data, write.Length);
                case NativeBattleEffect316DrawBatch draw:
                    Draws.Add(draw);
                    if (draw.CursorGlobal == 0x1D8E054)
                    {
                        return frame;
                    }
                    return frame with { State = frame.State.WriteDword(draw.CursorGlobal, frame.State.ReadDword(draw.CursorGlobal) + (uint)draw.Batch.AllocationBytes) };
                default:
                    throw new NotSupportedException();
            }
        });

        public NativeBattleEffect316Resources Resources()
        {
            byte[] bytes = new byte[364];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 12);
            for (int index = 1; index < 12; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + index * 2), 36);
            }
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), 2);
            for (int index = 0; index < 2; index++)
            {
                int offset = 40 + index * 20;
                bytes[offset] = 8;
                bytes[offset + 1] = 128;
                bytes[offset + 2] = 8;
                bytes[offset + 3] = 0x2C;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 16), 4096);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 18), 4096);
            }
            return new(null!, [], null!, null!, 0, new(5, 0x1005AE4, checked((uint)bytes.Length), 0, bytes.ToImmutableArray()));
        }
    }
}
