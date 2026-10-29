using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoom8ResourcesTests
{
    [Fact]
    public void Original_Doom_resources_keep_all_five_script_files_and_complete_texture_uploads()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        Assert.Same(resources, game.ReadNativeDoom8Resources());
        resources.Validate();
        Assert.Equal("mag007.tim", resources.MainTim.Name);
        Assert.Equal(167968, resources.MainTim.Data.Length);
        Assert.Equal(4908, resources.Camera.Data.Length);
        Assert.Equal(new short[] { 383, 384, 385, 386, 387 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { 7072, 3204, 10984, 33312, 14880 }, resources.Files.Select(file => file.Data.Length));
        Assert.Equal(new[] { 33824, 16928 }, resources.DirectorTims.Select(tim => tim.Data.Length));
        Assert.Equal(0x494A2u, resources.DirectorSound);
        Assert.Equal(112, resources.InitialIoScript.Data.Length);
        NativeBattleModuleResource dust = resources.RequireDustSprite();
        Assert.Equal(456, dust.Bytes.Length);
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16LittleEndian(dust.Bytes.AsSpan().Slice(8)));
        for (int frame = 0; frame < 17; frame++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(dust.Bytes.AsSpan().Slice(10 + frame * 2));
            Assert.Equal(48 + frame * 24, offset);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(dust.Bytes.AsSpan().Slice(offset)));
            _ = dust.ProjectedFrame571C80(frame);
        }
        NativeBattleStaticMeshResource smoke = resources.RequireSmokeMesh();
        Assert.Equal(2024, smoke.Bytes.Length);
        Assert.Equal(0x16350F0u, smoke.Address);
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.InitialIoScript.Data.AsSpan().Slice(index * 16 + 12)));
        }
        Assert.Equal(new ushort[] { 320, 240, 256, 8 }, Block(resources.MainTim.Data, 8));
        Assert.Equal(new ushort[] { 384, 256, 320, 256 }, Block(resources.MainTim.Data, 4116));
        Assert.Equal(new ushort[] { 320, 224, 256, 2 }, Block(resources.DirectorTims[0].Data, 8));
        Assert.Equal(new ushort[] { 832, 256, 64, 256 }, Block(resources.DirectorTims[0].Data, 1044));
        Assert.Equal(new ushort[] { 320, 226, 256, 1 }, Block(resources.DirectorTims[1].Data, 8));
        Assert.Equal(new ushort[] { 768, 256, 64, 128 }, Block(resources.DirectorTims[1].Data, 532));
        Assert.Equal(new ushort[] { 704, 256, 64, 256 }, Block(resources.File(386).Data, 532));
        Assert.Equal(new ushort[] { 768, 384, 56, 128 }, Block(resources.File(387).Data, 532));
        NativeBattleDoom8Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleDoom8Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restored));
    }

    [Fact]
    public void Embedded_resources_preserve_the_relative_node_bank_and_full_skeletal_model_after_JSON()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        Assert.Equal(0x1818B78u, resources.NodeSource.ResourceAddress);
        Assert.Equal(17460, resources.NodeSource.Data.Length);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan()));
        Assert.Equal(0x3ED4u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan().Slice(4)));
        Assert.Equal((byte)55, resources.NodeSource.Data[0x4184 + 0x1F]);
        Assert.Equal((byte)55, resources.NodeSource.Data[0x4184 + 0x20]);
        Assert.Equal((byte)22, resources.NodeSource.Data[0x104 + 3]);
        Assert.Equal((byte)23, resources.NodeSource.Data[0x298 + 3]);
        Assert.Equal(85u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan().Slice(0x444 + 4)));
        Assert.Equal(84u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan().Slice(0x1014 + 4)));
        NativeBattleEffectOwnedModelResource model = resources.ReaperModel;
        Assert.Equal(0x1636CE8u, model.Address);
        Assert.Equal(16332, model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x568, 0x2E28, 0x3FCC, 0x3FCC }, Enumerable.Range(0, 5).Select(model.Section));
        Assert.Equal(28, model.PoseSource.BoneCount);
        Assert.Equal(1360, model.PoseSource.Skeleton.Length);
        Assert.Equal(4516, model.PoseSource.Animations.Length);
        Assert.Equal(new byte[] { 1, 16, 6, 14, 1, 24, 18 }, NativeBattlePoseDecoder.ReadFrameCounts(model.PoseSource.Animations.AsSpan()));
        NativeBattleRawMesh geometry = Assert.Single(model.Geometry);
        Assert.Equal(302, geometry.Vertices.Length);
        Assert.Equal(507, geometry.Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(19, geometry.Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(25, geometry.PackedVertexGroups!.Value.Length);
        NativeBattleDoom8Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleDoom8Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources.NodeSource), NativeBattleSnapshotJson.Serialize(restored.NodeSource));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattlePoseDecoder.DecodeFrames(model.PoseSource.Animations.AsSpan(), 28)), NativeBattleSnapshotJson.Serialize(NativeBattlePoseDecoder.DecodeFrames(restored.ReaperModel.PoseSource.Animations.AsSpan(), 28)));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(model.Geometry), NativeBattleSnapshotJson.Serialize(restored.ReaperModel.Geometry));
        Assert.Throws<InvalidDataException>(() => (resources with { NodeSource = resources.NodeSource with { Data = resources.NodeSource.Data.RemoveAt(17459) } }).Validate());
        NativeBattleCuragaExecutableSource invalidChannel = resources.NodeSource with { Data = SetDword(resources.NodeSource.Data, 0x3ED4 + 0xC8, 17460) };
        Assert.Throws<InvalidDataException>(() => (resources with { NodeSource = invalidChannel }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ReaperModel = model with { Bytes = model.Bytes.RemoveAt(16331) } }).Validate());
        NativeBattlePoseSource invalidSkeleton = model.PoseSource with { Skeleton = model.PoseSource.Skeleton.SetItem(16, 0) };
        Assert.Throws<InvalidDataException>(() => (resources with { ReaperModel = model with { PoseSource = invalidSkeleton } }).Validate());
    }

    [Theory]
    [InlineData(383, 2, 6192, 192, 36)]
    [InlineData(384, 2, 2760, 140, 10)]
    [InlineData(385, 5, 7632, 944, 30)]
    public void Original_ACT_sources_keep_the_animation_tail_and_continue_their_actual_workspace_after_JSON(short id, int geometryCount, int animationOffset, int workspaceBytes, int duration)
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        uint destination = id == 383 ? 0x19EB018u : id == 384 ? 0x19ECBB8u : 0x19ED83Cu;
        NativeBattleGfAnimationSource source = resources.ActSource(id, destination);
        Assert.Equal(destination, source.Address);
        Assert.Equal(geometryCount, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()));
        Assert.Equal(animationOffset, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4)));
        Assert.True(source.Data.Length > animationOffset);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaceBytes].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        NativeBattleGfAnimationAdvance first = Advance(workspace);
        Assert.Equal(duration - 1, first.Remaining);
        Assert.True(first.EmittedNodes > 0);
        NativeBattleGfAnimationWorkspace restored = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(first.Workspace))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(Advance(first.Workspace)), NativeBattleSnapshotJson.Serialize(Advance(restored)));

        NativeBattleGfAnimationAdvance Advance(NativeBattleGfAnimationWorkspace current)
        {
            return current.Advance701970(source, 0, (next, node) =>
            {
                Assert.InRange(node.GeometryIndex, 0, geometryCount - 1);
                return next;
            });
        }
    }

    [Fact]
    public void Resource_ingress_rejects_truncation_changed_geometry_and_live_script_destinations()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        byte[] executable = game.ReadNativeExecutable();
        NativeBattleEffectFileAsset original = resources.File(383);
        Assert.Throws<InvalidDataException>(() => (resources with { Files = resources.Files.SetItem(0, original with { Data = original.Data.RemoveAt(original.Data.Length - 1) }) }).Validate());
        NativeBattleEffectFileAsset invalid = original with { Data = original.Data.SetItem(8, 0) };
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8ResourcesReader.Read(executable, resources.MainTim, resources.Files.SetItem(0, invalid)));
        NativeBattleEffectFileAsset absentGeometry = original with { Data = original.Data.SetItem(6212, 2) };
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8ResourcesReader.Read(executable, resources.MainTim, resources.Files.SetItem(0, absentGeometry)));
        NativeBattleEffectHeapAllocation activeScript = resources.InitialIoScript with { Data = resources.InitialIoScript.Data.SetItem(28, 1) };
        Assert.Throws<InvalidDataException>(() => (resources with { InitialIoScript = activeScript }).Validate());
        Assert.Equal((byte)0, resources.InitialIoScript.Data[28]);
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.ActSource(386, 0x19F0324));
    }

    [Fact]
    public void Original_node_bank_materializes_once_and_preserves_live_pointer_fixups_across_JSON()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        NativeBattleDoom8State state = NativeBattleDoom8State.FromColdProcess(resources.InitialIoScript.Data);
        NativeBattleCuragaExecutableState materialized = state.NodeProgram!.Initialize87EA20(resources.NodeSource);
        Assert.Equal(1u, materialized.InitializationFlag);
        Assert.Equal(0x181CA4Cu, BinaryPrimitives.ReadUInt32LittleEndian(materialized.Read(0x1818B7C, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(materialized.Read(0x1818B78, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan()));
        Assert.Same(materialized, materialized.Initialize87EA20(resources.NodeSource));
        state = state with { NodeProgram = materialized };
        NativeBattleDoom8State restored = NativeBattleSnapshotJson.Deserialize<NativeBattleDoom8State>(NativeBattleSnapshotJson.Serialize(state))!;
        restored.Validate();
        NativeBattleCuragaExecutableState continued = restored.NodeProgram!.Initialize87EA20(resources.NodeSource);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(materialized), NativeBattleSnapshotJson.Serialize(continued));
        Assert.Null(NativeBattleDoom8State.FromUnknown().NodeProgram);
        Assert.Throws<InvalidDataException>(() => (state with { NodeProgram = NativeBattleCuragaExecutableState.FromColdProcess() }).Validate());
    }

    [Fact]
    public void Reaper_model_owner_keeps_every_original_animation_identical_across_cold_continuation()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(8, NativeBattleDoom8Reaper.Callback, 320);
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = appended.Hierarchy.Pools };
        NativeBattleDoom8Frame initial = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleDoom8State.FromColdProcess(resources.InitialIoScript.Data)) { Staging = NativeBattleFileStaging.Unknown() };
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Models.Read(initial, appended.Address));
        NativeBattleDoom8Frame initialized = NativeBattleDoom8Models.Initialize(initial, appended.Address, resources.ReaperModel, 3, 0x123456);
        Assert.Null(NativeBattleDoom8Models.Read(initialized, appended.Address).DecodedPose);
        Assert.Equal(resources.ReaperModel.Bytes.ToArray(), initialized.Staging!.Read(resources.ReaperModel.Address, resources.ReaperModel.Bytes.Length));
        Assert.Equal(0x123456u, NativeBattleDoom8Controller.Read(initialized, appended.Address).ReadDword(0x5C));
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resources.ReaperModel.PoseSource.Animations.AsSpan());
        for (short animation = 0; animation < counts.Length; animation++)
        {
            NativeBattleDoom8Frame frame = NativeBattleDoom8Models.Select(initialized, appended.Address, resources.ReaperModel, animation);
            for (int tick = 0; tick < counts[animation] + 1; tick++)
            {
                NativeBattleDoom8Frame cold = NativeBattleSnapshotJson.Deserialize<NativeBattleDoom8Frame>(NativeBattleSnapshotJson.Serialize(frame))!;
                cold.State.Validate();
                NativeBattleDoom8Step warm = NativeBattleDoom8Models.Advance(frame, appended.Address, resources.ReaperModel, true);
                NativeBattleDoom8Step resumed = NativeBattleDoom8Models.Advance(cold, appended.Address, resources.ReaperModel, true);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(warm), NativeBattleSnapshotJson.Serialize(resumed));
                Assert.Single(warm.Frame.State.Models!.Value);
                frame = warm.Frame;
            }
        }
        NativeBattleDoom8Frame mutated = initialized with { Staging = initialized.Staging!.Write(resources.ReaperModel.Address + 0x18, new byte[] { 0xAB, 0xCD }) };
        NativeBattleDoom8Frame replaced = NativeBattleDoom8Models.Initialize(mutated, appended.Address, resources.ReaperModel, 3, 0x654321);
        Assert.Equal(new byte[] { 0xAB, 0xCD }, replaced.Staging!.Read(resources.ReaperModel.Address + 0x18, 2));
        Assert.Single(replaced.State.Models!.Value);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Models.Initialize(initial with { State = NativeBattleDoom8State.FromUnknown() }, appended.Address, resources.ReaperModel, 3, 0));
        Assert.Throws<InvalidDataException>(() => (initialized.State with { Models = [NativeBattleDoom8Models.Read(initialized, appended.Address), NativeBattleDoom8Models.Read(initialized, appended.Address)] }).Validate());
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Doom ingress.");
        return new(directory!);
    }

    private static ImmutableArray<byte> SetDword(ImmutableArray<byte> source, int offset, uint value)
    {
        byte[] bytes = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        return bytes.ToImmutableArray();
    }

    private static ushort[] Block(ImmutableArray<byte> data, int offset)
    {
        return Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(offset + 4 + index * 2))).ToArray();
    }
}
