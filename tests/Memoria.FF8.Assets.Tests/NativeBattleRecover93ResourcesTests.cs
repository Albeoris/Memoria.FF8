using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRecover93ResourcesTests
{
    [Fact]
    public void Original_Recover_profile_retains_independent_model_TIM_nodebank_program_and_control_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([93])[92];
        Assert.Equal(0x74E370u, binding.Initializer);
        Assert.Equal("mag092.tim", binding.InitializerAsset!.Name);
        NativeBattleRecover93Resources resources = Assert.IsType<NativeBattleRecover93Resources>(binding.Recover93Resources);
        Assert.Equal(0x410, resources.Animation.Data.Length);
        Assert.Equal(0x1538B38u, resources.Model.Address);
        Assert.Equal(0x4694, resources.Model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x688, 0x3344, 0x4688, 0x4694 }, Enumerable.Range(0, 5).Select(resources.Model.Section).ToArray());
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(0x670, resources.Model.PoseSource.Skeleton.Length);
        Assert.Equal(0x1344, resources.Model.PoseSource.Animations.Length);
        Assert.Equal(new byte[] { 6, 40, 6 }, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).ToArray());
        Assert.Equal(302, Assert.Single(resources.Model.Geometry).Vertices.Length);
        NativeBattleEffectOwnedModelResource search = game.ReadNativeSearchModelResources().Model;
        Assert.NotEqual(search.Address, resources.Model.Address);
        Assert.Equal(search.Bytes.Slice(search.Section(1), search.Section(2) - search.Section(1)).ToArray(), resources.Model.Bytes.Slice(resources.Model.Section(1), resources.Model.Section(2) - resources.Model.Section(1)).ToArray());
        Assert.Equal(0x8420, resources.ModelTim.Data.Length);
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ModelTim.Data.AsSpan().Slice(4)));
        Assert.Equal(0x32808080u, resources.DefaultColor44);
        Assert.Equal(game.ReadNativeRecover93NodeResource().Data.ToArray(), resources.Nodes.Data.ToArray());
        Assert.Equal(0x21C8, resources.Nodes.Data.Length);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        var upload = textures.EnqueueTim505E30(resources.ModelTim);
        Assert.Equal(0x8420, upload.NextOffset);
        Assert.NotEmpty(upload.State.Drain505D20().Uploads);
        Assert.Same(resources, game.ReadNativeRecover93Resources());
        Assert.Null(game.ReadNativeEffectCatalog()[92].Recover93Resources);
        string json = NativeBattleSnapshotJson.Serialize(binding);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleEffectBinding>(json)!));
        Assert.Throws<NotSupportedException>(() => NativeBattleRecover93ResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void All_three_original_poses_draw_with_the_exact140_byte_model_task_and_restore_current_pose()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRecover93Resources resources = game.ReadNativeRecover93Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleActionMatrix view = View();
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        for (short animation = 0; animation < 3; animation++)
        {
            NativeBattleModulePoolTask task = new(1, 0, null, 0, new byte[0x140 - 12].ToImmutableArray());
            NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(task, 0x2200000, resources.Model, 3, resources.DefaultColor44);
            task = initialized.Task.WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(task, initialized.State, resources.Model, animation);
            NativeBattleEffectOwnedModelStep restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectOwnedModelStep>(NativeBattleSnapshotJson.Serialize(selected))!;
            NativeBattleEffectOwnedModelDraw draw = Draw(selected);
            Assert.Equal(0x140 - 12, NativeBattleStorageSnapshots.Capture(draw.Task).Payload.Length);
            Assert.Equal(0x970, draw.ProjectedBytesWritten);
            Assert.NotEmpty(draw.Packets.Attempts);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(draw), NativeBattleSnapshotJson.Serialize(Draw(restored)));
        }

        NativeBattleEffectOwnedModelDraw Draw(NativeBattleEffectOwnedModelStep selected) => NativeBattleEffectOwnedModelDrawing.Draw7558A0(selected.Task, selected.State, resources.Model, view, projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, Enumerable.Repeat((byte)0xA5, 0x970).ToImmutableArray());
    }

    [Fact]
    public void Original_Recover_program_reuses_verified_alias_immediately_and_preserves_target_tail_and_Y()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeRecover93Resources().ProgramSource;
        Assert.Equal(0x16B8480u, source.Address);
        Assert.Equal(0x510, source.Data.Length);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x754950, Enumerable.Repeat((byte)0xA5, 0x17C - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, 0xE0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x24, 0).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteWord(0x1C, 0).WriteWord(0x1E, 123).WriteWord(0x20, 0).WriteWord(0x86, unchecked((ushort)-256)).WriteWord(0x174, 456).WriteWord(0x176, 789).WriteWord(0x178, 1234);
        byte[] tail = NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(0x174 - 12, 8).ToArray();
        task = NativeBattleSearchProgramDrawing.InitializeWorkspace7016B0(task, source);
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleActionMatrix view = View();
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        int packets = 0;
        for (int frame = 0; frame < 12; frame++)
        {
            string? taskCheckpoint = frame == 5 ? NativeBattleSnapshotJson.Serialize(task) : null;
            NativeBattleSearchProgramDraw advanced = NativeBattleSearchProgramDrawing.Advance758D70(task, source, view, projection, rules.SummonColors!);
            Assert.Equal(11 - frame, advanced.Remaining);
            Assert.Equal((uint)(frame + 1), advanced.Task.ReadDword(0x98));
            Assert.Equal((ushort)123, advanced.Task.ReadWord(0x1E));
            Assert.Equal(tail, NativeBattleStorageSnapshots.Capture(advanced.Task).Payload.AsSpan().Slice(0x174 - 12, 8).ToArray());
            if (frame == 5)
            {
                NativeBattleModulePoolTask restored = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(taskCheckpoint!)!;
                Assert.Equal(NativeBattleSnapshotJson.Serialize(advanced), NativeBattleSnapshotJson.Serialize(NativeBattleSearchProgramDrawing.Advance758D70(restored, source, view, projection, rules.SummonColors!)));
            }
            packets += advanced.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = advanced.Task;
        }
        Assert.True(packets > 0);
    }

    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
}
