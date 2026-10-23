using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect283BodyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Body_preserves_ordered_common_packets_unsigned_quarter_depth_and_counter_underflow(bool quad)
    {
        byte[] geometry = Geometry(quad);
        NativeBattleEffect283BodyDrawingStep result = Draw(geometry, Temporary(), Identity(8000));
        NativeBattleEffect283BodyWrite[] packets = result.Batch.Writes.Where(write => write.Bank == NativeBattleEffect283BodyBank.Common).ToArray();
        Assert.Equal(3, packets.Length);
        Assert.Equal(quad ? 72 : 60, result.Batch.CommonAllocationBytes);
        Assert.Equal(quad ? new[] { 0, 40, 64 } : new[] { 0, 32, 52 }, packets.Select(write => write.Attempt.ByteOffsetRelative));
        Assert.Equal(new uint[] { quad ? 0x2Eu : 0x26u, quad ? 0x2Au : 0x22u, 0xE1 }, packets.Select(write => write.Attempt.Primitive!.Words[1] >> 24));
        Assert.Equal(quad ? new[] { 2000, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 0 }, packets[0].Attempt.Primitive!.VertexPayload4);
        Assert.Equal(packets[0].Attempt.Primitive!.VertexPayload4, packets[1].Attempt.Primitive!.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, packets[2].Attempt.Primitive!.VertexPayload4);
        Assert.Equal((ushort)0xFFFF, U16(result.Temporary124, 0x28));
        Assert.Equal((ushort)0xFFFF, U16(result.Temporary124, 0x2A));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packets[0].Attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packets[0].Attempt.WriteMask[31]);
        Assert.Equal((ushort)0x00B6, BinaryPrimitives.ReadUInt16LittleEndian(packets[0].Attempt.Data.AsSpan().Slice(22)));
        Assert.All(result.Batch.Writes.Where(write => write.Bank == NativeBattleEffect283BodyBank.ProjectionScratch), write => Assert.Equal(8, write.Attempt.PacketLength));
    }

    [Fact]
    public void Zero_depth_writes_only_outcode_word_and_retains_scratch_suffix()
    {
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 32).ToArray());
        NativeBattleEffect283BodyDrawingStep result = Draw(Geometry(false), Temporary(), Identity(1), scratch: scratch);
        Assert.Equal(0, result.Batch.CommonAllocationBytes);
        Assert.Equal(3, result.Batch.Writes.Length);
        foreach (NativeBattleEffect283BodyWrite write in result.Batch.Writes)
        {
            Assert.Equal(6, write.Attempt.ByteOffsetRelative % 8);
            Assert.Equal(new byte[] { 0x10, 0 }, write.Attempt.Data);
        }
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0x10, 0 }, result.Scratch.Read(0, 8));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), result.Scratch.Read(24, 8));
    }

    [Fact]
    public void Inclusive_128_uses_128_stride_extension_and_unsigned_radial_words()
    {
        byte[] geometry = Geometry(false);
        geometry.AsSpan(14, 18).Clear();
        byte[] distance = new byte[16513];
        distance[16512] = 64;
        NativeBattleEffectArena temporary = Temporary().Write(0x70, BitConverter.GetBytes((short)128)).Write(0x72, BitConverter.GetBytes((short)1152)).Write(0x74, BitConverter.GetBytes((short)736));
        NativeBattleEffect283BodyDrawingStep result = Draw(geometry, temporary, Identity(8000), distance.ToImmutableArray(), Enumerable.Repeat((ushort)2048, 128).ToImmutableArray());
        Assert.Equal(128, result.MaximumDeviation);
        Assert.Equal((short)1216, S16(result.Scratch, 0));
        Assert.Equal((short)800, S16(result.Scratch, 2));
        Assert.Equal((byte)0, result.Scratch.Read(7, 1)[0]);
        Assert.Equal(60, result.Batch.CommonAllocationBytes);
        Assert.Equal(0u, U32(result.Temporary124, 0x5C));
        Assert.Throws<InvalidDataException>(() => Draw(geometry, temporary, Identity(8000), new byte[16384].ToImmutableArray()));
    }

    [Fact]
    public void Outer_composes_before_shadow_and_shares_maximum_with_actual_linked_callback()
    {
        NativeBattleGraphicsModel model = new(4, 0x20) { Color40 = 0x123456, GeometryWorldMatrix = Identity().ReadMatrix(), SequenceState = new() { MeshMask124 = 3 } };
        List<string> calls = [];
        NativeBattleEffect283BodyServices services = new(current =>
        {
            calls.Add("shadow");
            Assert.Equal(4096, S16(current.Temporary124, 0));
            Assert.Equal((ushort)0x5AA5, U16(current.Temporary124, 18));
            Assert.Equal((short)7, S16(current.Temporary124, 0x72));
            return current with { CommonCursor = 50 };
        }, (current, basis) =>
        {
            calls.Add("compose");
            return current;
        }, current =>
        {
            calls.Add("draw");
            Assert.Equal(50u, current.CommonCursor);
            Assert.Equal(0x00123456u, U32(current.Temporary124, 0x34));
            Assert.Equal(3u, U32(current.Temporary124, 0x38));
            return current with { Temporary124 = current.Temporary124.Write(0x76, BitConverter.GetBytes((short)80)), CommonCursor = 110 };
        }, current =>
        {
            calls.Add("restore");
            return current;
        })
        {
            DrawLinkedAndDeliver = (current, basis) =>
            {
                calls.Add("linked");
                Assert.Equal(uint.MaxValue, U32(current.Temporary124, 0x38));
                Assert.Equal((short)80, S16(current.Temporary124, 0x76));
                return current with { Temporary124 = current.Temporary124.Write(0x76, BitConverter.GetBytes((short)128)) };
            }
        };
        NativeBattleEffect283BodyFrame frame = new(Memory(), NativeBattleFileStaging.Unknown(), model, NativeBattleEffectArena.Clear().MarkUnknown(0, 124).Write(18, new byte[] { 0xA5, 0x5A }), 20);
        frame = NativeBattleEffect283BodyDrawing.DrawOuter(frame, Identity(), 7, 9, 24, 0x1000, 0, ScratchAddress, NativeBattleGteColorInterpolation.CodeConstants45F270, services);
        Assert.Equal(new[] { "shadow", "compose", "draw", "restore", "linked" }, calls);
        Assert.Equal((short)128, S16(frame.Temporary124, 0x76));
        Assert.Equal(4096u, frame.Memory.ScriptedColorRegisters!.Ir0);
    }

    private const uint ScratchAddress = 0x2000;
    private static NativeBattleEffect283BodyDrawingStep Draw(byte[] geometry, NativeBattleEffectArena temporary, NativeBattlePackedMatrix32 matrix, ImmutableArray<byte> distance = default, ImmutableArray<ushort> radial = default, NativeBattleEffectArena? scratch = null)
    {
        NativeBattleScriptedGraphicsMemory controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096, Zsf4 = 4096 };
        return NativeBattleEffect283BodyDrawing.DrawBody(Memory(), temporary, (address, length) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan().Slice(checked((int)(address - 0x1000)), length)), 0x1000, [matrix], new(null, 100, 160, 108), controls, scratch ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 32), ScratchAddress, 32, distance.IsDefault ? new byte[16513].ToImmutableArray() : distance, radial.IsDefault ? Enumerable.Repeat((ushort)4096, 128).ToImmutableArray() : radial);
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity().ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 124).Write(0x24, BitConverter.GetBytes(ScratchAddress)).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((short)320)).Write(0x32, BitConverter.GetBytes((short)216)).Write(0x34, BitConverter.GetBytes(0x02123456u)).Write(0x38, BitConverter.GetBytes(1u)).Write(0x70, new byte[8]);
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short S16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));

    private static byte[] Geometry(bool quad)
    {
        int vertices = quad ? 4 : 3;
        int countOffset = (14 + vertices * 6 + 3) & ~3;
        byte[] bytes = new byte[countOffset + 12 + (quad ? 20 : 16)];
        D(bytes, 0, 1);
        D(bytes, 4, 8);
        W(bytes, 8, 1);
        W(bytes, 12, vertices);
        W(bytes, 20, 100);
        W(bytes, 28, 100);
        if (quad)
        {
            W(bytes, 32, 100);
            W(bytes, 34, 100);
        }
        W(bytes, countOffset + (quad ? 2 : 0), 1);
        int face = countOffset + 12;
        W(bytes, face + 2, 1);
        W(bytes, face + 4, 2);
        W(bytes, face + 6, quad ? 3 : 0x8078);
        D(bytes, face + 8, 0x3D948048);
        D(bytes, face + 12, 0x02B68078);
        if (quad)
        {
            W(bytes, face + 16, 0x8078);
            W(bytes, face + 18, 0x8048);
        }
        return bytes;
    }

    private static void W(byte[] data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), checked((ushort)value));
    private static void D(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
