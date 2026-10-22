using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8StateTests
{
    [Fact]
    public void Fresh_loader_globals_are_zero_but_imported_unknowns_are_not_hydrated()
    {
        NativeBattleDoom8State cold = NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray());
        Assert.Equal(0u, cold.ReadDword(0x2758500));
        Assert.Equal(0x1818B78u, cold.NodeProgram!.ResourceAddress);
        Assert.Equal(0u, cold.NodeProgram.InitializationFlag);
        Assert.Null(cold.NodeProgram.Data);
        Assert.Equal((ushort)0, cold.SharedWord(0x40));
        NativeBattleDoom8State unknown = NativeBattleDoom8State.FromUnknown();
        Assert.Null(unknown.NodeProgram);
        Assert.Throws<NotSupportedException>(() => unknown.SharedWord(0x40));
        unknown = unknown.WithSharedWord(0x40, 2);
        Assert.Equal((ushort)2, unknown.SharedWord(0x40));
        Assert.Throws<NotSupportedException>(() => unknown.SharedWord(0x42));
        Assert.Throws<NotSupportedException>(() => unknown.InitializeIo());
    }

    [Fact]
    public void Physical_task_banks_and_descriptors_cannot_be_serialized_as_global_bytes()
    {
        NativeBattleDoom8State state = NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray());
        foreach (NativeBattleCureFamilyPool pool in NativeBattleDoom8State.Pools)
        {
            Assert.Throws<InvalidDataException>(() => state.Read(pool.Bank, 1));
            Assert.Throws<InvalidDataException>(() => state.Write(pool.Descriptor, new byte[16]));
        }
        state = state.WriteDword(0x275F3E8, 17);
        Assert.Equal(17u, state.ReadDword(0x275F3E8));
        Assert.Throws<InvalidDataException>(() => state.Read(NativeBattleDoom8State.BaseAddress - 1, 1));
        Assert.Throws<InvalidDataException>(() => state.Write(0x275FB7B, new byte[2]));
    }

    [Fact]
    public void IO_views_publish_one_control_cursor_script_owner_and_roundtrip_live_bytes()
    {
        byte[] script = new byte[112];
        script[28] = 0xAB;
        NativeBattleDoom8State state = NativeBattleDoom8State.FromColdProcess(script.ToImmutableArray()).WriteDword(NativeBattleDoom8Io.NextAddressGlobal, 0x20FFAB8).InitializeIo();
        var released = NativeBattleDoom8Io.Release8C5550(state.ReadIo());
        state = state.WithIo(released.State);
        Assert.True(released.Released);
        Assert.Equal(0x1636C88u, state.ReadDword(NativeBattleDoom8Io.CursorGlobal));
        Assert.Equal(0x275FA10u, state.ReadDword(NativeBattleDoom8Io.ControlPointerGlobal));
        Assert.Equal(0x2109AB8u, state.ReadDword(NativeBattleDoom8Io.NextAddressGlobal));
        Assert.Equal((ushort)1, state.ReadIoControlWord(0x166));
        Assert.Equal((byte)0xAB, state.IoScript!.Value[28]);
        NativeBattleDoom8State restored = JsonSerializer.Deserialize<NativeBattleDoom8State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(state.ReadIo().Control.ToArray(), restored.ReadIo().Control.ToArray());
        Assert.Equal(state.ReadIo().Script.ToArray(), restored.ReadIo().Script.ToArray());
        Assert.Equal(state.ReadIo().CursorAddress, restored.ReadIo().CursorAddress);
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8State.FromColdProcess(new byte[111].ToImmutableArray()));
    }
}
