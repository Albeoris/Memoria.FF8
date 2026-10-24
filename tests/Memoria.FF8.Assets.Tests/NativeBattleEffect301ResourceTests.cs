using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect301ResourceTests
{
    [Fact]
    public void Original_programme_all_frames_have_one_shape_no_true_morph_and_two_nodes()
    {
        using GameAssets game = Open();
        NativeBattleEffect301Resources resources = game.ReadNativeEffect301Resources();
        resources.Validate();
        NativeBattleGfAnimationSource source = resources.Act(0x103D46C);
        NativeBattleGfAnimationWorkspace cache = new(new byte[0x90].ToImmutableArray());
        cache = cache.Initialize7016B0(source, 0x90);
        int outputs = 0;
        for (int frame = 0; frame < 122; frame++)
        {
            NativeBattleGfAnimationAdvance step = cache.Advance701970(source, 0, (current, node) =>
            {
                Assert.Equal(0u, node.Header);
                Assert.Equal(node.Word40, node.Word42);
                outputs++;
                return current;
            });
            cache = step.Workspace;
        }
        Assert.Equal(244, outputs);
        Assert.Equal(144, resources.WorkspaceLength(0x103D46C));
        NativeBattleStaticMeshResource geometry = resources.ReadGeometry(0x103D478);
        Assert.Equal(36u, BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan(4, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan(8, 4)));
    }

    [Fact]
    public void Selected_normal_and_custom_sprite_sources_keep_distinct_directory_contracts()
    {
        using GameAssets game = Open();
        NativeBattleEffect301Resources resources = game.ReadNativeEffect301Resources();
        NativeBattleModuleResource normal = resources.Sprite(0x103D118);
        Assert.Equal(508, normal.Bytes.Length);
        for (int frame = 0; frame < 16; frame++)
        {
            _ = normal.ProjectedFrame571C80(frame);
        }
        NativeBattleModuleResource custom = resources.ReadProjectedQuads(0x103CFB8);
        Assert.Equal(352, custom.Bytes.Length);
        Assert.Equal((ushort)13, BinaryPrimitives.ReadUInt16LittleEndian(custom.Bytes.AsSpan(8, 2)));
        for (int life = 12; life > 0; life--)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(custom.Bytes.AsSpan(8 + (13 - life) * 2, 2));
            int count = BinaryPrimitives.ReadInt32LittleEndian(custom.Bytes.AsSpan(offset, 4));
            Assert.InRange(count, 0, (custom.Bytes.Length - offset - 4) / 20);
        }
        Assert.Throws<NotSupportedException>(() => resources.Sprite(0x103CFB8));
        Assert.Throws<NotSupportedException>(() => resources.ReadProjectedQuads(0x103D118));
        Assert.Equal(344, resources.ReadMesh(0x103D314).Bytes.Length);
    }

    [Fact]
    public void Actual_script_cold_words_and_bounded_exhausted_words_are_addressed_without_cursor_fallback()
    {
        using GameAssets game = Open();
        NativeBattleEffect301Resources resources = game.ReadNativeEffect301Resources();
        NativeBattleEffect301State state = NativeBattleEffect301State.FromColdProcess().BindSources(resources);
        uint cursor = state.ReadDword(NativeBattleEffect301State.ScriptPointerAddress);
        Assert.Equal(0x103BD00u, cursor);
        int first = 0;
        int second = 0;
        for (int age = 1; age <= 101; age++)
        {
            int count = resources.ReadScriptWord(cursor);
            Assert.InRange(count, 0, 32);
            first += count;
            cursor += checked((uint)(2 + count * 20));
            count = resources.ReadScriptWord(cursor);
            Assert.InRange(count, 0, 32);
            second += count;
            cursor += checked((uint)(2 + count * 12));
        }
        Assert.Equal(115, first);
        Assert.Equal(174, second);
        Assert.Equal(0x103CFB8u, cursor);
        Assert.True(resources.ReadScriptWord(cursor) > 32);
        Assert.Equal(2, resources.ReadStatic(0x103D23A, 2).Length);
        byte[] joined = resources.ReadStatic(0x103D116, 4);
        Assert.Equal(resources.ReadProjectedQuads(0x103CFB8).Bytes.AsSpan().Slice(350, 2).ToArray().Concat(resources.Sprite(0x103D118).Bytes.AsSpan().Slice(0, 2).ToArray()), joined);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(NativeBattleEffect301State.ScriptPointerAddress, 4));
    }

    [Fact]
    public void JSON_preserves_original_camera_vibration_and_catalog_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect301Resources resources = game.ReadNativeEffect301Resources();
        NativeBattleEffect301Resources restored = JsonSerializer.Deserialize<NativeBattleEffect301Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(132128, restored.MainTim.Data.Length);
        Assert.Equal(294, restored.Vibration.Data.Length);
        Assert.Equal(316, restored.Camera.Data.Length);
        Assert.Equal(resources.OriginalScriptCursor4.ToArray(), restored.OriginalScriptCursor4.ToArray());
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([301])[300];
        Assert.Same(resources, binding.Effect301Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag300.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect301 resources.");
        return new(directory!);
    }
}
