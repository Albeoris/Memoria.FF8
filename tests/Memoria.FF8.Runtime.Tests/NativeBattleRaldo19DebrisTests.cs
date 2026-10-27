using System.Text.Json;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRaldo19DebrisTests
{
    [Theory]
    [InlineData(-32768, -24576)]
    [InlineData(-17, -13)]
    [InlineData(-3, -3)]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(17, 13)]
    [InlineData(32767, 24576)]
    public void Damping_uses_signed_truncation_before_WORD_wrap(short value, short expected)
    {
        Assert.Equal(expected, NativeBattleRaldo19Debris.Damp(value));
    }

    [Fact]
    public void Spawner_draws_low_then_high36CRT_values_and_copies_both_position_DWORDs()
    {
        var fixture = NativeBattleRaldo19ControllerTests.Fixture();
        (NativeBattleRaldo19Frame frame, uint address) = Spawner(fixture.Frame, fixture.Inputs.Packets);
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 36; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleRaldo19Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRaldo19Frame>(NativeBattleTestSnapshots.Json(frame));
        NativeBattleRaldo19LeafStep step = NativeBattleRaldo19Debris.Advance(frame, address, fixture.Inputs.Packets, (_, _) => throw new InvalidDataException("The spawner cannot render a child inline."));
        Assert.Equal(expected, step.Frame.Random);
        NativeBattleEffectHierarchy hierarchy = new(step.Frame.Effect.Pools);
        NativeBattleModulePoolTask source = hierarchy.Read(address);
        Assert.Equal((byte)1, source.ReadByte(0x29));
        Assert.Equal((byte)2, source.ReadByte(0x28));
        Assert.Equal(0, step.Result);
        NativeBattleModulePoolTask low = Assert.Single(step.Frame.Effect.Pools[4].Slots, task => task.Callback == 0x8AAA30);
        NativeBattleModulePoolTask high = Assert.Single(step.Frame.Effect.Pools[4].Slots, task => task.Callback == 0x8AA1D0);
        for (int row = 0; row < 4; row++)
        {
            Assert.Equal((ushort)0x3333, low.ReadWord(0x72 + row * 8));
            Assert.Equal((ushort)0x3333, high.ReadWord(0x72 + row * 8));
            Assert.Equal((ushort)0, low.ReadWord(0x8E + row * 8));
            Assert.Equal((ushort)0, low.ReadWord(0x92 + row * 8));
            Assert.InRange(unchecked((short)low.ReadWord(0xAC + row * 2)), (short)-79, (short)-48);
            Assert.InRange(unchecked((short)high.ReadWord(0x8E + row * 8)), (short)-6143, (short)-2048);
            Assert.InRange(high.ReadWord(0xAC + row * 2), (ushort)240, (ushort)367);
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleRaldo19Debris.Advance(restored, address, fixture.Inputs.Packets, (_, _) => throw new InvalidDataException())));
    }

    [Fact]
    public void Draw_precedes_acceleration_and_preserves_frame_returns_and_position_padding()
    {
        var fixture = NativeBattleRaldo19ControllerTests.Fixture();
        (NativeBattleRaldo19Frame frame, uint spawner) = Spawner(fixture.Frame, fixture.Inputs.Packets);
        frame = NativeBattleRaldo19Debris.Advance(frame, spawner, fixture.Inputs.Packets, (current, _) => current).Frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        int slot = frame.Effect.Pools[4].Slots.Select((task, index) => (task, index)).Single(item => item.task.Callback == 0x8AAA30).index;
        uint address = hierarchy.Address(4, slot);
        NativeBattleModulePoolTask task = hierarchy.Read(address).WriteWord(0x8C, 0x8000).WriteWord(0x8E, 0xFFFF).WriteWord(0x90, 0x7FFF).WriteWord(0xAC, 0xFFFD);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        ushort x = task.ReadWord(0x6C);
        ushort y = task.ReadWord(0x6E);
        ushort z = task.ReadWord(0x70);
        int calls = 0;
        NativeBattleRaldo19LeafStep step = NativeBattleRaldo19Debris.Advance(frame, address, fixture.Inputs.Packets, (current, draw) =>
        {
            Assert.Equal(0x162A0C0u, draw.ResourceAddress);
            Assert.Equal((ushort)0, draw.Frame);
            Assert.Equal((short)-16, draw.DepthBias);
            NativeBattleModulePoolTask live = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            Assert.Equal(live.ReadDword(0x6C + calls * 8), live.ReadDword(0x1C));
            Assert.Equal(live.ReadDword(0x70 + calls * 8), live.ReadDword(0x20));
            calls++;
            return current with { Memory = current.Memory with { SoundPendingFlags = 0x1234 } };
        });
        task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(address);
        Assert.Equal(4, calls);
        Assert.Equal(0x1234u, step.Frame.Memory.SoundPendingFlags);
        Assert.Equal(unchecked((ushort)(x - 1536)), task.ReadWord(0x6C));
        Assert.Equal(y, task.ReadWord(0x6E));
        Assert.Equal(unchecked((ushort)(z + 1536)), task.ReadWord(0x70));
        Assert.Equal((ushort)0x3333, task.ReadWord(0x72));
        Assert.Equal((ushort)0xA000, task.ReadWord(0x8C));
        Assert.Equal((ushort)0xFFFD, task.ReadWord(0x8E));
        Assert.Equal((ushort)0x6000, task.ReadWord(0x90));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void D8_head_preserves_physical_padding_and_publishes_real_IR_MAC_vector_and_partial_stores(bool knownPadding)
    {
        var fixture = NativeBattleRaldo19ControllerTests.Fixture();
        (NativeBattleRaldo19Frame frame, uint parent) = Spawner(fixture.Frame, fixture.Inputs.Packets);
        frame = NativeBattleRaldo19Debris.Advance(frame, parent, fixture.Inputs.Packets, (current, _) => current).Frame;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() } };
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[4].Slots, item => item.Callback == 0x8AAA30);
        task = task.WriteWord(0x1C, 100).WriteWord(0x1E, unchecked((ushort)-200)).WriteWord(0x20, 300).WriteDword(0x4C, 0x162A0C0).WriteWord(0x50, 3).WriteWord(0x56, 0xFFF0);
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 216).ToArray());
        if (!knownPadding)
        {
            temporary = temporary.MarkUnknown(0xCA, 2);
        }
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = -22, Z = 33 };
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(camera, 0x1234);
        NativeBattleRaldo19DebrisHead head = NativeBattleRaldo19DebrisRendering.PrepareHead(frame, task, packed, temporary);
        (byte[] padding, bool[] known) = head.TemporaryD8.ReadSnapshot(0xCA, 2);
        Assert.All(known, value => Assert.Equal(knownPadding, value));
        if (knownPadding)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, padding);
            Assert.Equal((byte)0xA5, head.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        }
        else
        {
            Assert.Null(head.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        }
        Assert.Equal((byte)0xA5, head.TemporaryD8.Read(6, 1)[0]);
        Assert.Equal(new byte[] { 0xC0, 0xA0, 0x62, 0x01 }, head.TemporaryD8.Read(0, 4));
        Assert.Equal(new byte[] { 3, 0 }, head.TemporaryD8.Read(4, 2));
        Assert.Equal(new byte[] { 0, 0 }, head.TemporaryD8.Read(0x24, 2));
        Assert.Equal(new byte[] { 0xF0, 0xFF }, head.TemporaryD8.Read(0xB4, 2));
        Assert.Equal(new int?[] { 111, -222, 333 }, head.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(new int?[] { 111, -222, 333 }, head.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new byte[] { 100, 0, 0x38, 0xFF, 0x2C, 1, 0, 0 }, head.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void D8_dispatcher_retains_partial_UV_stores_and_applies_signed_bias_per_accepted_quad(bool extended)
    {
        var fixture = RenderingFixture(2000, extended);
        NativeBattleRaldo19DebrisHead head = NativeBattleRaldo19DebrisRendering.PrepareHead(fixture.Frame, fixture.Task, fixture.Camera, fixture.Temporary);
        NativeBattleRaldo19DebrisDraw draw = NativeBattleRaldo19DebrisRendering.Draw(head, fixture.Resources, new(null, 100, 160, 108), 0, 17);
        Assert.Equal(extended ? 92 : 80, draw.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive[] quads = draw.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 10).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(new[] { 138, 134 }, quads.Select(quad => quad.Bucket));
        Assert.Equal(468, BinaryPrimitives.ReadInt32LittleEndian(draw.TemporaryD8.Read(0x3C, 4)));
        Assert.Equal(1024, BinaryPrimitives.ReadInt32LittleEndian(draw.TemporaryD8.Read(0xAC, 4)));
        Assert.Equal((uint)500, draw.Frame.Memory.GeometryRegisters!.Otz);
        foreach (NativeBattleGfGeometryAttempt attempt in draw.Batch.Attempts.Where(attempt => attempt.Data.Length == 40 && attempt.WriteMask[4] == NativeBattleGfPacketByteState.Known))
        {
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[29]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[37]);
            foreach (int offset in new[] { 30, 31, 38, 39 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
            }
        }
        if (extended)
        {
            NativeBattleGfGeometryAttempt environment = Assert.Single(draw.Batch.Attempts, attempt => attempt.Primitive?.Words.Length == 2);
            Assert.Equal(40, environment.ByteOffsetRelative);
            Assert.Equal(138, environment.Primitive!.Bucket);
            Assert.All(environment.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }
        else
        {
            Assert.DoesNotContain(draw.Batch.Attempts, attempt => attempt.Primitive?.Words.Length == 2);
        }
    }

    [Fact]
    public void Clipped_D8_quads_publish_candidate_bytes_and_flags_without_advance_or_fourth_projection()
    {
        var fixture = RenderingFixture(0, false);
        NativeBattleRaldo19DebrisHead head = NativeBattleRaldo19DebrisRendering.PrepareHead(fixture.Frame, fixture.Task, fixture.Camera, fixture.Temporary);
        NativeBattleRaldo19DebrisDraw draw = NativeBattleRaldo19DebrisRendering.Draw(head, fixture.Resources, new(null, 100, 160, 108), 0, 17);
        Assert.Equal(0, draw.Batch.AllocationBytes);
        Assert.Equal(2, draw.Batch.Attempts.Length);
        Assert.All(draw.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[29]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[37]);
        });
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(draw.TemporaryD8.Read(0x40, 4)) & 0x60000);
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(draw.TemporaryD8.Read(0x3C, 4)));
    }

    private static (NativeBattleRaldo19Frame Frame, NativeBattleModulePoolTask Task, NativeBattlePackedMatrix32 Camera, NativeBattleEffectArena Temporary, NativeBattleRaldo19Resources Resources) RenderingFixture(short depth, bool extended)
    {
        var fixture = NativeBattleRaldo19ControllerTests.Fixture();
        (NativeBattleRaldo19Frame frame, uint parent) = Spawner(fixture.Frame, fixture.Inputs.Packets);
        frame = NativeBattleRaldo19Debris.Advance(frame, parent, fixture.Inputs.Packets, (current, _) => current).Frame;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[4].Slots, item => item.Callback == 0x8AAA30);
        task = task.WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, unchecked((ushort)depth)).WriteDword(0x4C, 0x162A0C0).WriteWord(0x50, 0).WriteWord(0x56, 0xFFF0);
        byte[] sprite = new byte[0x144];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 12);
        for (int frameIndex = 0; frameIndex <= 12; frameIndex++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + 2 * frameIndex), 36);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(36), extended ? 0x80000002u : 2);
        for (int quad = 0; quad < 2; quad++)
        {
            Span<byte> row = sprite.AsSpan(40 + 20 * quad, 20);
            row[0] = row[2] = 2;
            row[1] = 100;
            row[3] = 0x2C;
            row[4] = 10;
            row[5] = 20;
            BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(14), quad == 0 ? (ushort)0x123 : (ushort)0xD23);
            if (quad == 1)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(12), 1024);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(16), 4096);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(18), 2048);
            }
        }
        return (frame, task, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0x1234), NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 216).ToArray()), fixture.Resources with { DebrisLow = sprite.ToImmutableArray() });
    }

    private static (NativeBattleRaldo19Frame Frame, uint Address) Spawner(NativeBattleRaldo19Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint parent = hierarchy.Address(0, 0);
        NativeBattleEffectHierarchyAppend created = hierarchy.Append8DC540(4, 0x8A9F10, 184, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, packets, pointer, group, target));
        NativeBattleModulePoolTask task = created.Hierarchy.Read(created.Address).WriteDword(0x1C, 0x11112222).WriteDword(0x20, 0x33334444).WriteWord(0xB6, 1);
        return (frame with { Effect = frame.Effect with { Pools = created.Hierarchy.Write(created.Address, task).Pools } }, created.Address);
    }
}
