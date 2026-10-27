using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325BodyTests
{
    [Fact]
    public void Constructor_uses_actual_sparse_task_offsets_and_regular_pose_reads_current_skeleton_then_restarts()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        byte[] resource = new byte[0x6A];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(4 + index * 4), new[] { 0x18, 0x58, 0x5C, 0x6A }[index]);
        }
        resource[0x18] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(0x1A), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(0x28), -1);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(0x5C), 1);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(0x60), 8);
        resource[0x64] = 2;
        NativeBattleEffectMemory memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x2185C, resource) };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, Enumerable.Repeat((byte)0xA5, 0x994 - 12).ToImmutableArray());
        var preserved = NativeBattleStorageSnapshots.Capture(memory);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325BodyStep initialized = NativeBattleDiablos325Body.Initialize655190(memory, task, 0x20DFAB8);
        Assert.Equal(unchecked((uint)-8192), initialized.Task.ReadDword(0x164));
        Assert.Equal((ushort)2, initialized.Task.ReadWord(0x10C));
        Assert.Equal((ushort)4096, initialized.Task.ReadWord(0x14C));
        Assert.Equal((byte)0xA5, initialized.Task.ReadByte(0x15E));
        Assert.Equal((byte)0xA5, initialized.Task.ReadByte(0x16E));
        Assert.Equal(0x20DFAB8u + 0x244, initialized.Task.ReadDword(0x170));
        Assert.Equal(0x20DFAB8u + 0x21874, initialized.Task.ReadDword(0x244));
        Assert.Equal(0x20DFAB8u + 0x218B8, initialized.Task.ReadDword(0x24C));
        Assert.Equal((byte)1, initialized.Task.ReadByte(0x17E));
        Assert.Equal((byte)4, initialized.Task.ReadByte(0x179));
        NativeBattleEffectArena changed = NativeBattleArenaPose.WriteVector(initialized.Memory.Arena!, 0x2187C, new(123, -456, 789));
        NativeBattleDiablos325BodyStep next = NativeBattleDiablos325Body.Advance6FBDB0(initialized.Memory with { Arena = changed }, initialized.Task);
        Assert.Equal(new NativeBattleVector(123, -456, 789), NativeBattleArenaPose.ReadCurrentPose(next.Memory.Arena!, 0x21874, 1).Translation);
        Assert.Equal((byte)2, next.Task.ReadByte(0x17E));
        Assert.Equal((byte)0, next.Task.ReadByte(0x179));
        NativeBattleDiablos325BodyStep restarted = NativeBattleDiablos325Body.Advance6FBDB0(next.Memory, next.Task);
        Assert.Equal((byte)1, restarted.Task.ReadByte(0x17E));
        Assert.Equal((byte)4, restarted.Task.ReadByte(0x179));
        Assert.Equal(new NativeBattleVector(0, 0, 0), NativeBattleArenaPose.ReadCurrentPose(restarted.Memory.Arena!, 0x21874, 1).Translation);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }
}
