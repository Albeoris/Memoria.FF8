using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69ParticleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Debris_capture_preserves_final_OTZ_scratch_and_native_state_against_an_independent_baseline(bool quad)
    {
        NativeBattleEffect69Frame baselineInput = NativeBattleTestSnapshots.Independent(DebrisFrame(quad));
        NativeBattleGfGeometryBatch? baselineBatch = null;
        NativeBattleEffect69Inputs baselineInputs = Inputs((current, operation) =>
        {
            baselineBatch = Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation).Batch;
            return current;
        });
        NativeBattleEffect69Frame baseline = NativeBattleEffect69Particles.DrawDebris6FF2F0(baselineInput, baselineInputs);
        string expectedNativeState = NativeBattleTestSnapshots.Json(baseline);
        string expectedPackets = NativeBattleTestSnapshots.Json(baselineBatch);
        (byte[] expectedScratch, bool[] expectedKnown) = baseline.Staging!.ReadSnapshot(NativeBattleTemporaryMemory.Address, 8);
        Assert.Equal(quad ? 500u : 499u, BinaryPrimitives.ReadUInt32LittleEndian(expectedScratch));

        NativeBattleEffect69Frame capturedInput = NativeBattleTestSnapshots.Independent(DebrisFrame(quad));
        Assert.NotSame(baselineInput.Memory.Arena, capturedInput.Memory.Arena);
        Assert.NotSame(baselineInput.Staging, capturedInput.Staging);
        NativeBattleGfGeometryBatch? capturedBatch = null;
        NativeBattleEffect69Inputs capturedInputs = Inputs((current, operation) =>
        {
            capturedBatch = Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation).Batch;
            return current;
        }) with { CapturePresentation = true };
        NativeBattleEffect69Frame captured = NativeBattleEffect69Particles.DrawDebris6FF2F0(capturedInput, capturedInputs);
        Assert.Equal(expectedNativeState, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(expectedPackets, NativeBattleTestSnapshots.Json(capturedBatch));
        (byte[] capturedScratch, bool[] capturedKnown) = captured.Staging!.ReadSnapshot(NativeBattleTemporaryMemory.Address, 8);
        Assert.Equal(expectedScratch, capturedScratch);
        Assert.Equal(expectedKnown, capturedKnown);
        Assert.Equal(quad ? 500u : 499u, BinaryPrimitives.ReadUInt32LittleEndian(capturedScratch));
        NativeBattlePilotVisualGeometry geometry = Assert.Single(capturedBatch!.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
        Assert.Equal(new System.Numerics.Vector4(2000, 2000, 2000, quad ? 2000 : 0), geometry.SourceDepths);
    }

    [Fact]
    public void Spark_initialization_preserves_records_except_life_and_sets_actual_free_cursor()
    {
        NativeBattleEffect69Frame frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x8020, Enumerable.Repeat((byte)0xA5, 0x800).ToArray()) } };
        frame = NativeBattleEffect69Particles.InitializeSparks6FE5A0(frame);
        Assert.Equal(0x20E7AD8u, frame.State.ReadDword(SparkCursorGlobal));
        for (int index = 0; index < 128; index++)
        {
            byte[] row = frame.Memory.Arena!.Read(0x8020 + index * 16, 16);
            Assert.All(row.Take(15), value => Assert.Equal((byte)0xA5, value));
            Assert.Equal((byte)0, row[15]);
        }
    }

    [Fact]
    public void Last_spark_tick_still_moves_with_signed_byte_velocities_and_word_wrap()
    {
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x8024, BitConverter.GetBytes((short)32767)).Write(0x8026, BitConverter.GetBytes((short)-32768)).Write(0x8028, BitConverter.GetBytes((short)10)).Write(0x802C, [127, 128, 255, 1]);
        frame = NativeBattleEffect69Particles.AdvanceSparks700370(frame with { Memory = frame.Memory with { Arena = arena } });
        Assert.Equal(unchecked((short)(32767 + 508)), Word(frame, 0x8024));
        Assert.Equal(unchecked((short)(-32768 - 512)), Word(frame, 0x8026));
        Assert.Equal((short)6, Word(frame, 0x8028));
        Assert.Equal((byte)0, frame.Memory.Arena!.Read(0x802F, 1)[0]);
        Assert.Equal((byte)127, frame.Memory.Arena.Read(0x802C, 1)[0]);
    }

    [Fact]
    public void Debris_initialization_advances_shared_random_256_times_and_preserves_matrix_padding()
    {
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(6, BitConverter.GetBytes((short)2)).Write(8, BitConverter.GetBytes((short)123));
        for (int index = 0; index < 64; index++)
        {
            arena = arena.MarkUnknown(0x5550 + index * 20 + 18, 2);
        }
        frame = NativeBattleEffect69Particles.InitializeDebris6FE250(frame with { Memory = frame.Memory with { Arena = arena } });
        NativeCrtRandomState expected = NativeCrtRandomState.FromColdProcess();
        for (int index = 0; index < 256; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal((short)0, Word(frame, 6));
        Assert.Equal((short)123, Word(frame, 8));
        for (int index = 0; index < 64; index++)
        {
            Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(0x5550 + index * 20 + 18, 2));
        }
    }

    [Fact]
    public void Debris_exact_zero_lifetime_retires_without_reading_rotation_or_moving_center()
    {
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0, BitConverter.GetBytes((short)99)).Write(6, BitConverter.GetBytes((short)2)).Write(14, BitConverter.GetBytes((short)1)).MarkUnknown(16, 4);
        frame = NativeBattleEffect69Particles.AdvanceDebris6FF5F0(frame with { Memory = frame.Memory with { Arena = arena } });
        Assert.Equal((short)0, Word(frame, 6));
        Assert.Equal((short)0, Word(frame, 14));
        Assert.Equal((short)99, Word(frame, 0));
        Assert.Equal(0x20DFAB8u, frame.State.ReadDword(DebrisCursorGlobal));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(16, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Spark_birth_consumes_initial_angle_then_four_draws_only_for_each_successful_allocation(int free)
    {
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 128; index++)
        {
            arena = arena.Write(0x802F + index * 16, [18]);
        }
        if (free != 0)
        {
            arena = arena.Write(0x802F, [0]);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State.WriteDword(CasterGlobal, 0x1D972C0 + 4 * 156).WriteDword(SparkCursorGlobal, 0x20DFAB8 + 0x8020) };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort angle);
        ushort radius = 0;
        ushort x = 0;
        ushort z = 0;
        ushort y = 0;
        if (free != 0)
        {
            expected = expected.Draw(out radius).Draw(out x).Draw(out z).Draw(out y);
        }
        int modelReads = 0;
        NativeBattleEffect69Inputs inputs = Inputs() with { ReadModel = (current, actor) =>
        {
            modelReads++;
            Assert.Equal(4, actor);
            Assert.Equal(frame.Random.Draw(out _).Draw(out _), current.Random);
            return new(4, 3) { Transform = new(new(0, 0, 0), new(100, 0, -200), 0, 0, 4096) };
        } };
        NativeBattleEffect69Frame result = NativeBattleEffect69Particles.BirthSparks(frame, Resources(), inputs);
        Assert.Equal(expected, result.Random);
        Assert.Equal(free, modelReads);
        Assert.Equal(0x20DFAB8u + 0x8020u + (uint)free * 16, result.State.ReadDword(SparkCursorGlobal));
        if (free != 0)
        {
            Assert.Equal(unchecked((short)(100 + NativeBattleCameraMath.Sine(angle) * (radius & 2047) / 4096)), Word(result, 0x8024));
            Assert.Equal(unchecked((short)(-200 + NativeBattleCameraMath.Cosine(angle) * (radius & 2047) / 4096)), Word(result, 0x8028));
            Assert.Equal(unchecked((byte)((x & 3) - 2)), result.Memory.Arena!.Read(0x802C, 1)[0]);
            Assert.Equal(unchecked((byte)((z & 3) - 2)), result.Memory.Arena.Read(0x802E, 1)[0]);
            Assert.Equal((byte)(240 - (y & 31)), result.Memory.Arena.Read(0x802D, 1)[0]);
            Assert.Equal((byte)18, result.Memory.Arena.Read(0x802F, 1)[0]);
        }
    }

    [Theory]
    [InlineData(-120, -100)]
    [InlineData(-200, -170)]
    [InlineData(32760, -32746)]
    public void Debris_triangle_motion_rotates_all_four_deltas_and_wraps_vertical_plus_thirty_before_signed_clamp(short vertical, short expected)
    {
        NativeBattleEffect69Frame frame = MotionFrame(1, vertical);
        NativeBattleEffect69Frame result = NativeBattleEffect69Particles.AdvanceDebris6FF5F0(frame);
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.Equal((short)(corner + 1), Word(result, 20 + corner * 6));
        }
        Assert.Equal(expected, Word(result, 10));
        Assert.Equal((short)1, Word(result, 6));
        Assert.Equal((short)1, Word(result, 14));
        Assert.Equal(unchecked((short)(99 + 2)), Word(result, 0));
        Assert.Equal(unchecked((short)(100 + vertical)), Word(result, 2));
        Assert.Equal((short)96, Word(result, 4));
        Assert.Equal(new int?[] { 4, 0, 0 }, result.Memory.ScriptedColorRegisters!.Ir.ToArray());
        Assert.Equal((byte)0, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.Equal((byte)0, result.Memory.ScriptedColorRegisters.NormalInput.Value[7]);
    }

    [Fact]
    public void First_zero_delta_retires_after_second_GTE_transform_without_consuming_later_deltas()
    {
        NativeBattleEffect69Frame frame = MotionFrame(2, -200);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x5550, new byte[18]).MarkUnknown(32, 12);
        NativeBattleEffect69Frame result = NativeBattleEffect69Particles.AdvanceDebris6FF5F0(frame with { Memory = frame.Memory with { Arena = arena } });
        Assert.Equal((short)0, Word(result, 6));
        Assert.Equal((short)1, Word(result, 14));
        Assert.Equal((short)99, Word(result, 0));
        Assert.Equal(0x20DFAB8u, result.State.ReadDword(DebrisCursorGlobal));
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Memory.ScriptedColorRegisters!.Ir.ToArray());
        Assert.Equal((short)4, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan()));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(32, 6));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Debris_drawing_keeps_UV_tail_masks_actual_FIFO_and_full_OTZ_temporary_with_partial_W(bool quad)
    {
        NativeBattleEffect69Frame frame = DebrisFrame(quad);
        NativeBattleGfGeometryBatch? batch = null;
        NativeBattleEffect69Inputs inputs = Inputs((current, operation) =>
        {
            batch = Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation).Batch;
            return current;
        });
        NativeBattleEffect69Frame result = NativeBattleEffect69Particles.DrawDebris6FF2F0(frame, inputs);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch!.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(quad ? 40 : 32, batch.AllocationBytes);
        Assert.Equal(quad ? 0x2CF0F0F0u : 0x24F0F0F0u, attempt.Primitive!.Words[1]);
        Assert.Equal(0x12345678u, attempt.Primitive.Words[3]);
        Assert.Equal(0x9ABCDEF0u, attempt.Primitive.Words[5]);
        Assert.Equal(new[] { 2000, 2000, 2000, quad ? 2000 : 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(quad ? 125 : 124, attempt.Primitive.Bucket);
        Assert.Equal(quad ? 500u : 499u, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.All(attempt.WriteMask.Skip(30).Take(2), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.True(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(0xA5, result.Memory.ScriptedColorRegisters.NormalInput!.Value[7]);
        Assert.Null(result.Memory.GeometryRegisters!.V1[6]);
        Assert.Equal((byte)0xA5, result.Memory.GeometryRegisters.V1[7]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Debris_zero_depth_has_triangle_header_but_quad_header_is_unreached(bool quad)
    {
        NativeBattleEffect69Frame frame = DebrisFrame(quad);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(20, new byte[24]) } };
        NativeBattleGfGeometryBatch? batch = null;
        NativeBattleEffect69Inputs inputs = Inputs((current, operation) =>
        {
            batch = Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation).Batch;
            return current;
        }) with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleEffect69Frame result = NativeBattleEffect69Particles.DrawDebris6FF2F0(frame, inputs);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch!.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(quad ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, value));
        Assert.Equal(new byte[4], result.Staging!.Read(NativeBattleTemporaryMemory.Address, 4));
    }

    [Fact]
    public void Spark_draw_uses_reverse_signed_life_directory_and_keeps_extended_flag_sticky()
    {
        byte[] sprite = new byte[56];
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(8), 18);
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(10), 48);
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(44), 52);
        BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(48), 0x80000000);
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 2; index++)
        {
            int offset = 0x8020 + index * 16;
            arena = arena.Write(offset, BitConverter.GetBytes(SpriteAddress)).Write(offset + 4, new byte[6]).Write(offset + 10, BitConverter.GetBytes((short)2048)).Write(offset + 15, [index == 0 ? (byte)18 : (byte)1]);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena }, Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x25, [0]) };
        List<uint> cursors = [];
        NativeBattleEffect69Inputs inputs = Inputs((current, operation) =>
        {
            Assert.Empty(Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation).Batch.Attempts);
            Assert.Equal((byte)1, current.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x25, 1)[0]);
            cursors.Add(BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address + 0x2C, 4)));
            return current;
        });
        NativeBattleEffect69Frame result = NativeBattleEffect69Particles.DrawSparks7001E0(frame, Resources(sprite), inputs);
        Assert.Equal(new[] { SpriteAddress + 52, SpriteAddress + 56 }, cursors);
        Assert.Equal((short)2048, BinaryPrimitives.ReadInt16LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x84, 2)));
        Assert.Equal(new byte[2], result.Staging.Read(NativeBattleTemporaryMemory.Address + 0x96, 2));
        Assert.Equal(frame.Random, result.Random);
    }

    private static NativeBattleEffect69Frame MotionFrame(short kind, short vertical)
    {
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0, BitConverter.GetBytes((short)99)).Write(2, BitConverter.GetBytes((short)100)).Write(4, BitConverter.GetBytes((short)101)).Write(6, BitConverter.GetBytes(kind)).Write(8, BitConverter.GetBytes((short)2)).Write(10, BitConverter.GetBytes(vertical)).Write(12, BitConverter.GetBytes((short)-5)).Write(14, BitConverter.GetBytes((short)2)).Write(16, BitConverter.GetBytes(0x20DFAB8u + 0x5550));
        for (int corner = 0; corner < 4; corner++)
        {
            arena = arena.Write(20 + corner * 6, BitConverter.GetBytes((short)((corner + 1) * 2)));
        }
        for (int index = 0; index < 9; index++)
        {
            arena = arena.Write(0x5550 + index * 2, BitConverter.GetBytes((short)(index is 0 or 4 or 8 ? 2048 : 0)));
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    private static NativeBattleEffect69Frame DebrisFrame(bool quad)
    {
        NativeBattleEffect69Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(6, BitConverter.GetBytes((short)(quad ? 2 : 1))).Write(44, BitConverter.GetBytes(0x12345678u)).Write(48, BitConverter.GetBytes(0x9ABCDEF0u)).Write(52, BitConverter.GetBytes((ushort)0x2233)).Write(54, BitConverter.GetBytes((ushort)0x4455));
        NativeBattleVector[] points = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        for (int corner = 0; corner < 4; corner++)
        {
            arena = arena.Write(20 + corner * 6, BitConverter.GetBytes(points[corner].X)).Write(22 + corner * 6, BitConverter.GetBytes(points[corner].Y)).Write(24 + corner * 6, BitConverter.GetBytes(points[corner].Z));
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 7, [0xA5]) };
    }

    private static NativeBattleEffect69Resources Resources(byte[]? sprite = null) => new(null!, [], null!, null!, 0, new(0, SpriteAddress, 0, 0, (sprite ?? new byte[12]).ToImmutableArray()));
    private static NativeBattleEffect69Inputs Inputs(Func<NativeBattleEffect69Frame, NativeBattleEffect69Operation, NativeBattleEffect69Frame>? execute = null) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => throw new NotSupportedException(), (_, _) => throw new NotSupportedException(), _ => 0, _ => 0, (_, _) => 0, execute ?? ((frame, _) => frame));

    private static short Word(NativeBattleEffect69Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(offset, 2));

    private static NativeBattleEffect69Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(69, 68, new(69, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, effect, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect69State.FromColdProcess().WriteDword(ArenaGlobal, 0x20DFAB8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
}
