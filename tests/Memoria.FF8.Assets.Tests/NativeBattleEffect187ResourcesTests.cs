using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect187ResourcesTests
{
    [Fact]
    public void OriginalResourcesCloseAllSelectedFilesGeometryAndSpawnRows()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffect187Resources resources = game.ReadNativeEffect187Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect187Resources());
        Assert.Equal(new[] { 100896, 79664, 4, 65696, 4, 4 }, resources.Files.Select(file => file.Data.Length));
        Assert.Equal(Enumerable.Range(0x225, 6), resources.Files.Select(file => (int)file.FileId));
        Assert.Equal(60, resources.OwnedModel.PoseSource.BoneCount);
        Assert.Equal(3, resources.OwnedModel.Geometry.Length);
        Assert.Equal(8, resources.Meshes.Count);
        Assert.Equal(5, resources.Sprites.Count);
        Assert.Equal(168224, resources.Texture.Data.Length);
        Assert.Equal(324, resources.Camera.Data.Length);
        Assert.Equal(342, resources.Vibration.Data.Length);
        Assert.Equal((ushort)0x81, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan()));
        Assert.Equal(48, resources.ReadStatic(0xE41EF8, 48).Length);
        Assert.Equal(1008, resources.ReadStatic(0xE3FD70, 1008).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xE3FD70, 1009));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xE41EF8, 49));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xE41E50, 4));
        Assert.Equal(new uint[] { 0xE41788, 0xE413E8, 0xE3D3A8 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0xE41EA8 + (uint)(index * 4), 4))));
    }

    [Fact]
    public void OriginalCompressedPosePublishesItsCurrentBoneDataIntoTheActualWorkingFile()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffect187Resources resources = game.ReadNativeEffect187Resources();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0, resources.Files[1].Data.AsSpan()) };
        NativeBattleCactuarRawModelState model = NativeBattleEffect187Model.Initialize64A1D0(NativeBattleCactuarRawModelState.FromColdProcess(), resources.OwnedModel, NativeBattleEffect187Profile.ArenaAddress);
        model = NativeBattleEffect187Model.Select64A240(model, resources.OwnedModel, 0);
        memory = NativeBattleEffect187Model.PublishPose508C90(memory, model, resources.OwnedModel);
        NativeBattleDecodedPose decoded = Assert.IsType<NativeBattleDecodedPose>(model.DecodedPose);
        NativeBattleEffectArena current = Assert.IsType<NativeBattleEffectArena>(memory.Arena);
        int skeleton = resources.OwnedModel.Section(0);
        NativeBattleDecodedPose physical = NativeBattleArenaPose.ReadCurrentPose(current, skeleton, 60);
        Assert.Equal(decoded.Translation, physical.Translation);
        Assert.Equal(decoded.HasScale, physical.HasScale);
        Assert.Equal(decoded.Bones.ToArray(), physical.Bones.ToArray());
        _ = NativeBattleArenaPose.ReadMatrix(current, skeleton + 32 + 59 * 48);
        Assert.Throws<NotSupportedException>(() => current.Read(skeleton + 32 + 59 * 48, 32));
        NativeBattleCactuarRawModelStep step = NativeBattleEffect187Model.Advance64A260(model, resources.OwnedModel);
        memory = NativeBattleEffect187Model.PublishPose508C90(memory, step.State, resources.OwnedModel);
        NativeBattleEffectArena advanced = Assert.IsType<NativeBattleEffectArena>(memory.Arena);
        Assert.Equal(step.State.DecodedPose!.Translation, NativeBattleArenaPose.ReadCurrentPose(advanced, skeleton, 60).Translation);
    }
}
