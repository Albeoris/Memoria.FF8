using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelEchoTests
{
    [Fact]
    public void RingCopiesOnlyActualSelectedBonesAndPreservesUnknownMatrixPadding()
    {
        NativeBattleModulePoolTask task = Task();
        task = NativeBattleDuelEcho.Initialize6A1230(task, 336, [14, 18, 22]);
        List<int> copied = [];
        task = NativeBattleDuelEcho.Capture68ADF0(task, 336, 24, bone =>
        {
            copied.Add(bone);
            byte[] bytes = new byte[32];
            bytes[20] = checked((byte)bone);
            bool[] known = Enumerable.Repeat(true, 32).ToArray();
            known[18] = known[19] = false;
            return (bytes, known);
        });
        Assert.Equal(new[] { 14, 18, 22 }, copied);
        Assert.Equal(472, NativeBattleDuelEcho.ReadHistoryOffset(task, 336, 0));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(472 + 18));
        Assert.Equal(18, NativeBattleDuelEcho.ReadHistoryMatrix(task, 336, 0, 18).X);
        Assert.Equal((byte)1, task.ReadByte(337));
        Assert.Equal((byte)1, task.ReadByte(338));
    }

    [Fact]
    public void RingWrapAndDrawAcknowledgmentRetainNativeCountdownAcrossSnapshot()
    {
        NativeBattleModulePoolTask task = NativeBattleDuelEcho.Initialize6A1230(Task(), 336, [14]);
        task = NativeBattleDuelEcho.Capture68ADF0(task, 336, 15, Matrix);
        task = NativeBattleDuelEcho.CompleteDraw6A2760(task, 336);
        Assert.Equal((byte)1, task.ReadByte(337));
        task = NativeBattleDuelEcho.Capture68ADF0(task, 336, 15, Matrix);
        Assert.Equal((byte)0, task.ReadByte(338));
        Assert.Equal((byte)2, task.ReadByte(337));
        Assert.Equal(984, NativeBattleDuelEcho.ReadHistoryOffset(task, 336, 0));
        Assert.Equal(472, NativeBattleDuelEcho.ReadHistoryOffset(task, 336, 1));
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleDuelEcho.CompleteDraw6A2760(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task)), 336)), NativeBattleTestSnapshots.Json(NativeBattleDuelEcho.CompleteDraw6A2760(restored, 336)));
        task = NativeBattleDuelEcho.CompleteDraw6A2760(task, 336);
        Assert.Equal((byte)2, task.ReadByte(337));
        task = NativeBattleDuelEcho.CompleteDraw6A2760(task, 336);
        Assert.Equal((byte)1, task.ReadByte(337));
        task = NativeBattleDuelEcho.CompleteDraw6A2760(task, 336);
        Assert.Equal((byte)0, task.ReadByte(337));
    }

    private static (byte[] Data, bool[] Known) Matrix(int bone) => (new byte[32], Enumerable.Repeat(true, 32).ToArray());
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x6A1660, ImmutableArray.CreateRange(new byte[3268]));
}
