using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22OverlayTests
{
    [Fact]
    public void PrivatePacketsUseActualRectangleOrderAndRetainedProxyMetadata()
    {
        NativeBattleBio22OverlayStep step = Advance(Task(), Program(0x00123456, 0xFF000000));
        Assert.Equal(36, step.Packets.AllocationBytes);
        Assert.Equal(new uint[] { 0x05000000, 0x2A123456, 0, 320, 0x00D80000, 0x00D80140 }, step.Packets.Attempts[0].Primitive!.Words);
        Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, step.Packets.Attempts[1].Primitive!.Words);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(memory, 0x100, NativeBattleLogicalOrdering.Clear(), step.Packets);
        Assert.Equal(0x124, write.Cursor);
        Assert.Equal(0, write.Memory.Cursor);
        Assert.Equal(2, write.Ordering.ProxyCursor);
        Assert.Equal(new[] { 11, 22, 33, 44 }, write.Ordering.Proxies[0].VertexPayload4);
        Assert.Empty(write.Ordering.Proxies[1].VertexPayload4);
        Assert.All(write.Ordering.Proxies, proxy => Assert.Equal((ushort)0, proxy.Flags20));
        NativeBattlePrimitiveLink head = write.Ordering.Table[7];
        Assert.True(head.Proxy);
        Assert.Equal(0x118, write.Ordering.Proxies[head.Index].Packet.ByteOffset);
        NativeBattlePrimitiveLink next = write.Ordering.Packets.Single(packet => packet.Reference.ByteOffset == 0x118).Next;
        Assert.True(next.Proxy);
        Assert.Equal(0x100, write.Ordering.Proxies[next.Index].Packet.ByteOffset);
    }

    [Fact]
    public void SaturationStillClearsFullF4TagButRetainsUnknownE1LinkBytes()
    {
        NativeBattleLogicalProxy proxy = new(new("retained", 0, 0), 0, [], null);
        NativeBattleLogicalOrdering exhausted = NativeBattleOrderingTestFixture.Working(NativeBattleLogicalOrdering.Clear(), data => data with { Proxies = Enumerable.Repeat(proxy, NativeBattlePrimitiveOrdering.ProxyCapacity).ToImmutableArray(), ProxyCursor = NativeBattlePrimitiveOrdering.ProxyCapacity });
        NativeBattleEffectMemory memory = Memory() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x100, 36) };
        NativeBattleBio22OverlayStep step = Advance(Task(), Program(0x00010203));
        NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(memory, 0x100, exhausted, step.Packets);
        Assert.Equal(0x05000000u, BinaryPrimitives.ReadUInt32LittleEndian(write.Memory.Arena!.Read(0x100, 4)));
        Assert.Throws<NotSupportedException>(() => write.Memory.Arena.Read(0x118, 3));
        Assert.Equal(new byte[] { 2, 0x2A, 0, 0, 0xE1, 0, 0, 0, 0 }, write.Memory.Arena.Read(0x11B, 9));
        Assert.Equal(0x124, write.Cursor);
        Assert.Equal(exhausted.ProxyCursor, write.Ordering.ProxyCursor);
    }

    [Fact]
    public void ThresholdUsesSignedCounterAndFollowingRecordIsNotRecursivelyExecuted()
    {
        NativeBattleEffectHeapAllocation program = Program(0xFE020000, 0xFF123456);
        NativeBattleModulePoolTask task = Task().WriteDword(0x34, 0x00445566).WriteWord(0x24, 0xFFFF);
        NativeBattleBio22OverlayStep held = Advance(task, program);
        Assert.Equal((ushort)0, held.Task.ReadWord(0x24));
        Assert.Equal((ushort)0, held.Task.ReadWord(0x38));
        Assert.Equal(0x00445566u, held.Task.ReadDword(0x34));
        NativeBattleBio22OverlayStep loaded = Advance(held.Task.WriteWord(0x24, 2), program);
        Assert.Equal((ushort)1, loaded.Task.ReadWord(0x38));
        Assert.Equal(0u, loaded.Result);
        Assert.Equal(0x2A123456u, loaded.Packets.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(2u, Advance(loaded.Task, program).Result);
    }

    [Fact]
    public void TerminalDefersDrawInputsAndWaitsForActualChildrenWithCounterWrap()
    {
        NativeBattleEffectHeapAllocation program = Program(0xFF000000);
        NativeBattleBio22OverlayStep held = NativeBattleBio22Overlay.Advance89C650(Task().WriteByte(0x28, 1).WriteWord(0x24, 0xFFFF), program, _ => throw new Exception("Terminal must not read proxy metadata."));
        Assert.Equal(0u, held.Result);
        Assert.Equal((ushort)0, held.Task.ReadWord(0x24));
        Assert.Equal((byte)1, held.Task.ReadByte(0x26));
        Assert.Empty(held.Packets.Attempts);
        Assert.Equal(0, held.Packets.AllocationBytes);
        Assert.Equal(2u, Advance(held.Task.WriteByte(0x28, 0), program).Result);
        Assert.Throws<InvalidDataException>(() => Advance(Task().WriteWord(0x38, 0xFFFF), program));
    }

    [Fact]
    public void IndependentJsonPreservesNextColorAndTerminalBoundary()
    {
        NativeBattleEffectHeapAllocation program = Program(0x00112233, 0x00445566, 0x00778899, 0xFF000000);
        NativeBattleModulePoolTask task = Task();
        for (int callback = 0; callback < 4; callback++)
        {
            NativeBattleBio22OverlayStep expected = Advance(task, program);
            NativeBattleBio22OverlayStep restored = Advance(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!, JsonSerializer.Deserialize<NativeBattleEffectHeapAllocation>(JsonSerializer.Serialize(program))!);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restored));
            Assert.Equal(callback == 3 ? 0 : 36, expected.Packets.AllocationBytes);
            Assert.Equal(callback == 3 ? 2u : 0u, expected.Result);
            task = expected.Task;
        }
    }

    private static NativeBattleBio22OverlayStep Advance(NativeBattleModulePoolTask task, NativeBattleEffectHeapAllocation program) => NativeBattleBio22Overlay.Advance89C650(task, program, index => index == 0 ? [11, 22, 33, 44] : []);
    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 0x89C650, new byte[0x3C - 12].ToImmutableArray()).WriteDword(0x30, 0x162693C);
    private static NativeBattleEffectMemory Memory() => new(NativeBattleEffectArena.Clear(), 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0);
    private static NativeBattleEffectHeapAllocation Program(params uint[] values)
    {
        byte[] bytes = new byte[16];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), values[index]);
        }
        return new("FF8_EN:162693C", bytes.ToImmutableArray());
    }
}
