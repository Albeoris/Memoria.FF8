using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco98IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Initialization_preserves_current_script_and_publishes_only_the_actual98_directory()
    {
        ImmutableArray<byte> script = Script(3, 457, 1);
        NativeBattleChoco98IoInitialization initialized = NativeBattleChoco98Io.Initialize721CF0(script, Buffer, Banks());
        Assert.Equal(Buffer + 0xA000, initialized.NextAddress);
        Assert.Equal(script, initialized.State.Script);
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            uint expected = offset switch { 0x108 => 0x19DFEF8, 0x12C => 0x19E187C, 0x130 => 0x19E5584, 0x134 => 0x19E8E70, _ => 0x19DB018 };
            Assert.Equal(expected, U32(initialized.State.Control, offset));
        }
        Assert.Throws<NotSupportedException>(() => initialized.State.Banks.Read(0x19DB018, 1));
        NativeBattleChoco98IoAdvance waiting = NativeBattleChoco98Io.Advance721DF0(Task().WriteWord(0x24, 0xFFFF), initialized.State);
        Assert.Equal((ushort)0, waiting.Task.ReadWord(0x24));
        Assert.Null(waiting.Request);
        Assert.Equal(initialized.State.CursorAddress, waiting.State.CursorAddress);
        NativeBattleChoco98IoRelease released = NativeBattleChoco98Io.ReleaseWait(waiting.State);
        Assert.True(released.Released);
        Assert.False(NativeBattleChoco98Io.ReleaseWait(released.State).Released);
        Assert.Equal(initialized.State.CursorAddress + 16, released.State.CursorAddress);
    }

    [Theory]
    [InlineData(3, 1, 0x19DB018u, 0x722060u)]
    [InlineData(2, 1, 0x19DFEF8u, 0x722050u)]
    [InlineData(3, 2, 0x19E187Cu, 0x722060u)]
    [InlineData(3, 3, 0x19E5584u, 0x722060u)]
    [InlineData(3, 4, 0x19E8E70u, 0x722060u)]
    public void Data_requests_retain_exact_physical_destination_and_pending_JSON_completion(ushort opcode, ushort index, uint destination, uint callback)
    {
        NativeBattleChoco98IoState state = NativeBattleChoco98Io.ReleaseWait(NativeBattleChoco98Io.Initialize721CF0(Script(opcode, 457, index), Buffer, Banks()).State).State;
        NativeBattleChoco98IoAdvance begin = NativeBattleChoco98Io.Advance721DF0(Task(), state);
        NativeBattleChoco98IoRequest request = Assert.IsType<NativeBattleChoco98IoRequest>(begin.Request);
        Assert.Equal(destination, request.DestinationAddress);
        Assert.Equal(callback, request.CompletionCallback);
        Assert.Equal((short)1, begin.State.RangeCount);
        Assert.Equal(destination, U32(begin.State.Control, 0));
        Assert.Equal(destination + 16, U32(begin.State.Control, 0x80));
        NativeBattleEffectFileAsset file = new(457, "declared", Enumerable.Repeat((byte)0xA5, 16).ToImmutableArray());
        NativeBattleChoco98IoQueued queued = NativeBattleChoco98Io.CopyAndQueueFile(begin.State, NativeBattleEffectMemory.FromColdProcess().BeginLoad(98), NativeBattleFileIoState.FromColdProcess(), file);
        Assert.Equal(file.Data, queued.State.Banks.Read(destination, 16));
        Assert.Equal((ushort)1, queued.State.Busy);
        Assert.Equal(0, queued.Memory.Textures!.Count);
        NativeBattleChoco98IoState cold = JsonSerializer.Deserialize<NativeBattleChoco98IoState>(JsonSerializer.Serialize(queued.State))!;
        NativeBattleChoco98IoCompletion actual = NativeBattleChoco98Io.Complete(queued.State, queued.Memory, callback);
        NativeBattleChoco98IoCompletion restored = NativeBattleChoco98Io.Complete(cold, queued.Memory, callback);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)0, actual.State.Busy);
        Assert.Equal(begin.State.CursorAddress, actual.State.CursorAddress);
        Assert.Equal(0, actual.Memory.Textures!.Count);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco98Io.Complete(queued.State, queued.Memory, 0x721FF0));
        NativeBattleChoco98IoAdvance polled = NativeBattleChoco98Io.Advance721DF0(begin.Task, actual.State);
        Assert.Equal(begin.State.CursorAddress + 16, polled.State.CursorAddress);
    }

    [Fact]
    public void WORD_selector_preserves_unconsumed_high_WORD_and_terminal_completion_waits_for_children()
    {
        byte[] bytes = Script(1, 0xF123, 0xE234).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 0xBEEF);
        NativeBattleChoco98IoState state = NativeBattleChoco98Io.ReleaseWait(NativeBattleChoco98Io.Initialize721CF0(bytes.ToImmutableArray(), Buffer, Banks()).State).State;
        NativeBattleChoco98IoAdvance begin = NativeBattleChoco98Io.Advance721DF0(Task(), state);
        Assert.Equal((ushort)0xF123, begin.Request!.FileId);
        Assert.Equal((ushort)0xE234, begin.Request.Index);
        Assert.Equal(Buffer, begin.Request.DestinationAddress);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(begin.State.Script.AsSpan().Slice(22)));
        NativeBattleChoco98IoState terminal = state with { CursorAddress = NativeBattleChoco98IoState.ScriptAddress + 0x110 };
        Assert.True(NativeBattleChoco98Io.Advance721DF0(Task(), terminal).ReadyToComplete);
        Assert.False(NativeBattleChoco98Io.Advance721DF0(Task().WriteByte(0x28, 1), terminal).ReadyToComplete);
        byte[] unsupported = terminal.Script.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(unsupported.AsSpan(0x110), 4);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Io.Advance721DF0(Task(), terminal with { Script = unsupported.ToImmutableArray() }));
    }

    private static NativeBattleChoco97IoBanks Banks() => new(0x19DB018, 0x13184, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x13184));
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x721DF0, new byte[0x30 - 12].ToImmutableArray());
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static ImmutableArray<byte> Script(ushort opcode, ushort id, ushort index)
    {
        byte[] bytes = new byte[0x120];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), id);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), index);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 0xAABBCCDD);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x110), 0xFF);
        return bytes.ToImmutableArray();
    }
}
