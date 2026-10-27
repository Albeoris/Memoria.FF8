using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleResonance49Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleResonance49ProgrammeTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResonance49ScreenTests
{
    [Fact]
    public void Seed_consumes_three_CRT_draws_and_writes_UV_only_with_native_wrap_exception()
    {
        var frame = Frame(4);
        frame = frame with { State = frame.State.WriteView(0x2647900, NativeBattleEffectArena.Clear().MarkUnknown(0, 1056), 1056) };
        var random = frame.Random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort third);
        var actual = InitializeScreen824300(frame, 0x26467E8, Resources(), Inputs());
        Assert.Equal(random, actual.Random);
        Assert.Equal((ushort)((first & 63) + 32), actual.State.ReadWord(0x26466B6));
        Assert.Equal((ushort)(second & 4095), actual.State.ReadWord(0x26466B8));
        Assert.Equal((ushort)(third & 4095), actual.State.ReadWord(0x26466BC));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadWord(0x2647900));
        Assert.Equal(new byte[] { 0, 0, 32, 0, 0, 32, 32, 32 }, actual.State.ReadView(0x2647904, 8).Read(0, 8));
        Assert.Equal((byte)255, actual.State.ReadView(0x2647906 + 7u * 96, 1).Read(0, 1)[0]);
    }

    [Fact]
    public void Screen_emits_140_ordered_pairs_with_exact_raw_texture_holes_mode_NOP_and_no_GTE_changes()
    {
        var frame = InitializeScreen824300(Frame(4), 0x26467E8, Resources(), Inputs());
        List<NativeBattleResonance49DeliverScreenPair> pairs = [];
        var actual = DrawScreen8244E0(frame, 0x26467E8, 5, Resources(), Inputs((current, operation) =>
        {
            pairs.Add(Assert.IsType<NativeBattleResonance49DeliverScreenPair>(operation));
            return current;
        }));
        Assert.Equal(140, pairs.Count);
        foreach (var pair in pairs)
        {
            Assert.Equal(0, pair.Quad.ByteOffsetRelative);
            Assert.Equal(40, pair.Quad.PacketLength);
            Assert.Equal(40, pair.Mode.ByteOffsetRelative);
            Assert.Equal(12, pair.Mode.PacketLength);
            Assert.Equal(5, pair.Quad.Primitive!.Bucket);
            Assert.Empty(pair.Quad.Primitive.VertexPayload4);
            Assert.Equal(new uint[] { 0x02000000, 0xE1000020, 0 }, pair.Mode.Primitive!.Words);
            foreach (int offset in new[] { 4, 5, 6, 14, 15 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, pair.Quad.WriteMask[offset]);
            }
            Assert.All(pair.Mode.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
            Assert.All(pair.Mode.WriteMask.Skip(3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        Assert.All(pairs.Take(70), pair => Assert.Equal((byte)0x2F, pair.Quad.Data[7]));
        Assert.All(pairs.Skip(70), pair => Assert.Equal((byte)0x2D, pair.Quad.Data[7]));
        Assert.Equal((ushort)0x119, BinaryPrimitives.ReadUInt16LittleEndian(pairs[55].Quad.Data.AsSpan().Slice(22)));
        Assert.Equal((ushort)0x11D, BinaryPrimitives.ReadUInt16LittleEndian(pairs[56].Quad.Data.AsSpan().Slice(22)));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Memory), NativeBattleTestSnapshots.Json(actual.Memory));
        var random = frame.Random;
        for (int draw = 0; draw < 6; draw++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
    }

    [Fact]
    public void Negative_deformation_divides_toward_zero_and_grid_boundaries_stay_literal()
    {
        var frame = InitializeScreen824300(Frame(4), 0x26467E8, Resources(), Inputs());
        frame = frame with { State = frame.State.WriteWord(0x26466B0, unchecked((ushort)-511)) };
        var task = Read(frame, 0x26467E8).WriteWord(0x58, 0).WriteWord(0x60, 512).WriteWord(0x62, 512).WriteWord(0x70, 512).WriteWord(0x72, 512);
        frame = Write(frame, 0x26467E8, task);
        frame.Random.Draw(out ushort first);
        int angle = 256 + (first & 255);
        int product = NativeBattleCameraMath.Sine(angle) * -511;
        Assert.Equal(0, product / 2097152);
        Assert.Equal(-1, product >> 21);
        var actual = DrawScreen8244E0(frame, 0x26467E8, 5, Resources(), Inputs());
        Assert.Equal((ushort)0, actual.State.ReadWord(0x26466E0));
        Assert.Equal((ushort)0, actual.State.ReadWord(0x2647900));
        Assert.Equal((ushort)320, actual.State.ReadWord(0x2647900 + 10u * 96));
        Assert.Equal((ushort)240, actual.State.ReadWord(0x2647902 + 7u * 12));
    }

    [Theory]
    [InlineData((byte)1, 0)]
    [InlineData((byte)0, 1)]
    [InlineData((byte)2, 1)]
    public void Environment_uses_native_buffer_minus_one_parity_and_retains_static_packet_suffix(byte buffer, int parity)
    {
        var frame = Frame(4);
        List<NativeBattleResonance49StorePacketBatch> packets = [];
        NativeBattleEffectArena environment = NativeBattleEffectArena.Clear().Write(0, new byte[28]).MarkUnknown(28, 64).Write(4, BitConverter.GetBytes((short)320)).Write(6, BitConverter.GetBytes((short)240)).Write(16, BitConverter.GetBytes((short)256)).Write(18, BitConverter.GetBytes((short)256));
        var actual = PublishEnvironment824410(frame, Resources(), Inputs((current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleResonance49StorePacketBatch>(operation));
            return current;
        }) with
        {
            CurrentPrimitiveBufferByte = _ => buffer,
            ReadEnvironment92 = (_, selected) =>
            {
                Assert.Equal(parity, selected);
                return environment;
            }
        });
        Assert.Equal(2, packets.Count);
        Assert.Equal(0x26472D8u + (uint)parity * 64, packets[0].DestinationAddress);
        Assert.Equal(0x2647820u + (uint)parity * 64, packets[1].DestinationAddress);
        Assert.Equal(4385, Assert.Single(packets[0].Batch.Attempts).Primitive!.Bucket);
        Assert.Equal(5, Assert.Single(packets[1].Batch.Attempts).Primitive!.Bucket);
        var capture = Assert.Single(packets[0].Batch.Attempts);
        var restore = Assert.Single(packets[1].Batch.Attempts);
        Assert.Equal(0x02000000u, capture.Primitive!.Words[6]);
        Assert.Equal(36, capture.Primitive.Words.Length * 4);
        Assert.Equal(24, restore.Primitive!.Words.Length * 4);
        Assert.All(capture.WriteMask.Skip(36), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(restore.WriteMask.Skip(24), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadView(0x2647760 + (uint)parity * 92 + 28, 1).Read(0, 1));
    }

    [Fact]
    public void Screen_JSON_continuation_keeps_RNG_and_overwritten_single_grid_owner()
    {
        var frame = InitializeScreen824300(Frame(4), 0x26467E8, Resources(), Inputs());
        frame = DrawScreen8244E0(frame, 0x26467E8, 5, Resources(), Inputs());
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleResonance49Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(DrawScreen8244E0(frame, 0x26467E8, 5, Resources(), Inputs())), NativeBattleTestSnapshots.Json(DrawScreen8244E0(restored, 0x26467E8, 5, Resources(), Inputs())));
    }
}
