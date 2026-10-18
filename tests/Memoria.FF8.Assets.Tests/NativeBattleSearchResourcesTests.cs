using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSearchResourcesTests
{
    [Fact]
    public void Original_program_retains_uncleared_workspace_suffix_and_restores_before_the_next_resource_frame()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeSearchResources().ProgramSource;
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0, Enumerable.Repeat((byte)0xA5, 0x17C - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, 0xE0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, 0).WriteWord(0x86, unchecked((ushort)-256));
        task = NativeBattleSearchProgramDrawing.InitializeWorkspace7016B0(task, source);
        Assert.All(task.Payload.AsSpan().Slice(0x174 - 12, 8).ToArray(), value => Assert.Equal(0xA5, value));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        int packets = 0;
        for (int frame = 0; frame < 12; frame++)
        {
            NativeBattleSearchProgramDraw advanced = NativeBattleSearchProgramDrawing.Advance758D70(task, source, view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
            if (frame == 5)
            {
                NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
                NativeBattleSearchProgramDraw cold = NativeBattleSearchProgramDrawing.Advance758D70(restored, source, view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
                Assert.Equal(JsonSerializer.Serialize(advanced), JsonSerializer.Serialize(cold));
            }
            Assert.Equal(11 - frame, advanced.Remaining);
            packets += advanced.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = advanced.Task;
        }
        Assert.True(packets > 0);
        Assert.All(task.Payload.AsSpan().Slice(0x174 - 12, 8).ToArray(), value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void Original_Search_resources_keep_model_TIM_effect_TIM_and_target_override_separate()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([92])[91];
        Assert.Equal(0x755110u, binding.Initializer);
        Assert.Equal("mag091.tim", binding.InitializerAsset!.Name);
        NativeBattleSearchResources resources = Assert.IsType<NativeBattleSearchResources>(binding.SearchResources);
        Assert.Equal(0x278, resources.Animation.Data.Length);
        Assert.Equal(0x8420, resources.ModelProfile.ModelTim.Data.Length);
        Assert.Equal(0x153D59Cu, resources.ModelProfile.Model.Address);
        Assert.Equal(0x153D1E8u, resources.Sprite8.Address);
        Assert.Equal(0xDC, resources.Sprite8.Bytes.Length);
        Assert.NotEqual((byte)0, resources.Sprite8.Bytes[3]);
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite8.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0x153D2C4u, resources.ParticleMesh.Address);
        Assert.Equal(0x60, resources.ParticleMesh.Bytes.Length);
        for (int group = 0; group < 8; group++)
        {
            Assert.Equal(group == 3 ? 1 : 0, NativeBattleIceMeshVertices.Group(resources.ParticleMesh.Bytes.AsSpan(), group).Count);
        }
        Assert.Equal(0x1D0C, resources.TargetOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 0x1A68, 0x1A84, 0x1D0C }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.TargetOverride.Data.AsSpan(), index)).ToArray());
        AssertProgram(resources.ProgramSource);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation);
        Assert.Equal(resources.Animation, textures.Animation!.Source);
        Assert.Same(resources, game.ReadNativeSearchResources());
        Assert.Null(game.ReadNativeEffectCatalog()[91].SearchResources);
        string json = JsonSerializer.Serialize(binding);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!));
        Assert.Throws<NotSupportedException>(() => NativeBattleSearchResourcesReader.Read(new byte[128]));
    }

    private static void AssertProgram(NativeBattleGfAnimationSource source)
    {
        Assert.Equal(0x16C0DB8u, source.Address);
        Assert.Equal(0x510, source.Data.Length);
        foreach (int start in new[] { 0x10, 0xE4 })
        {
            ImmutableArray<byte> geometry = source.Data.Slice(start, 0xD4);
            Assert.Equal(0x54, BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan()));
            Assert.Equal(9, BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan().Slice(4)));
            Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan().Slice(8)));
            for (int group = 0; group < 8; group++)
            {
                Assert.Equal(group == 3 ? 4 : 0, NativeBattleIceMeshVertices.Group(geometry.AsSpan(), group).Count);
            }
        }
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0xE8].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, 0xE0);
        List<uint> headers = [];
        for (int frame = 0; frame < 12; frame++)
        {
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
            {
                Assert.Equal((short)0, node.Word40);
                Assert.Equal((short)0, node.Word42);
                if (frame == 0)
                {
                    headers.Add(node.Header);
                }
                return current;
            });
            Assert.Equal(11 - frame, advanced.Remaining);
            workspace = advanced.Workspace;
        }
        Assert.Equal(new uint[] { 0x200, 0x200, 0x201, 0x200 }, headers.ToArray());
    }
}
