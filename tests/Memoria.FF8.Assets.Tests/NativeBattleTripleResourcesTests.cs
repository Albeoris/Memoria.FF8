using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTripleResourcesTests
{
    [Fact]
    public void Authenticated_cold_mesh_is_retained_across_loads_and_missing_old_owner_stays_unknown()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleTripleMorph current = Assert.IsType<NativeBattleTripleMorph>(memory.TripleMorph);
        Assert.Equal(Assert.Single(game.ReadNativeTripleResources().Meshes, mesh => mesh.Address == 0x1627970).Bytes.ToArray(), current.Bytes.ToArray());
        Assert.Same(current, memory.BeginLoad(20).TripleMorph);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().TripleMorph);
        JsonObject saved = JsonNode.Parse(NativeBattleSnapshotJson.Serialize(memory))!.AsObject();
        Assert.True(saved.Remove("TripleMorph"));
        NativeBattleEffectMemory old = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(saved.ToJsonString())!;
        Assert.Null(old.TripleMorph);
        Assert.Null(old.BeginLoad(20).TripleMorph);
    }

    [Fact]
    public void Original_Triple_resources_keep_distinct_native_bounds_curves_and_kernel_identity()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.MagicId == 21);
        Assert.Equal("Triple", action.Name);
        Assert.Equal(34, action.Id);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalog()[20];
        Assert.Equal(0x89DEF0u, binding.Entry);
        Assert.Equal(0x89DED0u, binding.Initializer);
        Assert.Equal("mag020.tim", binding.InitializerResource!.Name);
        Assert.Equal(0x2714F28u, binding.InitializerResource.GlobalAddress);
        NativeBattleTripleResources resources = game.ReadNativeTripleResources();
        Assert.Same(resources, game.ReadNativeTripleResources());
        Assert.Equal(300027u, resources.Sound);
        Assert.Equal(0x98C, resources.Animation.Data.Length);
        Assert.Equal(new uint[] { 0x16269DC, 0x1626B3C, 0x1626C18, 0x1626CC8 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 13, 8, 10, 1 }, resources.Sprites.Select(sprite => (int)BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))));
        Assert.Equal(new uint[] { 0x1626CF0, 0x1626F00, 0x1627160, 0x1627380, 0x1627970, 0x1628218 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new[] { 17, 24, 24, 32, 80, 128 }, resources.Meshes.Select(mesh => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4))));
        Assert.Equal(new[] { 528, 608, 544, 872, 2216, 3560 }, resources.Meshes.Select(mesh => mesh.Bytes.Length));
        Assert.Equal(new ushort[] { 0, 1243, 2091, 2657, 3060, 3383, 3635, 3822, 3954, 4038, 4083, 4096 }, resources.Curve89E5E0);
        Assert.Equal(new ushort[] { 0, 893, 1611, 2198, 2664, 3016, 3303, 3561, 3779, 3948, 4057, 4096 }, resources.Curve89E620);
        Assert.Equal("7E3EDEE00D48C798F4EAA0E7130CE17D46D8C70201BA4E456259CC492C67EFCA", Convert.ToHexString(SHA256.HashData(resources.Meshes[4].Bytes.AsSpan())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Actual_eighty_vertex_morph_preserves_current_fourth_WORDs_and_all_mesh_records(short ratio)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleTripleResources resources = game.ReadNativeTripleResources();
        NativeBattleTripleMorph state = new(Assert.Single(resources.Meshes, mesh => mesh.Address == 0x1627970).Bytes);
        NativeBattleTripleMorph next = state.Morph89E6E0(resources.MorphFirst, resources.MorphSecond, ratio);
        Assert.Equal(state.Bytes.AsSpan().Slice(0, 8).ToArray(), next.Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(state.Bytes.AsSpan().Slice(648).ToArray(), next.Bytes.AsSpan().Slice(648).ToArray());
        for (int vertex = 0; vertex < 80; vertex++)
        {
            int offset = 8 + vertex * 8;
            Assert.Equal(state.Bytes.AsSpan().Slice(offset + 6, 2).ToArray(), next.Bytes.AsSpan().Slice(offset + 6, 2).ToArray());
            if (ratio is 0 or 4096)
            {
                var source = ratio == 0 ? resources.MorphFirst : resources.MorphSecond;
                Assert.Equal(source.AsSpan().Slice(offset, 6).ToArray(), next.Bytes.AsSpan().Slice(offset, 6).ToArray());
            }
        }
        Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleTripleMorph>(NativeBattleSnapshotJson.Serialize(next))));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void Original_entry_registers_its_actual_animation_and_TIM_only_on_the_native_flag_branch(byte flags)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleTripleResources resources = game.ReadNativeTripleResources();
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(21, game.ReadNativeEffectCatalogForEffects([21]));
        var initialized = NativeBattleOriginalEffects.Initialize(game.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
        NativeBattleOriginalEffectState effect = initialized.Effect with { Parameters = new(0, flags, 8, 0, 0, 0) };
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 34, 21, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleTripleFrame frame = NativeBattleTripleController.Initialize89DEF0(initialized.Memory, effect, packets, resources);
        Assert.Equal(0x271B0F0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(flags == 0 ? 1 : 0, frame.Memory.Textures!.Count);
        if (flags == 0)
        {
            Assert.Equal(8, frame.Memory.Textures.Animation!.StreamOffset);
            Assert.Equal(0x144, frame.Memory.Textures.Animation.ModelTableOffset);
            Assert.Equal(2, frame.Memory.Textures.Drain505D20(frame.Memory.Arena).Uploads.Length);
        }
        else
        {
            NativeBattleTripleFrame skipped = NativeBattleTripleController.Initialize89DEF0(initialized.Memory with { Textures = null, Arena = null }, effect with { ResourcePointers = [] }, packets, resources);
            Assert.Null(skipped.Memory.Textures);
        }
    }
}
