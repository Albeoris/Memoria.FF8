using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect65LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65StripTests
{
    [Fact]
    public void Initialization_retains_payload_and_both_random_owners()
    {
        NativeBattleEffect65Frame frame = Frame();
        uint point = ArenaAddress + 0x3B530;
        uint record = ArenaAddress + 0x3CD30;
        frame = Write(frame, point, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 24).ToArray()), 24);
        frame = Write(frame, record, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 56).ToArray()), 56);
        frame = frame with { State = frame.State.WriteDword(RandomGlobal, 12345) };
        NativeBattleEffect65Frame result = NativeBattleEffect65Strips.Initialize6E3910(frame, 7, 4096, -2048);
        Assert.Equal(12345u, result.State.ReadDword(RandomGlobal));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(-1, Word(Read(result, point + 6, 2), 0));
        Assert.Equal(0xA5, Read(result, point, 1).Read(0, 1)[0]);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(Read(result, record, 4).Read(0, 4)));
        Assert.Equal(0xA5A5A5A5u, BinaryPrimitives.ReadUInt32LittleEndian(Read(result, record + 4, 4).Read(0, 4)));
        Assert.Equal(8, result.State.ReadWord(0x2544FE8));
        Assert.Equal(64, result.State.ReadWord(0x2544FEA));
    }

    [Fact]
    public void Selected_fraction_step_limits_growth_to_nine_points_without_resetting_CRT()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65Strips.Initialize6E3910(Frame(), 7, 0, -2048);
        (frame, uint record) = NativeBattleEffect65Strips.Birth6E4F60(frame, Descriptor());
        for (int index = 0; index < 8; index++)
        {
            frame = NativeBattleEffect65Strips.Advance6E3F30(frame, Resources(), Inputs());
        }
        Assert.Equal(9, Word(Read(frame, record + 0x34, 2), 0));
        Assert.Equal(4096, Word(Read(frame, record + 0x1E, 2), 0));
        NativeCrtRandomState shared = frame.Random;
        frame = NativeBattleEffect65Strips.Advance6E3F30(frame, Resources(), Inputs());
        Assert.Equal(9, Word(Read(frame, record + 0x34, 2), 0));
        Assert.Equal(shared, frame.Random);
    }

    [Fact]
    public void Point_allocation_failure_exits_remaining_records_after_reached_LCG_and_GTE()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65Strips.Initialize6E3910(Frame(), 7, 0, -2048);
        (frame, uint record) = NativeBattleEffect65Strips.Birth6E4F60(frame, Descriptor());
        uint points = ArenaAddress + 0x3B530;
        for (int index = 1; index < 256; index++)
        {
            frame = Write(frame, points + (uint)(index * 24 + 6), NativeBattleEffectArena.Clear(), 2);
        }
        uint later = record + 56;
        frame = Write(frame, later, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(points + 24)).Write(0x36, BitConverter.GetBytes((short)4096)), 56);
        uint randomBefore = frame.State.ReadDword(RandomGlobal);
        NativeBattleEffect65Frame result = NativeBattleEffect65Strips.Advance6E3F30(frame, Resources(), Inputs());
        Assert.Equal(4096, Word(Read(result, record + 0x1E, 2), 0));
        Assert.Equal(4096, Word(Read(result, later + 0x36, 2), 0));
        Assert.NotEqual(randomBefore, result.State.ReadDword(RandomGlobal));
        Assert.Equal(512u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Fade_first_zero_frees_only_native_markers_without_motion_or_LCG()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65Strips.Initialize6E3910(Frame(), 7, 0, -2048);
        (frame, uint record) = NativeBattleEffect65Strips.Birth6E4F60(frame, Descriptor());
        uint point = BinaryPrimitives.ReadUInt32LittleEndian(Read(frame, record, 4).Read(0, 4));
        frame = Write(frame, record + 0x36, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)256)), 2);
        uint seed = frame.State.ReadDword(RandomGlobal);
        NativeBattleEffect65Frame result = NativeBattleEffect65Strips.Advance6E3F30(frame, Resources(), Inputs());
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(Read(result, record, 4).Read(0, 4)));
        Assert.Equal(-1, Word(Read(result, point + 6, 2), 0));
        Assert.Equal(100, Word(Read(result, point, 2), 0));
        Assert.Equal(seed, result.State.ReadDword(RandomGlobal));
        Assert.Equal(record, result.State.ReadDword(0x2544FC0));
    }

    [Theory]
    [InlineData(4096, 0x80F0F0u)]
    [InlineData(2048, 0x407878u)]
    public void Strip_draw_stores_low_IR_RGB_without_FIFO_scaling_and_preserves_color_code(short fade, uint expectedRgb)
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65Strips.Initialize6E3910(Frame(), 7, 0, -2048);
        (frame, uint record) = NativeBattleEffect65Strips.Birth6E4F60(frame, Descriptor());
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(Read(frame, record, 4).Read(0, 4));
        (frame, uint second) = NativeBattleEffect65Strips.AllocatePoint6E41D0(frame);
        frame = Write(frame, first + 0x14, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(second)), 4);
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((short)10000)).Write(0xE, BitConverter.GetBytes((short)4096)).Write(0x10, BitConverter.GetBytes((short)0x3000));
        frame = Write(frame, second, point, 24);
        frame = Write(frame, record + 0x34, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)2)), 2);
        frame = Write(frame, record + 0x36, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(fade)), 2);
        frame = Write(frame, ArenaAddress + 0x60008, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)500)), 8);
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { Material = null } } };
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame result = NativeBattleEffect65Strips.Draw6E3990(frame, Resources(), Inputs(operations));
        NativeBattleEffect65DeliverBatch delivery = Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(operations));
        Assert.Equal((ushort)1, delivery.Flags);
        Assert.Equal(0x1D8E054u, delivery.CursorGlobal);
        Assert.Equal(52, delivery.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(delivery.Batch.Attempts);
        Assert.Equal(0x3Eu, attempt.Primitive!.Words[1] >> 24);
        Assert.Equal(expectedRgb, attempt.Primitive.Words[1] & 0xFFFFFFu);
        Assert.Equal(expectedRgb, attempt.Primitive.Words[4] & 0xFFFFFFu);
        Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(0x3F54A048u, attempt.Primitive.Words[3]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[19]);
        Assert.Equal(0u, attempt.Primitive.Words[4] >> 24);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[50]);
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(frame.Random, result.Random);
    }

    private static NativeBattleEffectArena Descriptor() => NativeBattleEffectArena.Clear().MarkUnknown(0, 56).Write(8, BitConverter.GetBytes(ArenaAddress + 0x60000)).Write(12, BitConverter.GetBytes(ArenaAddress + 0x60008)).Write(0x10, BitConverter.GetBytes((short)100)).Write(0x12, BitConverter.GetBytes((short)200)).Write(0x14, BitConverter.GetBytes((short)300)).Write(0x16, BitConverter.GetBytes((short)10000)).Write(0x18, new byte[8]).Write(0x20, new byte[6]).Write(0x26, BitConverter.GetBytes((short)512)).Write(0x28, new byte[8]).Write(0x30, BitConverter.GetBytes(0xFE003000u)).Write(0x36, BitConverter.GetBytes((short)4096));
}
