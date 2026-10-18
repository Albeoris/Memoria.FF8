using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCactuar199ResourcesTests
{
    [Fact]
    public void Original_Cactuar_model_keeps_its_empty_final_section_and_separate_entry_copy_seed()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        Assert.Same(resources, game.ReadNativeCactuar199Resources());
        Assert.Equal(0xE94918u, resources.Model.Address);
        Assert.Equal(0x21E8, resources.Model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x238, 0x208C, 0x21E8, 0x21E8 }, Enumerable.Range(0, 5).Select(resources.Model.Section).ToArray());
        Assert.Equal(11, resources.Model.PoseSource.BoneCount);
        Assert.Equal(new byte[] { 1, 6, 1, 3, 2 }, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).ToArray());
        NativeBattleRawMesh model = Assert.Single(resources.Model.Geometry);
        Assert.Equal(259, model.Vertices.Length);
        Assert.Equal(202, model.Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(146, model.Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(0x21EC, resources.EntryModelCopySeed.Data.Length);
        Assert.Equal(resources.Model.Bytes.ToArray(), resources.EntryModelCopySeed.Data.AsSpan().Slice(0, 0x21E8).ToArray());
        Assert.Equal(0x10u, U32(resources.EntryModelCopySeed.Data, 0x21E8));
        Assert.Equal("FF8_EN:E96B00", resources.ModelTim.Identity);
        Assert.Equal(0x4220, resources.ModelTim.Data.Length);
        Assert.Equal(0x10u, U32(resources.ModelTim.Data, 0));
        Assert.Equal(9u, U32(resources.ModelTim.Data, 4));
        string json = JsonSerializer.Serialize(resources);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleCactuar199Resources>(json)!));
    }

    [Fact]
    public void Original_Cactuar_direct_projected_sprite_and_meshes_keep_their_actual_packet_groups()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        Assert.Equal(0xCF0740u, resources.Sprite.Address);
        Assert.Equal(0x1AC, resources.Sprite.Bytes.Length);
        NativeBattleModuleResource projected = new(0, resources.Sprite.Address, 0, 0, resources.Sprite.Bytes);
        Assert.Equal((byte)7, projected.Kind);
        Assert.Throws<InvalidDataException>(projected.Validate);
        for (int frame = 0; frame < 16; frame++)
        {
            Assert.NotEmpty(projected.ProjectedFrame571C80(frame).Quads);
        }
        Assert.Throws<InvalidDataException>(() => projected.ProjectedFrame571C80(16));
        Assert.Equal(new[] { 0, 0, 0, 0, 24, 48, 0, 0 }, Groups(resources.MeshA));
        Assert.Equal(new[] { 0, 0, 0, 0, 1, 144, 0, 0 }, Groups(resources.MeshB));
        Assert.Equal(new[] { 0, 0, 0, 68, 0, 0, 0, 12 }, Groups(resources.MeshC));
        Assert.Equal(new[] { 0, 0x100 }, resources.Vibration.Bank.Offsets.ToArray());
        Assert.Equal(0x240, resources.Vibration.Bank.Bytes.Length);
        Assert.Equal((ushort)28, resources.Vibration.Bank.ReadWord(0x100));
        Assert.Equal((ushort)288, resources.Vibration.Bank.ReadWord(0x102));
        Assert.Equal(0x400, resources.RandomTape.Data.Length);
        Assert.Equal(0x10, resources.AlignmentAxis.Data.Length);
        Assert.Equal("FF8_EN:CF3578", resources.NeedleAlignmentAxis.Identity);
        Assert.Equal(0x10, resources.NeedleAlignmentAxis.Data.Length);
        Assert.Equal(0x10, resources.EarlyColors.Data.Length);
        Assert.Equal(0x18, resources.LateColors.Data.Length);
        Assert.Equal(8, resources.Sounds.Data.Length);
        Assert.Equal(8, resources.Voices.Data.Length);
        Assert.Equal(156u, resources.Voice1);
        Assert.Equal(157u, resources.Voice2);
        Assert.Equal("mag198.tim", resources.InitializerHeapTim.Identity);
        Assert.NotEmpty(resources.InitializerHeapTim.Data);
    }

    [Fact]
    public void Original_Cactuar_camera_runs_its_bounded_program_and_restores_the_next_unpaused_callback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        Assert.Equal(0xAA, resources.CameraProgram.Data.Length);
        NativeBattleGuardianCameraBank bank = new(0, 0, 123, 7, default, default, default, 0);
        NativeBattleGuardianCameraProcess process = new(bank, bank, 1, null);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.CameraProgram.Data, 0);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 120, -30, 50, 4096);
        NativeBattleCameraPose pose = new(new(1, 2, 3), new(4, 5, 6), 288, 0);
        bool restoredBoundary = false;
        int publications = 0;
        for (int callback = 0; callback < 512; callback++)
        {
            if (callback == 37)
            {
                NativeBattleGuardianCameraStep paused = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, true);
                Assert.Equal(task.Offset, paused.Task.Offset);
                Assert.Equal(task.Frame, paused.Task.Frame);
                Assert.Equal(task.Process.Eye.Remaining, paused.Task.Process.Eye.Remaining);
                Assert.Equal(task.Process.Target.Remaining, paused.Task.Process.Target.Remaining);
                NativeBattleGuardianCameraTask cold = JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(paused.Task))!;
                NativeBattleCameraPose coldPose = JsonSerializer.Deserialize<NativeBattleCameraPose>(JsonSerializer.Serialize(paused.Pose))!;
                Assert.Equal(JsonSerializer.Serialize(NativeBattleGuardianCameraProgram.Advance63E9C0(paused.Task, matrix, paused.Pose, false)), JsonSerializer.Serialize(NativeBattleGuardianCameraProgram.Advance63E9C0(cold, matrix, coldPose, false)));
                task = paused.Task;
                pose = paused.Pose;
                restoredBoundary = true;
            }
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false);
            task = step.Task;
            pose = step.Pose;
            if (step.Result == 2)
            {
                Assert.False(step.Published);
                break;
            }
            Assert.True(step.Published);
            publications++;
        }
        Assert.True(restoredBoundary);
        Assert.True(task.Complete);
        Assert.Equal(0xAA, task.Offset);
        Assert.True(publications > 37);
    }

    private static int[] Groups(NativeBattleStaticMeshResource mesh) => Enumerable.Range(0, 8).Select(group => NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count).ToArray();
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
