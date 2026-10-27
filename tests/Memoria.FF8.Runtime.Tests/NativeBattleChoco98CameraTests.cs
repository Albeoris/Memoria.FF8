using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco98CameraTests
{
    private static ImmutableArray<byte> Actor => [10, 0, 20, 0, 30, 0, 40, 0];
    private static ImmutableArray<byte> Target => [50, 0, 60, 0, 70, 0, 80, 0];

    [Fact]
    public void Nine_phases_keep_ordered_publications_and_second_target_initialization_over_the_explicit_curve()
    {
        NativeBattleChoco98CameraResources resources = Resources();
        NativeBattleModulePoolTask task = new(1, 0, null, NativeBattleChoco98CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray());
        NativeBattleChoco98CameraTaskStep step = Advance(task, null, 0);
        Assert.Equal((byte)1, step.Task.ReadByte(0x29));
        Assert.Single(step.Publications);
        Assert.Equal(Actor.ToArray(), step.Camera.Data.AsSpan().Slice(0xA4, 8).ToArray());
        step = Advance(step.Task, step.Camera, 0);
        Assert.Equal((byte)2, step.Task.ReadByte(0x29));
        NativeBattleChoco98CameraTaskStep waiting = Advance(step.Task, step.Camera, -1);
        Assert.Empty(waiting.Publications);
        Assert.Equal(NativeBattleTestSnapshots.Json(step.Camera), NativeBattleTestSnapshots.Json(waiting.Camera));
        Assert.Equal((byte)2, waiting.Task.ReadByte(0x29));
        step = Advance(waiting.Task, waiting.Camera, 2);
        Assert.Equal((byte)3, step.Task.ReadByte(0x29));
        step = Advance(step.Task, step.Camera, 4);
        Assert.Equal((byte)4, step.Task.ReadByte(0x29));
        Assert.Equal(new[] { resources.Sources[1].Address, resources.Sources[2].Address }, step.Publications.Select(publication => U32(publication.State, 0xAC)).ToArray());
        step = Advance(step.Task, step.Camera, 4);
        Assert.Equal((byte)5, step.Task.ReadByte(0x29));
        Assert.Equal(new[] { resources.Sources[2].Address, resources.Sources[3].Address }, step.Publications.Select(publication => U32(publication.State, 0xAC)).ToArray());
        Assert.Equal(Target.ToArray(), step.Camera.Data.AsSpan().Slice(0xA4, 8).ToArray());
        int curveCalls = 0;
        while (step.Task.ReadByte(0x29) == 5 && curveCalls++ < 40)
        {
            step = Advance(step.Task, step.Camera, 4);
        }
        Assert.Equal(35, curveCalls);
        Assert.Equal((byte)6, step.Task.ReadByte(0x29));
        Assert.Equal(new[] { resources.Sources[3].Address, resources.Sources[4].Address }, step.Publications.Select(publication => U32(publication.State, 0xAC)).ToArray());
        Assert.Equal(resources.Curve.Address + 2, U32(step.Camera, 0x50));
        NativeBattleChoco98CameraTaskStep held = Advance(step.Task, step.Camera, -1);
        Assert.Equal((byte)6, held.Task.ReadByte(0x29));
        Assert.Single(held.Publications);
        Assert.Equal(resources.Curve.Address + 4, U32(held.Camera, 0x50));
        step = Advance(held.Task, held.Camera, 6);
        Assert.Equal((byte)7, step.Task.ReadByte(0x29));
        Assert.Single(step.Publications);
        Assert.Equal(resources.Curve.Address + 6, U32(step.Camera, 0x50));
        step = Advance(step.Task.WriteByte(0x28, 1), step.Camera, 6);
        Assert.Equal((byte)8, step.Task.ReadByte(0x29));
        Assert.Empty(step.Publications);
        Assert.False(step.ReadyToComplete);
        step = Advance(step.Task.WriteByte(0x28, 0), step.Camera, 6);
        Assert.True(step.ReadyToComplete);
        Assert.Empty(step.Publications);

        NativeBattleChoco98CameraTaskStep Advance(NativeBattleModulePoolTask currentTask, NativeBattleMoogleCameraState? camera, short mode)
        {
            Saved current = new(currentTask, camera);
            Saved restored = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(current))!;
            ushort previousCounter = currentTask.ReadWord(0x24);
            NativeBattleChoco98CameraTaskStep next = NativeBattleChoco98CameraTask.Advance7276F0(currentTask, camera, resources, mode, Actor, Target);
            Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(NativeBattleChoco98CameraTask.Advance7276F0(restored.Task, restored.Camera, resources, mode, Actor, Target)));
            Assert.Equal(unchecked((ushort)(previousCounter + 1)), next.Task.ReadWord(0x24));
            return next;
        }
    }

    [Fact]
    public void Missing_retained_state_and_wrong_source_or_curve_are_rejected_without_camera_fallback()
    {
        NativeBattleChoco98CameraResources resources = Resources();
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco98CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray()).WriteByte(0x29, 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98CameraTask.Advance7276F0(task, null, resources, 6, Actor, Target));
        Assert.Throws<InvalidDataException>(() => (resources with { Curve = resources.Curve with { Values = resources.Curve.Values.RemoveAt(35) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sources = resources.Sources.SetItem(3, resources.Sources[3] with { Address = resources.Curve.Address }) }).Validate());
    }

    private static NativeBattleChoco98CameraResources Resources()
    {
        uint[] addresses = [0x152B3D0, 0x152B418, 0x152B460, 0x152B4F0, 0x152B538];
        ImmutableArray<NativeBattleMoogleCameraSource>.Builder sources = ImmutableArray.CreateBuilder<NativeBattleMoogleCameraSource>();
        for (int index = 0; index < addresses.Length; index++)
        {
            byte[] bytes = new byte[0x48];
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x3A), 4096);
            if (index >= 3)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x152B4A8);
            }
            sources.Add(new(addresses[index], bytes.ToImmutableArray()));
        }
        return new(sources.ToImmutable(), new(0x152B4A8, Enumerable.Range(0, 35).Select(index => (short)(index * 100)).Append((short)4096).ToImmutableArray()));
    }

    private static uint U32(NativeBattleMoogleCameraState state, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(state.Data.AsSpan().Slice(offset));
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState? Camera);
}
