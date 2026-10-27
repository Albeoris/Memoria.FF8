using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattlePandemona291EntryTests
{
    [Fact]
    public void Body_constructor_publishes_real_directory_first_pose_and_only_native_sparse_task_writes()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
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
        uint address = 0x20DFAB8u + 0x21A4;
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6ED900, Enumerable.Repeat((byte)0xA5, 0x1024).ToImmutableArray());
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x31220, resource).Write(0x21A4 + 12, NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan()) } };
        int cleared = 0;
        NativeBattlePandemona291ChildStep result = NativeBattlePandemona291Body.Initialize99(frame, address, task, () => cleared++);
        Assert.Equal(1, cleared);
        Assert.Equal((ushort)2, result.Task.ReadWord(0x154));
        Assert.Equal(0xFFFFFFFFu, result.Task.ReadDword(0x1D0));
        Assert.Equal((ushort)6144, result.Task.ReadWord(0x194));
        Assert.Equal(unchecked((uint)-8192), result.Task.ReadDword(0x1AC));
        Assert.Equal((uint)3999, result.Task.ReadDword(0x1B0));
        Assert.Equal(address + 0x41C, result.Task.ReadDword(0x1B8));
        Assert.Equal(0x20DFAB8u + 0x31238, result.Task.ReadDword(0x41C));
        Assert.Equal((byte)4, result.Task.ReadByte(0x1C1));
        Assert.Equal((byte)1, result.Task.ReadByte(0x1C6));
        Assert.Equal((byte)2, result.Task.ReadByte(0x1C7));
        Assert.Equal((byte)0xA5, result.Task.ReadByte(0x1A6));
        Assert.Equal((byte)0xA5, result.Task.ReadByte(0x1B6));
        Assert.Equal((byte)0xA5, result.Frame.Memory.Arena!.Read(0x21A4 + 0x1A6, 1)[0]);
        Assert.Equal(frame.Random, result.Frame.Random);
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(result.Frame.Memory.Arena!, 0x31238, 1);
        Assert.Equal(new NativeBattleVector(0, 0, 0), pose.Translation);
        NativeBattlePandemona291Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePandemona291Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattlePandemona291Body.Initialize99(restored, address, task, () => { })));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Age99_draws_before_IO_gate_and_only_ready_IO_allows_camera_initialization_and_counter(bool busy)
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x231D4);
        if (busy)
        {
            memory = memory with { Arena = memory.Arena!.Write(0x231D4, new byte[] { 1 }) };
        }
        frame = frame with { Memory = memory };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6ED900, new byte[0x1024].ToImmutableArray());
        task = task.WriteWord(0xC, 99);
        List<string> calls = [];
        NativeBattlePandemona291TimelineServices services = new((current, request) =>
        {
            Assert.Equal((short)723, request.FileId);
            calls.Add("file");
            return current;
        }, (_, _) => throw new InvalidOperationException(), (current, _, _) =>
        {
            calls.Add("camera");
            return current;
        })
        {
            DrawVortex = (current, _, _) =>
            {
                calls.Add("draw");
                return current;
            },
            InitializeBody = (current, _, selected) =>
            {
                calls.Add("model");
                return new(current, selected, 0);
            }
        };
        NativeBattlePandemona291ChildStep result = NativeBattlePandemona291Timeline.Advance6ED900(frame, 0x20DFAB8u + 0x21A4, task, services);
        Assert.Equal(busy ? new[] { "draw" } : new[] { "draw", "file", "camera", "model" }, calls);
        Assert.Equal((ushort)(busy ? 99 : 100), result.Task.ReadWord(0xC));
    }

    [Fact]
    public void Body_camera_uses_exact_signed_words_and_retains_focal_and_roll()
    {
        Assert.Equal(new NativeBattleCameraPose(new(0, -1674, -458), new(0, -4511, 1900), 288, 17), NativeBattlePandemona291Timeline.FinalBodyCamera99(1900, 288, 17));
    }
}
