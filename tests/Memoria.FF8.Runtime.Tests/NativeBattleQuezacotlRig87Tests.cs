using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlRig87Tests
{
    [Fact]
    public void RegularPoseStepUsesTheCurrentRawCursorAndCurrentTranslationInsteadOfReplayingItsInitialPose()
    {
        byte[] animation = new byte[14];
        BinaryPrimitives.WriteInt32LittleEndian(animation, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animation.AsSpan(4), 8);
        animation[8] = 2;
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animation, 1, 0);
        Assert.Equal((ushort)3, first.ByteOffset);
        Assert.Equal((ushort)3, first.BitOffset);
        NativeBattleDecodedPose current = first.Pose with { Translation = new(123, -456, 789) };
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(animation, 1, 0, current, first.ByteOffset, first.BitOffset);
        Assert.Equal(current.Translation, next.Pose.Translation);
        Assert.Equal((ushort)5, next.ByteOffset);
        Assert.Equal((ushort)6, next.BitOffset);
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseDecoder.AdvanceRegular508F90(animation, 1, 0, current, 0, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseDecoder.AdvanceRegular508F90(animation, 1, 0, current, first.ByteOffset, 8));
    }

    [Fact]
    public void Camera87UsesSavedWordsPreservesBothPaddingWordsAndCopiesThemToTheCurrentGlobals()
    {
        const int targetZ = 3690;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        byte[] saved = new byte[16];
        short[] words = [10, 20, 30, 0x1234, 0, 5, targetZ + 4096, 0x5678];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(saved.AsSpan(index * 2), words[index]);
        }
        memory = memory with { Arena = memory.Arena!.Write(0x18, saved) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child], Quezacotl = new(1, 0, targetZ) };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(100, 200, 300), new(400, 500, 600), 288, 7), FocalLength = 288, Roll = 7, EyePaddingF6 = -1, TargetPaddingFE = -2 };
        var result = NativeBattleQuezacotlRig86.UpdateFlightCamera6C3DDF(memory, effect, camera, 1);
        Assert.Equal(new NativeBattleCameraPoint(10, 3, 30), result.Camera.CurrentPose!.Target);
        Assert.Equal((short)0, result.Camera.CurrentPose.Eye.Y);
        Assert.Equal((short?)0x1234, result.Camera.TargetPaddingFE);
        Assert.Equal((short?)0x5678, result.Camera.EyePaddingF6);
        Assert.Equal((short?)288, result.Camera.FocalLength);
        Assert.Equal((short)7, result.Camera.Roll);
        Assert.Equal(saved.AsSpan(6, 2).ToArray(), result.Rig.Memory.Arena!.Read(0x1E, 2));
        Assert.Equal(saved.AsSpan(14, 2).ToArray(), result.Rig.Memory.Arena.Read(0x26, 2));
        Assert.Equal(result.Rig.Memory.Arena.Read(12, 0x8D0 - 12), NativeBattleStorageSnapshots.Capture(result.Rig.Effect.Pools[0].Slots[0]).Payload.ToArray());
    }
    [Theory]
    [InlineData(31, 1531, -2459, 3172, -5127, -1606, 5232)]
    [InlineData(38, -232, 200, 307, 96, 500, 576)]
    [InlineData(41, 106, -122, 3487, -52, -216, 430)]
    public void FlightCameraUsesItsNativeSpecialRotationAndBlendBranchesWithoutReplacingPadding(int localFrame, short eyeX, short eyeY, short eyeZ, short targetX, short targetY, short targetZ)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        short[] words = localFrame == 41 ? [0, 17, 0, 0x1234, 0, 5, 3690, 0x5678] : [10, 20, 30, 0x1234, 0, 5, 7786, 0x5678];
        byte[] saved = new byte[16];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(saved.AsSpan(index * 2), words[index]);
        }
        memory = memory with { Arena = memory.Arena!.Write(0x18, saved) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child], Quezacotl = new(1, 0, 3690) };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(100, 200, 300), new(400, 500, 600), 288, 7), FocalLength = 288, Roll = 7, EyePaddingF6 = -1, TargetPaddingFE = -2 };
        var result = NativeBattleQuezacotlRig86.UpdateFlightCamera6C3DDF(memory, effect, camera, localFrame);
        Assert.Equal(new NativeBattleCameraPoint(eyeX, eyeY, eyeZ), result.Camera.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(targetX, targetY, targetZ), result.Camera.CurrentPose.Target);
        Assert.Equal((short?)-1, result.Camera.EyePaddingF6);
        Assert.Equal((short?)-2, result.Camera.TargetPaddingFE);
        Assert.Equal(saved.AsSpan(6, 2).ToArray(), result.Rig.Memory.Arena!.Read(0x1E, 2));
        Assert.Equal(saved.AsSpan(14, 2).ToArray(), result.Rig.Memory.Arena.Read(0x26, 2));
    }
}
