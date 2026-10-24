using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect65ResourceTests
{
    [Fact]
    public void Original_fronts_exclude_current_literal_fields_and_include_exact_scale_and_files()
    {
        using GameAssets game = Open();
        NativeBattleEffect65Resources resources = game.ReadNativeEffect65Resources();
        resources.Validate();
        Assert.Equal(new int[] { 2048, 2048, 2048 }.SelectMany(BitConverter.GetBytes).ToArray(), resources.ReadStatic(ScaleAddress, 12).ToArray());
        Assert.Equal(new short[] { 360, 361, 362, 391, 392, 393, 394 }, resources.Files.Keys.Order().ToArray());
        Assert.Equal((short)37, BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(0x133ABB6, 2).AsSpan()));
        Assert.Equal((short)38, BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(0x133ABBE, 2).AsSpan()));
        foreach ((uint address, int length) in LiteralRanges)
        {
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, length));
        }
        Assert.Equal(12, resources.ReadStatic(0x133AC90, 12).Length);
        Assert.Equal(14, resources.ReadStatic(0x133AC9E, 14).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x133AC90, 36));
    }

    [Fact]
    public void Every_loaded_ACT_frame_uses_bounded_shape_words_and_actual_shared_geometry()
    {
        using GameAssets game = Open();
        NativeBattleEffect65Resources resources = game.ReadNativeEffect65Resources();
        int emissions = 0;
        int maximumMorph = 0;
        foreach ((short file, ImmutableArray<int> offsets) in BundleProgrammeOffsets)
        {
            ImmutableArray<byte> data = resources.ReadFile(file).Data;
            int[] caches = file == 393 ? [940, 428, 640, 640, 640, 2500] : [288, 288, 288, 2140, 120, 416, 200];
            int[] frames = file == 393 ? [13, 12, 30, 26, 22, 22] : [26, 26, 26, 26, 14, 24, 200];
            for (int index = 0; index < offsets.Length; index++)
            {
                if (caches[index] == 0)
                {
                    continue;
                }
                NativeBattleGfAnimationSource source = new(checked(ArenaAddress + FileOffset + (uint)offsets[index]), data.AsSpan().Slice(offsets[index]).ToArray().ToImmutableArray());
                NativeBattleGfAnimationWorkspace workspace = new(new byte[caches[index]].ToImmutableArray());
                workspace = workspace.Initialize7016B0(source, caches[index]);
                for (int frame = 0; frame < frames[index]; frame++)
                {
                    NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
                    {
                        uint address = source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan(8 + node.GeometryIndex * 4, 4));
                        NativeBattleStaticMeshResource mesh = NativeBattleEffect65Resources.ReadLoadedGeometry(source, address);
                        int shapes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan(8, 4)));
                        Assert.InRange(node.Word40, (short)0, checked((short)(shapes - 1)));
                        Assert.InRange(node.Word42, (short)0, checked((short)(shapes - 1)));
                        if (node.Word40 != node.Word42)
                        {
                            maximumMorph = Math.Max(maximumMorph, checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan(4, 4)) * 8));
                        }
                        emissions++;
                        return current;
                    });
                    workspace = next.Workspace;
                }
            }
        }
        Assert.Equal(4134, emissions);
        Assert.Equal(2448, maximumMorph);
    }

    [Fact]
    public void Selected_catalog_and_json_keep_original_inputs_separate_from_current_COMMON()
    {
        using GameAssets game = Open();
        NativeBattleEffect65Resources resources = game.ReadNativeEffect65Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([65])[64];
        Assert.Same(resources, binding.Effect65Resources);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag064.tim", InitializerGlobal), binding.InitializerResource);
        NativeBattleEffect65Resources restored = JsonSerializer.Deserialize<NativeBattleEffect65Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(ArenaAddress + 0x39E00, 8));
        int offset = 0x39E00 - FileOffset;
        Assert.Equal(104u, BinaryPrimitives.ReadUInt32LittleEndian(restored.ReadFile(394).Data.AsSpan(offset, 4)));
        Assert.NotEqual(104u, BinaryPrimitives.ReadUInt32LittleEndian(restored.ReadFile(393).Data.AsSpan(offset, 4)));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect65 resources.");
        return new(directory!);
    }
}
