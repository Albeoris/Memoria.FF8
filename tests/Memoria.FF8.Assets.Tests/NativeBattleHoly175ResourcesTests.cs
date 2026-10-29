using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleHoly175ResourcesTests
{
    [Fact]
    public void Original_Holy_profile_keeps_all_bounded_sources_and_roundtrips_without_dispatch_materialization()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleHoly175Resources resources = game.ReadNativeHoly175Resources();
        Assert.Same(resources, game.ReadNativeHoly175Resources());
        Assert.Equal("FF8_EN:D45188", resources.Animation.Identity);
        Assert.Equal(0xD24, resources.Animation.Data.Length);
        Assert.Equal("FF8_EN:D45EAC", resources.Tim.Identity);
        Assert.Equal(0xC420, resources.Tim.Data.Length);
        Assert.Equal("FF8_EN:D59180", resources.SoundDescriptor.Identity);
        Assert.Equal(4, resources.SoundDescriptor.Data.Length);
        Assert.Equal("FF8_EN:D59250", resources.CaptureDirectory!.Identity);
        Assert.Equal(32, resources.CaptureDirectory.Data.Length);
        Assert.Equal("FF8_EN:D59188", resources.DelayTable!.Identity);
        Assert.Equal(48, resources.DelayTable.Data.Length);
        Assert.Equal("FF8_EN:D591D0", resources.RayRows!.Identity);
        Assert.Equal(64, resources.RayRows.Data.Length);
        Assert.Equal("FF8_EN:D59210", resources.WindowColors!.Identity);
        Assert.Equal(64, resources.WindowColors.Data.Length);
        NativeBattleEffectBinding installed = game.ReadNativeEffectCatalogForEffects([175])[174];
        Assert.Equal(0x5D1AD0u, installed.Entry);
        Assert.Equal(0x5D1AB0u, installed.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag174.tim", 0x2392CE0), installed.InitializerResource);
        Assert.Same(resources, installed.Holy175Resources);
        Assert.Equal(new uint[] { 4382, 15, 4156, 11, 4124, 11, 4382, 15 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(resources.CaptureDirectory.Data.AsSpan().Slice(index * 4))).ToArray());
        Assert.Equal("FF8_EN:D592C0", resources.CaptureBlackPackets!.Identity);
        Assert.Equal(48, resources.CaptureBlackPackets.Data.Length);
        for (int index = 0; index < 2; index++)
        {
            ReadOnlySpan<byte> black = resources.CaptureBlackPackets.Data.AsSpan().Slice(index * 24, 24);
            Assert.Equal((byte)5, black[3]);
            Assert.Equal(0x28000000u, BinaryPrimitives.ReadUInt32LittleEndian(black.Slice(4)));
            Assert.Equal(320u, BinaryPrimitives.ReadUInt32LittleEndian(black.Slice(12)));
            Assert.Equal(248u << 16, BinaryPrimitives.ReadUInt32LittleEndian(black.Slice(16)));
            Assert.Equal(248u << 16 | 320u, BinaryPrimitives.ReadUInt32LittleEndian(black.Slice(20)));
        }
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(resources.SoundDescriptor.Data.AsSpan()), resources.Sound);
        Assert.Equal(new uint[] { 0xD522CC, 0xD52374, 0xD523E8, 0xD524C4 }, resources.ProjectedSprites.Select(sprite => sprite.Address).ToArray());
        Assert.Equal(new[] { 0xA8, 0x74, 0xDC, 0x334 }, resources.ProjectedSprites.Select(sprite => sprite.Bytes.Length).ToArray());
        Assert.Equal(new uint[] { 0xD527F8, 0xD52DD8, 0xD53688, 0xD53BA0, 0xD54588, 0xD56C58 }, resources.Meshes.Select(mesh => mesh.Address).ToArray());
        Assert.Equal(new[] { 0x5E0, 0x8B0, 0x518, 0x9E8, 0x26D0, 0x2528 }, resources.Meshes.Select(mesh => mesh.Bytes.Length).ToArray());
        string json = NativeBattleSnapshotJson.Serialize(resources);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleHoly175Resources>(json)!));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(0));
        Assert.Throws<NotSupportedException>(() => resources.Mesh(0));
        Assert.Throws<NotSupportedException>(() => NativeBattleHoly175ResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void Original_Holy_projected_frames_and_all_six_mesh_group_directories_preserve_original_bytes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleHoly175Resources resources = game.ReadNativeHoly175Resources();
        int[] frames = [6, 4, 8, 31];
        for (int index = 0; index < resources.ProjectedSprites.Length; index++)
        {
            NativeBattleHoly175SpriteResource source = resources.ProjectedSprites[index];
            Assert.Same(source, resources.Sprite(source.Address));
            Assert.Equal((byte)0, source.Bytes[3]);
            if (index < 3)
            {
                Assert.Equal((byte)1, source.Bytes[2]);
            }
            NativeBattleModuleResource projected = new(0, source.Address, 0, 0, source.Bytes);
            projected.Validate();
            Assert.Equal(frames[index], BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8)));
            for (int frame = 0; frame < frames[index]; frame++)
            {
                NativeBattleModuleSpriteFrame selected = projected.ProjectedFrame571C80(frame);
                Assert.Equal(0, selected.Quads.Length % 20);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(selected), NativeBattleSnapshotJson.Serialize(projected.SpriteFrame(frame)));
            }
            Assert.Throws<InvalidDataException>(() => projected.ProjectedFrame571C80(frames[index]));
        }
        int[][] expected = [[0, 16, 0, 0, 0, 32, 0, 0], [0, 0, 0, 64, 0, 0, 0, 0], [0, 0, 0, 0, 0, 36, 0, 0], [0, 0, 0, 0, 0, 72, 0, 0], [0, 0, 24, 288, 0, 0, 0, 0], [0, 0, 0, 160, 0, 0, 0, 96]];
        int[] vertexCounts = [63, 81, 50, 96, 313, 272];
        for (int index = 0; index < resources.Meshes.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = resources.Meshes[index];
            Assert.Same(mesh, resources.Mesh(mesh.Address));
            Assert.Equal(vertexCounts[index], BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
            Assert.Equal(expected[index], Enumerable.Range(0, 8).Select(group => NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count).ToArray());
        }
    }

    [Fact]
    public void Original_Holy_animation_and_two_TIM_blocks_use_existing_registration_upload_and_cold_next_contracts()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleHoly175Resources resources = game.ReadNativeHoly175Resources();
        NativeBattleEffectTextureState registered = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        Assert.NotNull(registered.Animation);
        (NativeBattleEffectTextureState queued, int end) = registered.EnqueueTim505E30(resources.Tim);
        Assert.Equal(resources.Tim.Data.Length, end);
        Assert.Equal(1, queued.Count);
        Assert.Same(resources.Tim, queued.Slots[0]!.Source);
        string json = NativeBattleSnapshotJson.Serialize(queued);
        NativeBattleEffectTextureState cold = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectTextureState>(json)!;
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(cold));
        NativeBattleEffectTextureDrain drained = queued.Drain505D20();
        Assert.Equal(2, drained.Uploads.Length);
        Assert.All(drained.Uploads, upload => Assert.Equal(upload.Width * upload.Height * 2, upload.Bytes.Length));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(drained), NativeBattleSnapshotJson.Serialize(cold.Drain505D20()));
        Assert.Equal(0, drained.State.Count);
    }
}
