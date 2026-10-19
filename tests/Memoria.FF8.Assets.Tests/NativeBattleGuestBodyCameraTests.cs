using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGuestBodyCameraTests
{
    [Fact]
    public void Special_body_loader_retains_camera_section_instead_of_sound_directory()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var source = assets.ReadNativeModelDatSource(7, false, 0);
        BattleModel model = assets.LoadPlayer(7);
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan()));
        byte[] camera = source.Bytes.Slice(source.Section(4), source.Section(5) - source.Section(4)).ToArray();
        byte[] sound = source.Bytes.Slice(source.Section(6), source.Section(7) - source.Section(6)).ToArray();
        Assert.Equal(1904, camera.Length);
        Assert.Equal(480, sound.Length);
        Assert.Equal(camera, model.NativeActionCameraBank!.Data.ToArray());
        Assert.NotEqual(sound, model.NativeActionCameraBank.Data.ToArray());
    }
    [Fact]
    public void OriginalLoadedBodyMatricesStartAfterSixteenBytesOfEachBoneRecord()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var dat = assets.ReadNativeModelDatSource(7, false, 0);
        BattleModel body = assets.LoadPlayer(7);
        NativeBattleGraphicsModel loaded = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(new(0, 2), body.NativePoseSource!);
        int skeleton = dat.Section(0);
        for (int bone = 0; bone < body.Bones.Count; bone++)
        {
            int record = skeleton + 16 + 48 * bone;
            NativeBattleActionMatrix matrix = loaded.CurrentBodyBoneMatrices!.Matrices[bone];
            for (int lane = 0; lane < 9; lane++)
            {
                Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(dat.Bytes.AsSpan().Slice(record + 16 + lane * 2, 2)), matrix.Rotation[lane]);
            }
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(dat.Bytes.AsSpan().Slice(record + 36, 4)), matrix.X);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(dat.Bytes.AsSpan().Slice(record + 40, 4)), matrix.Y);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(dat.Bytes.AsSpan().Slice(record + 44, 4)), matrix.Z);
        }
    }
}
