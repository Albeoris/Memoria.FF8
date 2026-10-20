using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221BreakupTrianglesTests
{
    [Fact]
    public void Intact_and_detached_triangles_keep_exact_UV_masks_depth_and_45C7A0_FIFO_payload()
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        const uint state = basis + 0x3000;
        const uint shape = basis + 0x3400;
        const uint vertices = basis + 0x3800;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000).WriteTemporaryDword(0x7C, 0).WriteTemporaryDword(0x80, 0).WriteTemporaryByte(0x145, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, basis + 0x4000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, basis + 0x5000);
        frame = PutDword(frame, state + 0xC, shape);
        frame = PutWord(frame, state + 0xA, 2);
        frame = PutDword(frame, shape + 8, 0x40);
        frame = PutDword(frame, shape + 0x18, 3);
        frame = PutWord(frame, state + 20, 0);
        frame = PutWord(frame, state + 56, 1);
        frame = PutWord(frame, state + 58, 23);
        for (uint index = 0; index < 2; index++)
        {
            uint face = shape + 0x44 + index * 20;
            frame = PutWord(frame, face + 4, 0x0201);
            frame = PutWord(frame, face + 6, 0x0403);
            frame = PutWord(frame, face + 8, 0x0605);
            frame = PutWord(frame, face + 0xA, 0);
            frame = PutWord(frame, face + 0xC, 8);
            frame = PutWord(frame, face + 0xE, 16);
            frame = PutWord(frame, face + 0x10, 0x1234);
            frame = PutWord(frame, face + 0x12, 0x0102);
        }
        int projected = 0;
        int emitted = 0;
        int shadeCalls = 0;
        int detached = 0;
        NativeBattleApocalypse221BreakupTriangleServices services = new(owner =>
        {
            owner = owner.WriteTemporary(0xE0, new byte[32]);
            return owner with { TemporaryFrame = owner.TemporaryFrame with { Known = owner.TemporaryFrame.Known.SetItem(0xF2, false).SetItem(0xF3, false) } };
        }, (owner, source) =>
        {
            Assert.Equal(vertices + (uint)(8 * projected), source);
            projected++;
            return new(owner, (uint)(0x10000 + projected), (ushort)(100 * projected));
        }, (owner, material) => owner, (owner, face) =>
        {
            Assert.Equal(shape + 0x44, face);
            shadeCalls++;
            return new(owner, 0xAB010203);
        }, (owner, record) =>
        {
            detached++;
            Assert.Equal(state + 56, record);
            Assert.Equal(24, Word(owner, record + 2));
            return owner;
        }, owner => new(owner, [11, 22, 33], 401), index =>
        {
            Assert.Equal(23, index);
            return 0x26998877;
        }, (owner, output, ordering, batch) =>
        {
            Assert.Equal(basis + 0x5000u + (uint)(emitted * 32), output);
            Assert.Equal(emitted == 0 ? 0x1030u : 0x1064u, ordering);
            NativeBattleGfGeometryAttempt packet = Assert.Single(batch.Attempts);
            Assert.Equal(32, batch.AllocationBytes);
            Assert.Equal(emitted == 0 ? 0x26010203u : 0x26998877u, packet.Primitive!.Words[1]);
            Assert.Equal(emitted == 0 ? new[] { 0x10002, 0x10003, 300, 0 } : new[] { 22, 33, 401, 0 }, packet.Primitive.VertexPayload4.ToArray());
            Assert.Equal(0x12340201u, packet.Primitive.Words[3]);
            Assert.Equal(0x01220403u, packet.Primitive.Words[5]);
            Assert.Equal(0x0605u, packet.Primitive.Words[7]);
            foreach (int offset in new[] { 0, 1, 2, 30, 31 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[offset]);
            }
            Assert.Equal(27, packet.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            emitted++;
            return owner;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221BreakupTriangles.Draw(frame, state, vertices, services);
        Assert.Equal(3, projected);
        Assert.Equal(2, emitted);
        Assert.Equal(1, shadeCalls);
        Assert.Equal(1, detached);
        Assert.Equal((ushort)401, result.ReadTemporaryWord(0xC0));
        Assert.False(result.TemporaryFrame.Known[0x52]);
        Assert.False(result.TemporaryFrame.Known[0x53]);
        Assert.Equal(basis + 0x5040, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), 2));
    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, uint address, int value) => frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), BitConverter.GetBytes(unchecked((ushort)value))) };
    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), BitConverter.GetBytes(value)) };
}
