using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelEchoDrawingTests
{
    [Fact]
    public void EmptyHistoryStillPublishesAndReleasesTheActual116ByteCallerPreservingUnwrittenMemory()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(address, Enumerable.Repeat((byte)0xA5, 116).ToArray()).MarkUnknown(address + 18, 2).WriteResourcePointer(address + 32, new(new(0, 0x123), null, 20));
        NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0);
        NativeBattleDuel274Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, staging, new(1));
        uint echo = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
        frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, echo, []);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = identity, SequenceState = new() { MeshMask124 = 0x12345678 } };

        frame = NativeBattleDuel274Echo.Draw68A7B0(frame, model, echo, 0x1000, (_, _, _) => throw new InvalidOperationException("Empty history must not read geometry."), 1, identity, new(identity, 256, 160, 112), (_, _) => throw new InvalidOperationException("Empty history must not submit packets."), 0x155, 0x100);
        NativeBattleDuel274Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!;

        Assert.Equal(address, BitConverter.ToUInt32(restored.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(0x12345678u, BitConverter.ToUInt32(restored.Staging.Read(address + 56, 4)));
        Assert.Equal(echo, BitConverter.ToUInt32(restored.Staging.Read(address + 108, 4)));
        Assert.Equal((ushort)320, BitConverter.ToUInt16(restored.Staging.Read(address + 48, 2)));
        Assert.Equal((byte)0xA5, restored.Staging.Read(address + 60, 1)[0]);
        Assert.Throws<NotSupportedException>(() => restored.Staging.Read(address + 18, 2));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), restored.Staging.ReadResourcePointer(address + 32)!.Cpu);
    }
}
