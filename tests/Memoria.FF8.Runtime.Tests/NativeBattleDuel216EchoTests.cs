using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216EchoTests
{
    [Fact]
    public void EighteenNativeSelectorsCopyFullRowsIntoSeparate1152ByteHistories()
    {
        int offset = NativeBattleDuel216Echo.Offset;
        NativeBattleModulePoolTask task = NativeBattleDuel216Echo.Initialize695AB0(new(1, 0, null, 0x6960E0, new byte[6116].ToImmutableArray()));
        List<int> copied = [];
        task = NativeBattleDuelEcho.Capture697850(task, offset, 24, bone =>
        {
            copied.Add(bone);
            return Matrix(bone);
        });
        Assert.Equal(NativeBattleDuel216Echo.SelectedBones.Select(value => (int)value), copied);
        Assert.Equal((byte)18, task.ReadByte(offset + 4));
        Assert.Equal(22u, task.ReadDword(offset + 136 + 17 * 32 + 20));
        task = task.WriteDword(offset + 136 + 576, 0xAABBCCDD);
        task = NativeBattleDuelEcho.Capture697850(task, offset, 24, bone => Matrix(bone + 100));
        Assert.Equal(0xAABBCCDDu, task.ReadDword(offset + 136 + 576));
        Assert.Equal(122u, task.ReadDword(offset + 136 + 1152 + 17 * 32 + 20));
        Assert.Equal((byte)0, task.ReadByte(offset + 2));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(offset + 136 + 1152 + 18));
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        Assert.Equal(JsonSerializer.Serialize(task), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void DrawPreparesAllEighteenRowsAt576WithoutOverwritingCapturedRowsOrPadding()
    {
        int offset = NativeBattleDuel216Echo.Offset;
        NativeBattleModulePoolTask task = NativeBattleDuel216Echo.Initialize695AB0(new(1, 0, null, 0x6960E0, new byte[6116].ToImmutableArray()));
        task = NativeBattleDuelEcho.Capture697850(task, offset, 24, Matrix);
        task = task.MarkPayloadUnknown(offset + 136 + 576 + 18, 2);
        Frame frame = new(task, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()));
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = identity, Color40 = 0x303030, SequenceState = new() { MeshMask124 = 0 } };
        frame = NativeBattleDuelEchoDrawing.Draw697520(frame, model, checked(0x20DFAB8u + (uint)offset), offset, current => current.Task, (current, updated, _) => current with { Task = updated }, 0x1000, (_, _, length) => new byte[length], 1, identity, new(identity, 256, 160, 112), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging });
        Assert.Equal(22u, frame.Task.ReadDword(offset + 136 + 17 * 32 + 20));
        Assert.Equal(22u, frame.Task.ReadDword(offset + 136 + 576 + 17 * 32 + 20));
        Assert.Throws<NotSupportedException>(() => frame.Task.ReadWord(offset + 136 + 576 + 18));
        Assert.Equal((byte)1, frame.Task.ReadByte(offset + 1));
        Assert.Equal((byte)0, frame.Task.ReadByte(offset));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private static (byte[] Data, bool[] Known) Matrix(int value)
    {
        byte[] bytes = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(lane * 2), (short)(lane % 4 == 0 ? 4096 : 0));
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), value);
        bool[] known = Enumerable.Repeat(true, 32).ToArray();
        known[18] = known[19] = false;
        return (bytes, known);
    }

    private sealed record Frame(NativeBattleModulePoolTask Task, NativeBattleFileStaging Staging);
}
