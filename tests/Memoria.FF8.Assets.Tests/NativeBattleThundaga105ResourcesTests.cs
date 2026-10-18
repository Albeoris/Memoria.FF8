using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleThundaga105ResourcesTests
{
    [Fact]
    public void Original_Thundaga_profile_keeps_complete_program_sprites_selector_and_animation_without_dispatch()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleThundaga105Resources resources = game.ReadNativeThundaga105Resources();
        Assert.Same(resources, game.ReadNativeThundaga105Resources());
        Assert.Equal(0x13009C0u, resources.ProgramSource.Address);
        Assert.Equal(0xDB9C, resources.ProgramSource.Data.Length);
        Assert.Equal("FF8_EN:1311720", resources.Animation.Identity);
        Assert.Equal(0xF3C, resources.Animation.Data.Length);
        Assert.Equal(0x131136Cu, resources.SpriteStationary.Address);
        Assert.Equal(0x270, resources.SpriteStationary.Bytes.Length);
        Assert.Equal(0x13115DCu, resources.SpriteMoving.Address);
        Assert.Equal(0x144, resources.SpriteMoving.Bytes.Length);
        foreach ((NativeBattleModuleResource sprite, int frames) in new[] { (resources.SpriteStationary, 15), (resources.SpriteMoving, 12) })
        {
            Assert.Equal((byte)0, sprite.Kind);
            Assert.Equal(frames, BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)));
            for (int frame = 0; frame < frames; frame++)
            {
                Assert.Equal(0, sprite.SpriteFrame(frame).Quads.Length % 20);
            }
        }
        Assert.Equal("FF8_EN:131265C", resources.SoundDescriptor.Identity);
        Assert.Equal(4, resources.SoundDescriptor.Data.Length);
        Assert.Equal(300053u, resources.Sound);
        Assert.Equal("FF8_EN:1312660", resources.NodeSelector.Identity);
        Assert.Equal(32, resources.NodeSelector.Data.Length);
        Assert.Equal(new[] { 16, 17 }, Enumerable.Range(0, 32).Where(index => resources.NodeSelector.Data[index] != 0).ToArray());
        Assert.All(resources.NodeSelector.Data, value => Assert.True(value is 0 or 1));
        Assert.NotNull(NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Animation).Animation);
        string json = JsonSerializer.Serialize(resources);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleThundaga105Resources>(json)!));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaga105ResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void All_fourteen_original_fragment_meshes_keep_their_distinct_bounded_reference_banks()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleThundaga105Resources resources = game.ReadNativeThundaga105Resources();
        uint[] meshAddresses = [0x130E55C, 0x130E8E4, 0x130ECB4, 0x130EED4, 0x130F18C, 0x130F55C, 0x130F824, 0x130FADC, 0x130FD94, 0x131002C, 0x131052C, 0x13109DC, 0x1310CDC, 0x1310F8C];
        uint[] referenceAddresses = [0x130E86C, 0x130EC34, 0x130EE84, 0x130F12C, 0x130F4DC, 0x130F7C4, 0x130FA7C, 0x130FD34, 0x130FFCC, 0x131048C, 0x1310944, 0x1310C74, 0x1310F2C, 0x13112EC];
        int[] meshLengths = [784, 848, 464, 600, 848, 616, 600, 600, 568, 1120, 1048, 664, 592, 864];
        int[] vertices = [14, 15, 9, 11, 15, 11, 11, 11, 11, 19, 18, 12, 11, 15];
        int[] ft3 = [5, 5, 5, 4, 5, 2, 4, 4, 8, 3, 4, 4, 5, 3];
        int[] gt3 = [19, 21, 9, 14, 21, 16, 14, 14, 10, 31, 28, 16, 13, 23];
        Assert.Equal(14, resources.FragmentPairs.Length);
        for (int index = 0; index < 14; index++)
        {
            NativeBattleThundagaFragmentPair pair = resources.FragmentPairs[index];
            Assert.Equal(meshAddresses[index], pair.MutableMesh.Address);
            Assert.Equal(meshLengths[index], pair.MutableMesh.Bytes.Length);
            Assert.Equal(referenceAddresses[index], pair.Reference.Address);
            Assert.Equal(vertices[index], (int)U32(pair.MutableMesh.Bytes, 4));
            Assert.Equal(8 + vertices[index] * 8, pair.Reference.Bytes.Length);
            Assert.Equal(new[] { 0, 0, ft3[index], 0, 0, 0, gt3[index], 0 }, Enumerable.Range(0, 8).Select(group => NativeBattleIceMeshVertices.Group(pair.MutableMesh.Bytes.AsSpan(), group).Count).ToArray());
            Assert.Equal(pair.Reference.Address, pair.MutableMesh.Address + (uint)pair.MutableMesh.Bytes.Length);
            Assert.Equal(index == 13 ? resources.SpriteStationary.Address : meshAddresses[index + 1], pair.Reference.Address + (uint)pair.Reference.Bytes.Length);
        }
    }

    [Fact]
    public void Original_sixty_four_frame_program_retains_its_868_workspace_and_independent_next_callback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeThundaga105Resources().ProgramSource;
        Assert.Equal(16u, U32(source.Data, 0));
        Assert.Equal(0xBE70u, U32(source.Data, 4));
        Assert.Equal(32u, U32(source.Data, 0xBE70));
        Assert.Equal(64u, U32(source.Data, 0xBE74));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x868].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, 0x868);
        int nodes = 0;
        for (int frame = 0; frame < 64; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, _) => current);
            Assert.Equal(63 - frame, next.Remaining);
            if (frame == 31)
            {
                NativeBattleGfAnimationWorkspace cold = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold.Advance701970(source, 0, (current, _) => current)));
            }
            nodes += next.EmittedNodes;
            workspace = next.Workspace;
        }
        Assert.True(nodes > 0);
        Assert.Equal(64, workspace.Frame);
    }

    [Fact]
    public void Original_multi_shape_geometry_fits_exact_7D0_scratch_and_preserves_each_actual_fourth_WORD()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeThundaga105Resources().ProgramSource;
        ImmutableArray<byte> scratch = Enumerable.Range(0, 0x7D0).Select(index => unchecked((byte)index)).ToImmutableArray();
        for (int index = 0; index < 2; index++)
        {
            int geometry = checked((int)U32(source.Data, 8 + index * 4));
            Assert.Equal(250u, U32(source.Data, geometry + 4));
            Assert.Equal(index == 0 ? 3u : 4u, U32(source.Data, geometry + 8));
            ImmutableArray<byte> morphed = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, 0, 1, 2048, scratch);
            Assert.Equal(0x7D0, morphed.Length);
            for (int vertex = 0; vertex < 250; vertex++)
            {
                Assert.Equal(scratch[vertex * 8 + 6], morphed[vertex * 8 + 6]);
                Assert.Equal(scratch[vertex * 8 + 7], morphed[vertex * 8 + 7]);
            }
        }
    }

    [Fact]
    public void Original_primary_draw_runs_all64_frames_with_shared_morph_padding_custom_windows_and_cold_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleThundaga105Resources resources = game.ReadNativeThundaga105Resources();
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        ImmutableArray<byte> anchor = new byte[8].ToImmutableArray();
        byte[] scratch = Enumerable.Range(0, 0x7D0).Select(index => unchecked((byte)index)).ToArray();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = new NativeBattleEffectArena([]).Write(0x2254, scratch) };
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x6D43B0, new byte[0x884 - 12].ToImmutableArray()).WriteDword(0x18, 4096);
        task = NativeBattleThundaga105ProgramDrawing.InitializeWorkspace7016B0(task, resources.ProgramSource);
        int allocations = 0;
        int linked = 0;
        int windows = 0;
        bool scratchChanged = false;
        for (int frame = 0; frame < 64; frame++)
        {
            NativeBattleThundaga105ProgramDraw next = NativeBattleThundaga105ProgramDrawing.Advance6D43B0(memory, task, resources.ProgramSource, resources.NodeSelector.Data, anchor, view, view, projection, colors);
            Assert.Equal(63 - frame, next.Remaining);
            Assert.Equal((uint)(frame + 1), next.Task.ReadDword(0x20));
            if (frame == 31)
            {
                PrimaryState restored = JsonSerializer.Deserialize<PrimaryState>(JsonSerializer.Serialize(new PrimaryState(memory, task)))!;
                NativeBattleThundaga105ProgramDraw cold = NativeBattleThundaga105ProgramDrawing.Advance6D43B0(restored.Memory, restored.Task, resources.ProgramSource, resources.NodeSelector.Data, anchor, view, view, projection, colors);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold));
            }
            byte[] currentScratch = next.Memory.Arena!.Read(0x2254, 0x7D0);
            scratchChanged |= !scratch.AsSpan().SequenceEqual(currentScratch);
            for (int vertex = 0; vertex < 250; vertex++)
            {
                Assert.Equal(scratch[vertex * 8 + 6], currentScratch[vertex * 8 + 6]);
                Assert.Equal(scratch[vertex * 8 + 7], currentScratch[vertex * 8 + 7]);
            }
            allocations += next.Packets.AllocationBytes;
            foreach (NativeBattleGfGeometryAttempt attempt in next.Packets.Attempts)
            {
                if (attempt.Primitive is NativeBattleGeometryPrimitive primitive)
                {
                    linked++;
                    if (primitive.Words[1] >> 24 == 0xE2)
                    {
                        windows++;
                    }
                }
            }
            memory = next.Memory;
            task = next.Task;
        }
        Assert.True(allocations > 0);
        Assert.True(linked > 0);
        Assert.True(windows > 0);
        Assert.True(scratchChanged);
    }

    private sealed record PrimaryState(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task);

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
