using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuraResourcesTests
{
    [Theory]
    [InlineData(0x161194Cu, 14, 52, 0x30u)]
    [InlineData(0x1611C20u, 18, 100, 0x40000030u)]
    public void Original_scene_channels_emit_the_actual_meshes_for_their_complete_lifetime_and_restore_next_callback(uint address, int duration, int clearBytes, uint flags)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleCuraSceneResource scene = game.ReadNativeCuraResources().Scenes.Single(item => item.Address == address);
        NativeBattleGfAnimationSource source = new(scene.Address, scene.Data);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[104].ToImmutableArray());
        workspace = workspace.Initialize88A7D0(source, clearBytes);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        int packets = 0;
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationWorkspace independent = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            List<string> actual = [];
            List<string> restored = [];
            NativeBattleGfAnimationAdvance step = workspace.Advance88A980(source, (current, node) =>
            {
                NativeBattleModulePacketBatch batch = NativeBattleCuraScenePackets.Emit88AF90(scene, node, camera, projection, flags, 0);
                packets += batch.Primitives.Length;
                actual.Add(JsonSerializer.Serialize(batch));
                return current;
            });
            NativeBattleGfAnimationAdvance clone = independent.Advance88A980(source, (current, node) =>
            {
                restored.Add(JsonSerializer.Serialize(NativeBattleCuraScenePackets.Emit88AF90(scene, node, camera, projection, flags, 0)));
                return current;
            });
            Assert.Equal(actual, restored);
            Assert.Equal(step.Workspace.Data.ToArray(), clone.Workspace.Data.ToArray());
            Assert.Equal(duration - frame - 1, step.Remaining);
            Assert.Equal(1, step.EmittedNodes);
            workspace = step.Workspace;
        }
        Assert.True(packets > 0);
        Assert.Equal(0, workspace.Advance88A980(source, (_, _) => throw new InvalidOperationException("Completed native scenes emit no node.")).EmittedNodes);
    }

    [Fact]
    public void Real_Cura_scene_meshes_use_their_explicit_plus_twelve_vertex_pointer_and_keep_the_default_mesh_path_equivalent()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleCuraResources resources = game.ReadNativeCuraResources();
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = new(0, 0, 0, 0), MeshRotation = new(0, 0, 0), MeshScale = new(4096, 4096, 4096), MeshFlags = 0x30 };
        List<NativeBattleModulePacketBatch> emitted = [];
        foreach (NativeBattleCuraSceneResource scene in resources.Scenes)
        {
            NativeBattleCuraMeshFrame selected = NativeBattleCuraSceneMesh.Select(scene, 0, 0);
            NativeBattleModulePacketBatch original = NativeBattleModulePackets.EmitMesh572200WithVertices(draw with { MeshFlags = 0x2030 }, selected.Resource, projection, camera, selected.Vertices, 17, 2);
            Assert.NotEmpty(original.Primitives);
            //Equivalent conventional mesh storage removes the scene-only third header DWORD; only this test rebases its vertex pointer.
            byte[] regular = selected.Resource.Bytes.AsSpan().Slice(0, 8).ToArray().Concat(selected.Resource.Bytes.AsSpan().Slice(12).ToArray()).ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(regular, BinaryPrimitives.ReadInt32LittleEndian(regular) - 4);
            NativeBattleStaticMeshResource conventional = selected.Resource with { Bytes = regular.ToImmutableArray() };
            NativeBattleModulePacketBatch baseline = NativeBattleModulePackets.EmitMesh572200(draw, conventional, projection, 17, 2);
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(baseline));
            emitted.Add(original);
        }
        Assert.Contains(emitted[0].Primitives, primitive => (primitive.Primitive.Words[1] >> 24 & 0xFC) == 0x2C);
        Assert.Contains(emitted[1].Primitives, primitive => (primitive.Primitive.Words[1] >> 24 & 0xFC) == 0x28);
        Assert.Contains(emitted[1].Primitives, primitive => (primitive.Primitive.Words[1] >> 24 & 0xFC) == 0x38);
    }

    [Fact]
    public void Original_Cura_resources_keep_all_sprite_banks_both_scene_resources_and_signed_emission_tables()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleCuraResources resources = game.ReadNativeCuraResources();
        Assert.Equal(2120, resources.Animation.Data.Length);
        Assert.Equal(0x4942Du, resources.Sound);
        Assert.Equal(new uint[] { 0x161079C, 0x1610948, 0x16109BC, 0x1610A10, 0x1610AD4, 0x1610C00, 0x1610DAC, 0x1610F58 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 16, 4, 2, 7, 11, 16, 16, 16 }, resources.Sprites.Select(sprite => sprite.Bytes[8] | sprite.Bytes[9] << 8));
        Assert.Equal(new[] { 724, 6756 }, resources.Scenes.Select(scene => scene.Data.Length));
        Assert.Equal(new[] { 30, 30, 30 }, resources.EmissionSchedules.Select(schedule => schedule.Length));
        foreach (NativeBattleModuleResource sprite in resources.Sprites)
        {
            sprite.Validate();
        }
    }

    [Fact]
    public void Entry_uploads_the_actual_loaded_TIM_and_registers_the_actual_camera_while_draw_banks_come_from_literal571B50()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(25, game.ReadNativeEffectCatalogForEffects([25]));
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 22, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(initialized.Memory, initialized.Effect with { Parameters = new(0, 0, 8, 0, 0, 0) }, packets, game.ReadNativeCuraResources());
        Assert.Equal(0x26E5A00u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0, frame.State.OddDrawBase26FD114);
        Assert.Equal(0x12000, frame.State.EvenDrawBase26FD110);
        Assert.Null(frame.State.PrimaryDrawCursor);
        Assert.Equal(8, frame.Memory.Textures!.Animation!.StreamOffset);
        Assert.Equal(0x144, frame.Memory.Textures.Animation.ModelTableOffset);
        NativeBattleEffectTextureDrain drain = frame.Memory.Textures.Drain505D20(frame.Memory.Arena);
        Assert.Equal(2, drain.Uploads.Length);
        Assert.Equal(0, drain.State.Count);
    }
}
