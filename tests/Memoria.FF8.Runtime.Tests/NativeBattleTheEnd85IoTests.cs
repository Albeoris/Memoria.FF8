using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85IoTests
{
    [Fact]
    public void Initialization_preserves_retained_script_and_bounded_original_directory_overrides()
    {
        NativeBattleTheEnd85Frame frame = Initialize();
        NativeBattleTheEnd85State state = frame.Memory.TheEnd85!;
        Assert.Equal(0x1571E70u, state.Dword(0x25C1B60));
        Assert.Equal(0x25C21E0u, state.Dword(0x25C1C58));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2A000, state.Dword(0x25C21D8));
        Assert.Equal(0x19CF018u, state.Dword(0x25C21E0 + 0x104));
        Assert.Equal(0x19EB018u, state.Dword(0x25C21E0 + 0x114));
        Assert.Equal(0x19D2560u, state.Dword(0x25C21E0 + 0x12C));
        byte[] retained = state.Script!.Value.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(retained.AsSpan(28), 0x12345678);
        frame = frame with { Memory = frame.Memory with { TheEnd85 = state with { Script = retained.ToImmutableArray() } } };
        NativeBattleTheEnd85Frame next = NativeBattleTheEnd85Io.Initialize784960(frame, Script());
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(next.Memory.TheEnd85!.Script!.Value.AsSpan().Slice(28)));
    }

    [Fact]
    public void Actual_file_copy_has_distinct_busy_phase_and_retains_bytes_outside_the_received_length()
    {
        NativeBattleTheEnd85Frame frame = Initialize();
        NativeBattleTheEnd85State state = frame.Memory.TheEnd85!.WriteDword(0x25C1B60, 0x1571E80);
        frame = frame with { Memory = frame.Memory with { TheEnd85 = state, Arena = frame.Memory.Arena!.Write(0x20000, Enumerable.Repeat((byte)0xA5, 16).ToArray()) } };
        NativeBattleTheEnd85IoAdvance begun = NativeBattleTheEnd85Io.Advance784A60(frame, Task());
        Assert.Equal((ushort)402, begun.Request!.FileId);
        Assert.Equal(0x784C60u, begun.Request.CompletionCallback);
        Assert.Equal((ushort)1, begun.Frame.Memory.TheEnd85!.Word(0x25C21E0 + 356));
        NativeBattleTheEnd85Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleTheEnd85Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(begun.Frame)))!);
        NativeBattleEffectFileAsset file = new(402, "input", [1, 2, 3, 4]);
        NativeBattleTheEnd85IoQueued queued = NativeBattleTheEnd85Io.CopyAndQueue(begun.Frame, NativeBattleFileIoState.FromColdProcess(), file);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(queued)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleTheEnd85Io.CopyAndQueue(restored, NativeBattleFileIoState.FromColdProcess(), file))));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 0xA5 }, queued.Frame.Memory.Arena!.Read(0x20000, 5));
        Assert.Equal(4, queued.FileIo.Rows[0].FileResult);
        Assert.Equal((byte)1, NativeBattleTheEnd85Io.Advance784A60(queued.Frame, begun.Task).Task.ReadByte(41));
    }

    [Fact]
    public void Cold_owner_and_missing_JSON_script_are_distinct_from_an_initialized_script()
    {
        NativeBattleTheEnd85State state = NativeBattleTheEnd85State.FromColdProcess();
        Assert.Null(state.Script);
        Assert.Equal(new byte[NativeBattleTheEnd85State.GlobalLength], state.Read(NativeBattleTheEnd85State.GlobalAddress, NativeBattleTheEnd85State.GlobalLength));
        Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleTheEnd85State>("{\"Globals\":{\"Regions\":[],\"UnknownRegions\":[]}}")));
        Assert.Throws<InvalidDataException>(() => state.Read(0x25C234B, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleTheEnd85Io.Advance784A60(new(NativeBattleEffectMemory.FromColdProcess(), Effect(), new(1)), Task()));
        NativeBattleTheEnd85Frame initialized = Initialize();
        NativeBattleTheEnd85Frame missingScript = initialized with { Memory = initialized.Memory with { TheEnd85 = initialized.Memory.TheEnd85! with { Script = null } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleTheEnd85Io.Advance784A60(missingScript, Task()));
    }

    private static NativeBattleTheEnd85Frame Initialize()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), TheEnd85 = NativeBattleTheEnd85State.FromColdProcess().WriteDword(0x25C21D8, NativeBattleEffectNodeMemory.ArenaAddress + 0x20000) };
        return NativeBattleTheEnd85Io.Initialize784960(new(memory, Effect(), new(1)), Script());
    }
    private static NativeBattleOriginalEffectState Effect() => new(85, 85, new(85, 0x7844A0, 0x784480), null, null);
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x784A60, new byte[36].ToImmutableArray());
    private static ImmutableArray<byte> Script()
    {
        byte[] bytes = new byte[80];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 402);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 0xFF);
        return bytes.ToImmutableArray();
    }
}
