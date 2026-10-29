using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogle96ResourcesTests
{
    [Fact]
    public void Original_Moogle_profile_keeps_its_owned_model_camera_vibration_and_distinct_neutral_banks()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([96])[95];
        Assert.Equal(0x731DE0u, binding.Entry);
        Assert.Equal(0x731DC0u, binding.Initializer);
        Assert.Equal("mag095.tim", binding.InitializerAsset!.Name);
        NativeBattleMoogle96Resources resources = Assert.IsType<NativeBattleMoogle96Resources>(binding.Moogle96Resources);
        Assert.Same(resources, game.ReadNativeMoogle96Resources());
        Assert.Equal(0x152BC04u, resources.Model.Address);
        Assert.Equal(0x7060, resources.Model.Bytes.Length);
        Assert.Equal(30, resources.Model.PoseSource.BoneCount);
        Assert.Equal(new byte[] { 77, 12, 36 }, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).ToArray());
        NativeBattleRawMesh mesh = Assert.Single(resources.Model.Geometry);
        Assert.Equal(507, mesh.Vertices.Length);
        Assert.Equal(950, mesh.Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(22, mesh.Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(0x32808080u, resources.DefaultColor44);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(game.ReadNativeMoogleCameraResources()), NativeBattleSnapshotJson.Serialize(resources.Camera));
        Assert.Equal(12, resources.Camera.Sources.Length);
        Assert.Equal(0x1692140u, resources.Vibration.Address);
        Assert.Equal(0x13C, resources.Vibration.Bank.Bytes.Length);
        Assert.Equal(new[] { 0, 0x100 }, resources.Vibration.Bank.Offsets.ToArray());
        Assert.Equal((ushort)54, resources.Vibration.Bank.ReadWord(0x100));
        Assert.Equal((ushort)0, resources.Vibration.Bank.ReadWord(0x102));
        Assert.Equal("FF8_EN:169227C", resources.NodesA.Identity);
        Assert.Equal(0x4B6C, NativeBattleStorageSnapshots.Capture(resources.NodesA).Data.Length);
        Assert.Equal("FF8_EN:1696DE8", resources.NodesB.Identity);
        Assert.Equal(0x33D4, NativeBattleStorageSnapshots.Capture(resources.NodesB).Data.Length);
        Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.NodesA).Data, 0));
        Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.NodesB).Data, 0));
        Assert.Equal(new[] { 0x10, 0x10, 0x18 }, resources.TextureSequences.Select(item => NativeBattleStorageSnapshots.Capture(item).Data.Length).ToArray());
        Assert.Equal(new[] { "FF8_EN:1532FC8", "FF8_EN:1532FD8", "FF8_EN:1532FE8" }, resources.TextureSequences.Select(item => item.Identity).ToArray());
        string json = NativeBattleSnapshotJson.Serialize(binding);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleEffectBinding>(json)!));
        Assert.Null(game.ReadNativeEffectCatalog()[95].Moogle96Resources);
    }

    [Fact]
    public void Original_Moogle_WAIT_file_and_terminal_records_bind_complete_original_battle_archive_payloads()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleMoogle96Resources resources = game.ReadNativeMoogle96Resources();
        Assert.Equal("FF8_EN:152BBA0", resources.IoScript.Identity);
        Assert.Equal(0x60, NativeBattleStorageSnapshots.Capture(resources.IoScript).Data.Length);
        Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.IoScript).Data, 0));
        Assert.Equal((ushort)0xFF, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleStorageSnapshots.Capture(resources.IoScript).Data.AsSpan().Slice(0x50)));
        Assert.Equal(new short[] { 438, 439, 440, 441 }, resources.Files.Select(item => item.FileId).ToArray());
        Assert.Equal(new[] { 0x8220, 0x8220, 0x4220, 0x4220 }, resources.Files.Select(item => NativeBattleStorageSnapshots.Capture(item).Data.Length).ToArray());
        Assert.Equal(new[] { "MAG095_B.1T0", "MAG095_B.1T1", "MAG095_B.1T2", "MAG095_B.1T3" }, resources.Files.Select(item => item.Name.ToUpperInvariant()).ToArray());
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            var queued = textures.EnqueueTim505E30(new(file.Name, NativeBattleStorageSnapshots.Capture(file).Data));
            Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.Length, queued.NextOffset);
            textures = queued.State;
        }
        Assert.NotEmpty(textures.Drain505D20().Uploads);
    }

    [Fact]
    public void Original_Moogle_complete_GF_source_advances_all_keyed_streams_and_restores_its_own_cache()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeMoogle96Resources().ProgramSource;
        Assert.Equal(0x169A1BCu, source.Address);
        Assert.Equal(0x9DF4, NativeBattleStorageSnapshots.Capture(source).Data.Length);
        int[] groups = new int[8];
        for (int index = 0; index < 21; index++)
        {
            int geometry = checked((int)U32(NativeBattleStorageSnapshots.Capture(source).Data, 8 + index * 4));
            int end = index == 20 ? 0x7C34 : checked((int)U32(NativeBattleStorageSnapshots.Capture(source).Data, 12 + index * 4));
            for (int group = 0; group < 8; group++)
            {
                groups[group] += NativeBattleIceMeshVertices.Group(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(geometry, end - geometry), group).Count;
            }
        }
        Assert.Equal(new[] { 4, 97, 0, 160, 6, 134, 48, 288 }, groups);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x488].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, 0x488);
        int emitted = 0;
        for (int frame = 0; frame < 135; frame++)
        {
            NativeBattleGfAnimationWorkspace? restored = frame == 67 ? NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace)) : null;
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, _) => current);
            Assert.Equal(134 - frame, next.Remaining);
            if (restored is not null)
            {
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(restored.Advance701970(source, 0, (current, _) => current)));
            }
            emitted += next.EmittedNodes;
            workspace = next.Workspace;
        }
        Assert.Equal(135, workspace.Frame);
        Assert.True(emitted > 0);
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
