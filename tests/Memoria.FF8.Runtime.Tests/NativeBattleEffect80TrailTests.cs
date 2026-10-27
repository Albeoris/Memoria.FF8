using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80TrailTests
{
    [Fact]
    public void Initialization_clears_only_native_free_markers_and_retains_lcg_and_backing()
    {
        NativeBattleEffect80Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(TrailRandomGlobal, 1234) };
        NativeBattleEffect80Frame result = NativeBattleEffect80Trails.Initialize7B43F0(frame, Resources());
        Assert.Equal(1234u, result.State.ReadDword(TrailRandomGlobal));
        Assert.Equal(0xA5A5A5A5u, result.State.ReadDword(TrailAddress));
        Assert.Equal(ushort.MaxValue, result.State.ReadWord(TrailAddress + 6));
        Assert.Equal(0u, result.State.ReadDword(TrailAddress + 0x3000));
        Assert.Equal(0xA5A5A5A5u, result.State.ReadDword(TrailAddress + 0x3004));
        Assert.Equal(TrailAddress, result.State.ReadDword(TrailPointCursorGlobal));
        Assert.Equal(TrailAddress + 0x3000, result.State.ReadDword(TrailRecordCursorGlobal));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Point_allocator_wraps_and_exhaustion_preserves_cursor()
    {
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), Resources());
        frame = frame with { State = frame.State.WriteDword(TrailPointCursorGlobal, TrailAddress + 0x2FE8) };
        (frame, uint last) = NativeBattleEffect80Trails.AllocatePoint7AFD80(frame);
        Assert.Equal(TrailAddress + 0x2FE8, last);
        Assert.Equal(TrailAddress, frame.State.ReadDword(TrailPointCursorGlobal));
        for (uint index = 0; index < 512; index++)
        {
            frame = frame with { State = frame.State.WriteWord(TrailAddress + index * 24 + 6, 0) };
        }
        NativeBattleEffect80Frame before = frame;
        (frame, uint exhausted) = NativeBattleEffect80Trails.AllocatePoint7AFD80(frame);
        Assert.Equal(0u, exhausted);
        Assert.Equal(before, frame);
    }

    [Fact]
    public void Record_cursor_advances_before_point_failure_and_descriptor_is_not_read()
    {
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), Resources());
        for (uint index = 0; index < 512; index++)
        {
            frame = frame with { State = frame.State.WriteWord(TrailAddress + index * 24 + 6, 0) };
        }
        (frame, uint point) = NativeBattleEffect80Trails.Birth7AFDD0(frame, NativeBattleEffectArena.Clear().MarkUnknown(0, 48), Resources());
        Assert.Equal(0u, point);
        Assert.Equal(TrailAddress + 0x3030, frame.State.ReadDword(TrailRecordCursorGlobal));
        Assert.Equal(0u, frame.State.ReadDword(TrailAddress + 0x3000));
    }

    [Fact]
    public void Birth_copies_only_reached_descriptor_fields_and_lcg_is_independent_of_crt()
    {
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), Resources());
        NativeBattleEffectArena descriptor = Descriptor().MarkUnknown(0, 8).MarkUnknown(0x2C, 2);
        (frame, uint point) = NativeBattleEffect80Trails.Birth7AFDD0(frame, descriptor, Resources());
        Assert.Equal(TrailAddress, point);
        Assert.Equal(0u, frame.State.ReadDword(point + 8));
        Assert.Equal(0u, frame.State.ReadDword(point + 12));
        Assert.Equal(0xFF003000u, frame.State.ReadDword(point + 0x10));
        Assert.Equal(1, frame.State.ReadWord(TrailAddress + 0x302C));
        NativeCrtRandomState crt = frame.Random;
        (frame, int random) = NativeBattleEffect80Trails.Random7AFEA0(frame);
        Assert.Equal(14, random);
        Assert.Equal(crt, frame.Random);
        NativeBattleEffect80Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect80Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        (_, int next) = NativeBattleEffect80Trails.Random7AFEA0(restored);
        Assert.Equal(1764, next);
    }

    [Fact]
    public void Fade_retirement_frees_the_chain_without_reading_unreached_geometry()
    {
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), Resources());
        (frame, uint point) = NativeBattleEffect80Trails.Birth7AFDD0(frame, Descriptor().Write(0x2E, BitConverter.GetBytes((short)256)), Resources());
        frame = NativeBattleEffect80Trails.Update7AF9F0(frame, Resources());
        Assert.Equal(ushort.MaxValue, frame.State.ReadWord(point + 6));
        Assert.Equal(0u, frame.State.ReadDword(TrailAddress + 0x3000));
        Assert.Equal(TrailAddress + 0x3000, frame.State.ReadDword(TrailRecordCursorGlobal));
        Assert.Equal(0u, frame.State.ReadDword(TrailRandomGlobal));
    }

    [Fact]
    public void Point_failure_stops_entire_updater_after_four_lcg_calls()
    {
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), Resources());
        (frame, _) = NativeBattleEffect80Trails.Birth7AFDD0(frame, Descriptor(), Resources());
        (frame, _) = NativeBattleEffect80Trails.Birth7AFDD0(frame, Descriptor(), Resources());
        for (uint index = 0; index < 512; index++)
        {
            frame = frame with { State = frame.State.WriteWord(TrailAddress + index * 24 + 6, 0) };
        }
        frame = NativeBattleEffect80Trails.Update7AF9F0(frame, Resources());
        Assert.Equal(4096, frame.State.ReadWord(TrailAddress + 0x3016));
        Assert.Equal(4096, frame.State.ReadWord(TrailAddress + 0x305E));
        uint expected = 0;
        for (int index = 0; index < 4; index++)
        {
            expected = (expected * 125 + 14) & 0x7FFF;
        }
        Assert.Equal(expected, frame.State.ReadDword(TrailRandomGlobal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Draw_keeps_depth_gradient_alias_zero_weight_gpl_and_retained_direction_between_records(bool degenerateSecondRecord)
    {
        NativeBattleEffect80Resources resources = Resources();
        byte[] shape = new byte[ShapeTableLength];
        for (int index = 0; index < shape.Length; index += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(index), 4096);
        }
        byte[] distances = Enumerable.Repeat((byte)1, DistanceLength).ToArray();
        distances[0] = 0;
        resources = resources with { StaticTables = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(ShapeTableAddress, shape.ToImmutableArray()).Add(DistanceAddress, distances.ToImmutableArray()) };
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), resources);
        (frame, uint first) = NativeBattleEffect80Trails.Birth7AFDD0(frame, Descriptor(), resources);
        uint second = first + 24;
        frame = frame with
        {
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()),
            Memory = frame.Memory with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() },
            State = frame.State.WriteDword(first + 0x14, second).Write(second, new byte[24]).WriteWord(second, 40).WriteWord(second + 6, 32).WriteWord(second + 0x10, 0x3000).WriteWord(TrailAddress + 0x302C, 2).WriteDword(TrailAddress + 0x3004, second)
        };
        if (degenerateSecondRecord)
        {
            uint record = TrailAddress + 0x3030;
            uint third = first + 48;
            for (uint index = 0; index < 3; index++)
            {
                uint node = third + index * 24;
                frame = frame with { State = frame.State.Write(node, new byte[24]).WriteWord(node, index == 2 ? (ushort)60 : (ushort)40).WriteWord(node + 6, 32).WriteWord(node + 0x10, 0x3000).WriteDword(node + 0x14, index == 2 ? 0u : node + 24) };
            }
            frame = frame with { State = frame.State.WriteDword(record, third).WriteWord(record + 0x2C, 3).WriteWord(record + 0x2E, 4096) };
        }
        NativeBattleGfGeometryBatch? batch = null;
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        NativeBattleEffect80Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => view, _ => new(null, 100, 160, 108), null!, null!, null!, _ => 0, null!, (current, operation) =>
        {
            NativeBattleEffect80DeliverBatch delivery = Assert.IsType<NativeBattleEffect80DeliverBatch>(operation);
            Assert.Equal(0x1D8E054u, delivery.CursorGlobal);
            batch = delivery.Batch;
            return current;
        });
        NativeBattleEffect80Frame result = NativeBattleEffect80Trails.Draw7AF4D0(frame, resources, inputs);
        Assert.NotNull(batch);
        Assert.Equal(degenerateSecondRecord ? 208 : 104, batch.AllocationBytes);
        Assert.Equal(degenerateSecondRecord ? 4 : 2, batch.Attempts.Length);
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive!.VertexPayload4.ToArray());
            Assert.Equal(0x3Eu, attempt.Primitive.Words[1] >> 24);
            Assert.Equal(0x80F0F0u, attempt.Primitive.Words[1] & 0xFFFFFF);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[19]);
        }
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(-32, BinaryPrimitives.ReadInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 0x2C, 4)));
    }

    private static NativeBattleEffectArena Descriptor() => NativeBattleEffectArena.Clear().Write(8, BitConverter.GetBytes((uint)20)).Write(12, BitConverter.GetBytes(0x200000u)).Write(0x1E, BitConverter.GetBytes((ushort)256)).Write(0x28, BitConverter.GetBytes(0xFF003000u)).Write(0x2E, BitConverter.GetBytes((ushort)4096));
    private static NativeBattleEffect80Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() }, new(80, 79, new(80, Entry, Initializer), null, null) { Pools = [] }, new(99), NativeBattleEffect80State.FromColdProcess());
    private static NativeBattleEffect80Resources Resources() => new(new("texture", []), new("camera", []), new("vibration", []), ImmutableDictionary<uint, NativeBattleEffect80NodeResourceBank>.Empty, InitializedSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => pair.Key == TrailPointerAddress ? BitConverter.GetBytes(TrailAddress).ToImmutableArray() : Enumerable.Repeat((byte)0xA5, pair.Value).ToImmutableArray()), [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, TargetAddress);
}
