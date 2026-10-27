using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlExecutableWorkspaceTests
{
    [Fact]
    public void Executable_workspace_initialization_and_advance_preserve_the_concurrent_child_workspace_and_other_executable_bytes()
    {
        var (memory, effect) = Fixture();
        NativeBattleGfAnimationWorkspace child = new(Enumerable.Repeat((byte)0x5A, NativeBattleQuezacotlWorkspace.SeventhLength).ToImmutableArray());
        NativeBattleQuezacotlWorkspaceWrite childWritten = NativeBattleQuezacotlWorkspace.Write(memory, effect, child);
        memory = childWritten.Memory;
        effect = childWritten.Effect;
        string beforeEffect = SnapshotJson(effect);
        ImmutableArray<byte> before = NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory!).Data;
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84);
        NativeBattleGfAnimationWorkspace initialized = NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Initialize7016B0(source, NativeBattleQuezacotlWorkspace.EighthLength);
        memory = NativeBattleQuezacotlWorkspace.WriteExecutable(memory, initialized);
        NativeBattleGfAnimationAdvance advanced = NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Advance701970(NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84), 0, (current, _) => current);
        memory = NativeBattleQuezacotlWorkspace.WriteExecutable(memory, advanced.Workspace);
        Assert.Equal(0x129CD84u, advanced.Workspace.ResourceAddress);
        Assert.Equal(1, NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Frame);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(child).Data.ToArray(), NativeBattleStorageSnapshots.Capture(NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.SeventhLength)).Data.ToArray());
        Assert.Equal(beforeEffect, SnapshotJson(effect));
        int offset = checked((int)(NativeBattleQuezacotlWorkspace.EighthAddress - NativeBattleQuezacotlExecutableMemory.Address));
        Assert.Equal(before.AsSpan().Slice(0, offset).ToArray(), NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory!).Data.AsSpan().Slice(0, offset).ToArray());
        Assert.Equal(before.AsSpan().Slice(offset + NativeBattleQuezacotlWorkspace.EighthLength).ToArray(), NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory).Data.AsSpan().Slice(offset + NativeBattleQuezacotlWorkspace.EighthLength).ToArray());
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(SnapshotJson(memory))!);
        Assert.Equal(SnapshotJson(memory), SnapshotJson(restored));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(advanced.Workspace).Data.ToArray(), NativeBattleStorageSnapshots.Capture(NativeBattleQuezacotlWorkspace.ReadExecutable(restored)).Data.ToArray());
    }

    [Fact]
    public void Paused_executable_evaluator_still_dispatches_without_advancing_and_wrong_source_pointer_stays_strict()
    {
        var (memory, _) = Fixture();
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Initialize7016B0(source, NativeBattleQuezacotlWorkspace.EighthLength);
        int callbacks = 0;
        NativeBattleGfAnimationAdvance paused = workspace.Advance701970(source, 1, (current, _) =>
        {
            callbacks++;
            return current;
        });
        Assert.Equal(1, callbacks);
        Assert.Equal(0, paused.Workspace.Frame);
        Assert.Equal(1, paused.Workspace.Advance701970(source, 0, (current, _) => current).Workspace.Frame);
        Assert.Throws<InvalidDataException>(() => paused.Workspace.Advance701970(source with { Address = 0x129CD86 }, 0, (current, _) => current));
    }

    [Fact]
    public void Missing_executable_owner_and_child_alias_or_wrong_length_are_rejected()
    {
        var (memory, effect) = Fixture();
        NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.ReadExecutable(memory);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlWorkspace.ReadExecutable(memory with { QuezacotlExecutableMemory = null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlWorkspace.WriteExecutable(memory with { QuezacotlExecutableMemory = null }, workspace));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlWorkspace.WriteExecutable(memory, new(NativeBattleStorageSnapshots.Capture(workspace).Data.RemoveAt(0))));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlWorkspace.Write(memory, effect, workspace));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlWorkspace.Source(memory, NativeBattleQuezacotlWorkspace.EighthAddress));
    }

    [Theory]
    [InlineData(20, -64)]
    [InlineData(37, -64)]
    [InlineData(38, 384)]
    [InlineData(45, 384)]
    public void Camera_threshold_changes_only_signed_word_EyeY_and_TargetY_without_publishing_projection(int localFrame, int delta)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(7, -32760, 9), new(10, 32760, 12), 288, -13), FocalLength = 288, Roll = -13, EyePaddingF6 = 0x1234, TargetPaddingFE = 0x5678 };
        NativeBattleCameraState shifted = NativeBattleQuezacotlLaterCamera.Shift6C4685(camera, localFrame);
        Assert.Equal(new NativeBattleCameraPoint(7, unchecked((short)(-32760 + delta)), 9), shifted.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(10, unchecked((short)(32760 + delta)), 12), shifted.CurrentPose.Target);
        Assert.Equal(camera.Projection, shifted.Projection);
        Assert.Equal(camera.Roll, shifted.Roll);
        Assert.Equal(camera.FocalLength, shifted.FocalLength);
        Assert.Equal(camera.EyePaddingF6, shifted.EyePaddingF6);
        Assert.Equal(camera.TargetPaddingFE, shifted.TargetPaddingFE);
    }

    [Theory]
    [InlineData(277)]
    [InlineData(301)]
    public void Additional_workspace_phases_before302_do_not_poll_or_append_to_the_busy276_batch(ushort phase)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(116), 64);
        memory = memory with { Arena = memory.Arena!.Write(64, [1, 0, 1, 2, 0]), EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
        NativeBattleQuezacotlFirstWindowIoStep step = NativeBattleQuezacotlFirstWindowIo.Advance(memory, phase);
        Assert.True(step.AdvancePhase);
        Assert.Null(step.Request);
        Assert.Equal(new byte[] { 1, 0, 1, 2, 0 }, step.Memory.Arena!.Read(64, 5));
    }

    [Fact]
    public void Camera_shift_rejects_operands_outside_the_original_concurrent_workspace_range()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess();
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleQuezacotlLaterCamera.Shift6C4685(camera, 19));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleQuezacotlLaterCamera.Shift6C4685(camera, 46));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleQuezacotlLaterCamera.Shift6C4685(camera, int.MaxValue));
    }

    [Theory]
    [InlineData(3690, 7245, 1328)]
    [InlineData(32000, -29981, 29638)]
    public void Phase302_camera_uses_targetZ1778_and_keeps_roll_and_prior_projection(int targetZ1778, short eyeZ, short targetZ)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 288, Roll = 13, EyePaddingF6 = 7, TargetPaddingFE = 8 };
        NativeBattleCameraState result = NativeBattleQuezacotlLaterCamera.Apply6C4F80(camera, 302, targetZ1778);
        Assert.Equal(new NativeBattleCameraPoint(0, -2378, eyeZ), result.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(0, -1291, targetZ), result.CurrentPose.Target);
        Assert.Equal(camera.Projection, result.Projection);
        Assert.Equal(camera.Roll, result.Roll);
        Assert.Equal(camera.EyePaddingF6, result.EyePaddingF6);
        Assert.Equal(camera.TargetPaddingFE, result.TargetPaddingFE);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        byte[] bytes = Enumerable.Repeat((byte)0xCC, NativeBattleQuezacotlExecutableMemory.Length).ToArray();
        int source = 0x129CD84 - (int)NativeBattleQuezacotlExecutableMemory.Address;
        bytes.AsSpan(source, 28).Clear();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(source + 4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(source + 8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(source + 12), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(source + 16), 12);
        memory = memory with { QuezacotlExecutableMemory = new(bytes.ToImmutableArray()) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child] };
        return (memory, effect);
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

}
