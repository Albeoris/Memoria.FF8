using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216CameraTests
{
    [Fact]
    public void OpeningUsesActualActorWordsAndPublishesFocalRollWithoutSoundReplay()
    {
        NativeBattleDuel216CameraStep step = NativeBattleDuel216Camera.AdvanceMain6960E0(Frame(), 0, NativeBattleCameraState.FromColdProcess());
        Assert.Equal(new NativeBattleCameraPose(new(-36, -542, -8), new(-524, -688, -1672), 288, 0), step.Camera.CurrentPose);
        Assert.True(step.Camera.ForceCamera);
        Assert.Equal((short)288, BinaryPrimitives.ReadInt16LittleEndian(step.Frame.Staging.Read(0x1D8E038, 2)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(step.Frame.Staging.Read(0x1D977A2, 2)));
    }

    [Theory]
    [InlineData(22, 493, -3139, -1, 1278, -2460, 151)]
    [InlineData(69, 329, -9256, -258, 3295, -4912, 463)]
    [InlineData(70, 275, -13993, 5476, 171, -13797, 7504)]
    [InlineData(101, 366, -13372, 4959, 5694, -13159, 5180)]
    [InlineData(102, 366, -13372, 4959, 5694, -13159, 5180)]
    [InlineData(111, 318, -1493, 361, 1197, -4578, 4852)]
    [InlineData(112, 829, -1312, -314, 2609, -2004, -2228)]
    public void CameraSegmentsKeepNativeEndpointsAndHeightDword(ushort tick, short ex, short ey, short ez, short tx, short ty, short tz)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess();
        NativeBattleDuel216CameraStep step = NativeBattleDuel216Camera.AdvanceMain6960E0(Frame(), tick, camera);
        Assert.Equal(new NativeBattleCameraPose(new(ex, ey, ez), new(tx, ty, tz), camera.FocalLength, camera.Roll), step.Camera.CurrentPose);
    }

    private static NativeBattleDuel216Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Root);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x25088F8, BitConverter.GetBytes((ushort)100)).Write(0x25088FC, BitConverter.GetBytes((ushort)200)).Write(0x2508900, BitConverter.GetBytes((ushort)300)).Write(0x2508902, BitConverter.GetBytes(unchecked((ushort)-20))).Write(0x2508904, BitConverter.GetBytes((ushort)400)).Write(0x2508908, BitConverter.GetBytes(1249));
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, staging, NativeCrtRandomState.FromColdProcess());
    }
}
