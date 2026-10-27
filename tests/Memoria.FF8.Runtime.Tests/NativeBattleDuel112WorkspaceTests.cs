using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112WorkspaceTests
{
    [Theory]
    [InlineData(180, 0x12C7420u, 192)]
    [InlineData(372, 0x12C8020u, 272)]
    [InlineData(644, 0x12CA7B4u, 824)]
    [InlineData(1468, 0x12CB40Cu, 848)]
    [InlineData(2316, 0x12D19F4u, 268)]
    [InlineData(2584, 0x12D4684u, 896)]
    [InlineData(3480, 0x12D8D28u, 224)]
    [InlineData(3704, 0x12D9DC0u, 208)]
    public void InlineWorkspaceHasOnePhysicalTaskOwnerAndRetainsFrameAcrossSnapshot(int offset, uint source, int size)
    {
        byte[] bytes = new byte[NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address];
        int relative = checked((int)(source - NativeBattleDuel112Resources.Address));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(relative + 4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(relative + 20), 2);
        NativeBattleDuel112Resources resources = new(bytes.ToImmutableArray(), [0, 0, 0, 0], [], []);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
        (target, _) = target.Append(0x6CBF90);
        NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(target.Slots[0], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xCC, 0xF48 - 12).ToImmutableArray() });
        task = task.WriteResourcePointer(16, new(new(0, 0x123), null));
        task = task.MarkPayloadUnknown(offset, size);
        target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, task) });
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, target], DrawArenaOffset = 0x1E88 };
        NativeBattleDuel112Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Duel112 = new(true, parameters, resources) }, effect, NativeBattleFileStaging.Unknown(), new(1));
        frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, 0, offset);
        task = frame.Effect.Pools[1].Slots[0];
        Assert.Equal(source, task.ReadDword(offset));
        Assert.Equal(0u, task.ReadDword(offset + 4));
        Assert.All(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(offset - 12 + 8, size - 8).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0xCC, task.ReadByte(offset - 1));
        if (offset + size < NativeBattleStorageSnapshots.Capture(task).Payload.Length + 12)
        {
            Assert.Equal((byte)0xCC, task.ReadByte(offset + size));
        }
        else
        {
            Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload.Length + 12, offset + size);
        }
        Assert.Null(NativeBattleStorageSnapshots.Capture(task).UnknownPayloadRanges);
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), task.ReadResourcePointer(16).Cpu);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel112Frame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleDuel112WorkspaceStep step = NativeBattleDuel112Workspaces.Advance701970(frame, 0, offset, new(0, 0, null, null, null), null!, null!, null!, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(1u, step.Frame.Effect.Pools[1].Slots[0].ReadDword(offset + 4));
        Assert.Empty(step.Ordering.Packets);
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), step.Frame.Effect.Pools[1].Slots[0].ReadResourcePointer(16).Cpu);
    }
}
