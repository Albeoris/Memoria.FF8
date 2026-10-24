using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect69ResourceTests
{
    [Fact]
    public void Original_static_fronts_preserve_ascii_selectors_and_complete_programme_lifetime()
    {
        using GameAssets game = Open();
        NativeBattleEffect69Resources resources = game.ReadNativeEffect69Resources();
        resources.Validate();
        Assert.Equal(26036, resources.Programme.Data.Length);
        Assert.Equal("000100200", Encoding.ASCII.GetString(resources.Selectors.AsSpan()));
        Assert.Equal(496, resources.Camera.Data.Length);
        Assert.Equal(280, resources.Vibration.Bytes.Length);
        Assert.Equal(0x64307u, resources.Sound);
        Assert.Equal(480, resources.Sprite.Bytes.Length);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[ProgrammeWorkspaceLength].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Programme, ProgrammeWorkspaceLength);
        HashSet<ushort> selected = [];
        for (int frame = 0; frame < 92; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.Programme, 0, (current, node) =>
            {
                selected.Add(node.GeometryIndex);
                Assert.InRange(node.GeometryIndex, (ushort)0, (ushort)8);
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(resources.Programme.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                Assert.Equal(ProgrammeAddress + offset, resources.ReadGeometry(ProgrammeAddress + offset).Address);
                return current;
            });
            Assert.Equal(91 - frame, next.Remaining);
            workspace = next.Workspace;
        }
        Assert.Contains((ushort)0, selected);
        Assert.Contains((ushort)4, selected);
    }

    [Fact]
    public void Flat_source_groups_use_twelve_byte_faces_and_multi_shape_geometry_remains_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect69Resources resources = game.ReadNativeEffect69Resources();
        NativeBattleStaticMeshResource first = Geometry(0);
        int polygon = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(first.Bytes.AsSpan()));
        Assert.Equal(24u, BinaryPrimitives.ReadUInt32LittleEndian(first.Bytes.AsSpan().Slice(polygon)));
        int group5 = polygon + 4 + 24 * 12 + 4 * 4;
        Assert.Equal(72u, BinaryPrimitives.ReadUInt32LittleEndian(first.Bytes.AsSpan().Slice(group5)));
        NativeBattleStaticMeshResource fourth = Geometry(3);
        Assert.Equal(120u, BinaryPrimitives.ReadUInt32LittleEndian(fourth.Bytes.AsSpan().Slice(4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(fourth.Bytes.AsSpan().Slice(8)));
        Assert.Equal(12u + 120 * 2 * 8, BinaryPrimitives.ReadUInt32LittleEndian(fourth.Bytes.AsSpan()));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ProgrammeAddress + 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SelectorsAddress, 10));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x20DFAB8, 4));

        NativeBattleStaticMeshResource Geometry(int index) => resources.ReadGeometry(ProgrammeAddress + BinaryPrimitives.ReadUInt32LittleEndian(resources.Programme.Data.AsSpan().Slice(8 + index * 4)));
    }

    [Fact]
    public void Resource_json_preserves_selected_fronts_and_rejects_changed_dimensions()
    {
        using GameAssets game = Open();
        NativeBattleEffect69Resources resources = game.ReadNativeEffect69Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleEffect69Resources restored = JsonSerializer.Deserialize<NativeBattleEffect69Resources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { Selectors = resources.Selectors.SetItem(3, (byte)1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Programme = resources.Programme with { Data = resources.Programme.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    [Fact]
    public void Stage_reload_decoder_consumes_current_file37_bytes_and_preserves_its_geometry_directory()
    {
        using GameAssets game = Open();
        NativeBattleEffect69Resources resources = game.ReadNativeEffect69Resources();
        NativeBattleEffectFileAsset file = resources.ReadFile(37);
        Assert.Equal(218152, file.Data.Length);
        NativeBattleStageSource source = resources.DecodeReloadStage!(file.Data.ToArray());
        Assert.Equal(0x1A64, source.PhysicalSource!.DirectoryOffset);
        Assert.Equal(4, source.Groups.Length);
        Assert.NotEmpty(source.Geometry!.Value);
        byte[] current = file.Data.ToArray();
        current[^1] ^= 1;
        NativeBattleStageSource changed = resources.DecodeReloadStage(current);
        Assert.Equal(current[^1], changed.Texture.Data[^1]);
        Assert.NotEqual(source.Texture.Data[^1], changed.Texture.Data[^1]);
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(38));
    }

    [Fact]
    public void Selected_catalog_retains_resources_for_the_empty_RET_initializer()
    {
        using GameAssets game = Open();
        NativeBattleEffect69Resources resources = game.ReadNativeEffect69Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([69])[68];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerAsset);
        Assert.Null(binding.InitializerResource);
        Assert.Same(resources, binding.Effect69Resources);
        Assert.NotNull(binding.Effect69Resources!.DecodeReloadStage);
        Assert.Same(resources.StageFile37, binding.Effect69Resources.ReadFile(37));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect69 resources.");
        return new(directory!);
    }
}
