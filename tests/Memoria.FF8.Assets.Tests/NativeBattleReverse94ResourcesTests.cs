using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleReverse94ResourcesTests
{
    [Fact]
    public void Original_Reverse_profile_retains_distinct_resource_owners_and_the_larger_six_source_bank()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([94])[93];
        Assert.Equal(0x7475F0u, binding.Entry);
        Assert.Equal(0x7475D0u, binding.Initializer);
        Assert.Equal("mag093.tim", binding.InitializerAsset!.Name);
        NativeBattleRecover93Resources resources = Assert.IsType<NativeBattleRecover93Resources>(binding.Reverse94Resources);
        Assert.Null(binding.Recover93Resources);
        NativeBattleRecover93Resources recover = game.ReadNativeRecover93Resources();
        Assert.Equal(0x1F4, resources.Animation.Data.Length);
        Assert.Equal(0x153407Cu, resources.Model.Address);
        Assert.Equal(0x4694, resources.Model.Bytes.Length);
        Assert.Equal(recover.Model.Bytes.ToArray(), resources.Model.Bytes.ToArray());
        Assert.NotEqual(recover.Model.Address, resources.Model.Address);
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(new byte[] { 6, 40, 6 }, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).ToArray());
        Assert.Equal(302, Assert.Single(resources.Model.Geometry).Vertices.Length);
        Assert.Equal(0x8420, resources.ModelTim.Data.Length);
        Assert.NotEqual(recover.ModelTim.Identity, resources.ModelTim.Identity);
        Assert.Equal(0x32808080u, resources.DefaultColor44);
        ImmutableArray<byte> bank = resources.Nodes.Data;
        Assert.Equal(0x2C00, bank.Length);
        Assert.Equal(0u, U32(bank, 0));
        Assert.Equal(new uint[] { 0x23F0, 0x2548, 0x26A0, 0x27F8, 0x2950, 0x2AA8 }, Enumerable.Range(0, 6).Select(index => U32(bank, 4 + index * 4)).ToArray());
        Assert.Equal(0x2C00u, U32(bank, 4 + 5 * 4) + 0x158);
        Assert.Equal(new uint[] { 0x104, 0x248, 0, 0x38C, 0x4D0, 0x614 }, Enumerable.Range(0, 6).Select(index => U32(bank, 0x44 + index * 4)).ToArray());
        Assert.Equal(0x7DCu, U32(bank, 0x84 + 2 * 4));
        Assert.All(Enumerable.Range(0, 16), index => Assert.Equal(0u, U32(bank, 0xC4 + index * 4)));
        Assert.Equal(0x16AD980u, resources.ProgramSource.Address);
        Assert.Equal(0x510, resources.ProgramSource.Data.Length);
        Assert.NotEqual(recover.ProgramSource.Address, resources.ProgramSource.Address);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        var upload = textures.EnqueueTim505E30(resources.ModelTim);
        Assert.Equal(0x8420, upload.NextOffset);
        Assert.NotEmpty(upload.State.Drain505D20().Uploads);
        Assert.Same(resources, game.ReadNativeReverse94Resources());
        Assert.Null(game.ReadNativeEffectCatalog()[93].Reverse94Resources);
        string json = NativeBattleSnapshotJson.Serialize(binding);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleEffectBinding>(json)!));
        Assert.Null(game.ReadNativeEffectCatalogForEffects([93])[92].Reverse94Resources);
        Assert.Throws<NotSupportedException>(() => NativeBattleReverse94ResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void Original_Reverse_GF_alias_advances_all_twelve_frames_and_restores_its_own_workspace()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeReverse94Resources().ProgramSource;
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x74DBB0, new byte[0x17C - 12].ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, 0xE0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x86, unchecked((ushort)-256));
        task = NativeBattleSearchProgramDrawing.InitializeWorkspace7016B0(task, source);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int packets = 0;
        for (int frame = 0; frame < 12; frame++)
        {
            string? taskCheckpoint = frame == 5 ? NativeBattleSnapshotJson.Serialize(task) : null;
            NativeBattleSearchProgramDraw next = NativeBattleSearchProgramDrawing.Advance758D70(task, source, view, projection, colors);
            Assert.Equal(11 - frame, next.Remaining);
            Assert.Equal((uint)(frame + 1), next.Task.ReadDword(0x98));
            if (frame == 5)
            {
                NativeBattleModulePoolTask restored = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(taskCheckpoint!)!;
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleSearchProgramDrawing.Advance758D70(restored, source, view, projection, colors)));
            }
            packets += next.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = next.Task;
        }
        Assert.True(packets > 0);
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
