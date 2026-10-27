using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274ArenaEchoTests
{
    private const uint Address = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;

    [Fact]
    public void InitializationWritesOnlyNativeHeaderAndMapKeepingUntouchedHistoryUnknownsAndPointerTags()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x4000 + 133, 3).MarkUnknown(0x4000 + 600, 2).WriteResourcePointer(0x4000 + 200, new(new(0, 0x123), null, 20)).WriteResourcePointer(0x4000 + 16, new(new(0, 0x456), null, 20));
        NativeBattleDuel274Frame frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(Frame(arena), Address, [9]);
        NativeBattleModulePoolTask task = NativeBattleDuel274ArenaEcho.Read(frame, Address);
        Assert.Equal(1, task.ReadByte(16));
        Assert.Equal(0, task.ReadByte(26));
        Assert.Equal(255, task.ReadByte(25));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), frame.Memory.Arena!.ReadResourcePointer(0x4000 + 200)!.Cpu);
        Assert.Null(frame.Memory.Arena.ReadResourcePointer(0x4000 + 16)!.Cpu);
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(0x4000 + 133, 1));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(0x4000 + 600, 1));
        NativeBattleDuel274Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(frame.Memory.Arena.ReadResourcePointer(0x4000 + 200), restored.Memory.Arena!.ReadResourcePointer(0x4000 + 200));
    }

    [Fact]
    public void CaptureCopiesExactlySelectedMatrixDwordsAndUnknownPaddingIntoTheActualArenaRing()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(0x4000 + 400, new(new(0, 0x123), null, 20));
        NativeBattleDuel274Frame frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(Frame(arena), Address, [9]);
        int reads = 0;
        frame = NativeBattleDuel274ArenaEcho.Capture68ADF0(frame, Address, 10, bone =>
        {
            Assert.Equal(9, bone);
            reads++;
            byte[] data = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            bool[] known = Enumerable.Repeat(true, 32).ToArray();
            known[18] = false;
            known[19] = false;
            return (data, known);
        });
        Assert.Equal(1, reads);
        Assert.Equal(new byte[] { 1, 1, 1, 0 }, frame.Memory.Arena!.Read(0x4000, 4));
        Assert.Equal(Enumerable.Range(0, 18).Select(value => (byte)value).ToArray(), frame.Memory.Arena.Read(0x4000 + 136, 18));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(0x4000 + 154, 2));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), frame.Memory.Arena.ReadResourcePointer(0x4000 + 400)!.Cpu);
        Assert.Equal(new byte[32], frame.Memory.Arena.Read(0x4000 + 648, 32));
    }

    private static NativeBattleDuel274Frame Frame(NativeBattleEffectArena arena) => new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0), NativeBattleFileStaging.Unknown(), new(1));
}
