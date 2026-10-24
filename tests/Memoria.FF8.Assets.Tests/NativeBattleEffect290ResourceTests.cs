using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect290Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect290ResourceTests
{
    [Fact]
    public void Original_programmes_geometry_meshes_and_completion_file_have_bounded_extents()
    {
        using GameAssets game = Open();
        NativeBattleEffect290Resources resources = game.ReadNativeEffect290Resources();
        resources.Validate();
        Assert.Equal(199200, resources.MainTim.Data.Length);
        Assert.Equal(3, resources.Programmes.Count);
        Assert.Equal(5, resources.Geometry.Count);
        Assert.Equal(4, resources.Meshes.Count);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            Assert.Equal(ActLengths[index], resources.Act(ActAddresses[index]).Data.Length);
            Assert.Equal(ActWorkspaceLengths[index], resources.WorkspaceLength(ActAddresses[index]));
        }
        for (int index = 0; index < GeometryAddresses.Length; index++)
        {
            Assert.Equal(GeometryLengths[index], resources.ReadGeometry(GeometryAddresses[index]).Bytes.Length);
        }
        foreach (uint address in MeshAddresses)
        {
            Assert.Equal(184, resources.ReadMesh(address).Bytes.Length);
        }
        Assert.Equal("MA8DEF_0.TIM", resources.ReadFile(360).Name);
        Assert.Equal(33824, resources.ReadFile(360).Data.Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(361));
    }

    [Fact]
    public void Distinct_adjacent_main_sprite_and_custom_quad_directories_preserve_native_domains()
    {
        using GameAssets game = Open();
        NativeBattleEffect290Resources resources = game.ReadNativeEffect290Resources();
        int[] counts = [12, 13, 16];
        for (int source = 0; source < SpriteAddresses.Length; source++)
        {
            NativeBattleModuleResource sprite = resources.Sprite(SpriteAddresses[source]);
            Assert.Equal(counts[source], (int)BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(8, 2)));
            for (int frame = 0; frame < counts[source]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        Assert.Equal(MainSpriteAddress, FirstSpriteAddress + (uint)resources.Sprite(FirstSpriteAddress).Bytes.Length);
        NativeBattleModuleResource quads = resources.ReadProjectedQuads(SmokeSourceAddress);
        Assert.Equal(328, quads.Bytes.Length);
        for (int life = 12; life > 0; life--)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(quads.Bytes.AsSpan(2 * (17 - life), 2));
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(quads.Bytes.AsSpan(offset, 4)));
            Assert.InRange(offset + 24, 24, quads.Bytes.Length);
        }
        Assert.Throws<NotSupportedException>(() => resources.Sprite(SmokeSourceAddress));
    }

    [Fact]
    public void Original_script_counts_bind_only_once_and_overlay_current_JSON_cells()
    {
        using GameAssets game = Open();
        NativeBattleEffect290Resources resources = JsonSerializer.Deserialize<NativeBattleEffect290Resources>(JsonSerializer.Serialize(game.ReadNativeEffect290Resources()))!;
        resources.Validate();
        (NativeBattleEffect290State state, NativeBattleFileStaging staging) = NativeBattleEffect290State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        Assert.Equal(19, ScriptRanges.Length);
        Assert.Equal(23, ScriptCountAddresses.Length);
        foreach (uint address in ScriptCountAddresses)
        {
            Assert.True(BinaryPrimitives.ReadInt16LittleEndian(resources.OriginalScriptCount(address)) > 0);
            Assert.Equal(resources.OriginalScriptCount(address), resources.ReadScript(staging, address, 2));
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, 2));
            staging = staging.Write(address, new byte[2]);
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect290State>(JsonSerializer.Serialize(state))!;
        staging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
        (state, staging) = state.BindSources(staging, resources);
        Assert.False(state.ScriptSourcePending);
        foreach (uint address in ScriptCountAddresses)
        {
            Assert.Equal((short)0, resources.ReadScriptWord(staging, address));
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadScript(staging, ScriptRanges[0].Address, ScriptRanges[0].Length + 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Selected_catalog_retains_camera_vibration_and_complete_static_lookup_widths()
    {
        using GameAssets game = Open();
        NativeBattleEffect290Resources resources = game.ReadNativeEffect290Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([290])[289];
        Assert.Same(resources, binding.Effect290Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag289.tim", InitializerGlobal), binding.InitializerResource);
        Assert.Equal(new ushort[] { 2, 8, 24, 192 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(index * 2, 2))));
        Assert.Equal(296, resources.Vibration.Data.Length);
        resources.VibrationBank.Validate();
        Assert.Equal(16384, resources.ReadStatic(TrigonometryAddress, TrigonometryLength).Length);
        Assert.Equal(384, resources.ReadStatic(NormalizationAddress, NormalizationLength).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(NormalizationAddress, NormalizationLength + 1));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect290 resources.");
        return new(directory!);
    }
}
