using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffectRenderingTests
{
    [Fact]
    public void Native_flash_covers_original320x216_rectangle_and_keeps_the_lower24_lines()
    {
        BattleRenderer renderer = new(320, 240);
        byte[] pixels = renderer.Render(new BattleStage(0, [], []), [], new(new(0, 0, 10), Vector3.Zero));
        renderer.DrawNativeModuleEffects([new(NativeBattleModuleDrawKind.Flash, 255)], null, [], 1);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels.AsSpan(0, 4).ToArray());
        Assert.Equal(new byte[] { 17, 20, 29, 255 }, pixels.AsSpan(216 * 320 * 4, 4).ToArray());
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 24)]
    [InlineData(2, 0)]
    [InlineData(3, 6)]
    public void Native_sprite_uses_texture_page_blend_and_shared_diagonal_is_drawn_once(int mode, int addedRed)
    {
        BattleRenderer renderer = new(320, 240);
        byte[] pixels = renderer.Render(new BattleStage(0, [], []), [], new(new(0, 0, 10), Vector3.Zero));
        NativeBattleModuleDraw draw = Sprite(mode);
        NativeBattleEffectTextureState textures = Textures();
        renderer.DrawNativeModuleEffects([draw], textures, []);
        int expected = mode == 0 ? 17 / 2 + 24 / 2 : mode == 2 ? 0 : 17 + addedRed;
        Assert.Equal(expected, pixels[(120 * 320 + 160) * 4]);
        Assert.Equal(2, renderer.DrawnTriangles);
    }

    [Fact]
    public void Sprite_unknown_texture_memory_fails_at_its_material_read()
    {
        BattleRenderer renderer = new(320, 240);
        renderer.Render(new BattleStage(0, [], []), []);
        Assert.Throws<NotSupportedException>(() => renderer.DrawNativeModuleEffects([Sprite(1)], null, []));
    }

    [Fact]
    public void Extended_packet_does_not_override_the_following_textured_primitive_page_blend()
    {
        BattleRenderer renderer = new(320, 240);
        byte[] pixels = renderer.Render(new BattleStage(0, [], []), [], new(new(0, 0, 10), Vector3.Zero));
        NativeBattleModuleDraw draw = Sprite(2);
        renderer.DrawNativeModuleEffects([draw with { SpriteFrame = draw.SpriteFrame! with { ExtendedPacket = true } }], Textures(), []);
        Assert.Equal(0, pixels[(120 * 320 + 160) * 4]);
    }

    [Fact]
    public void Sprite_reads_clut_word6_and_texture_page_low_nine_bits_of_word14()
    {
        NativeBattleModuleDraw draw = Sprite(1);
        byte[] quad = draw.SpriteFrame!.Quads.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(6), 0x4000);
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(14), 0x20);
        byte[] words = new byte[1024 * 512 * 2];
        for (int row = 0; row < 33; row++)
        {
            for (int word = 0; word < 9; word++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(2 * (row * 1024 + word)), 0x1111);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(2 * (256 * 1024 + 1)), 0x8003);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, words.ToImmutableArray())] };
        BattleRenderer renderer = new(320, 240);
        byte[] pixels = renderer.Render(new BattleStage(0, [], []), [], new(new(0, 0, 10), Vector3.Zero));
        renderer.DrawNativeModuleEffects([draw with { SpriteFrame = draw.SpriteFrame with { Quads = quad.ToImmutableArray() } }], textures, []);
        Assert.Equal(41, pixels[(120 * 320 + 160) * 4]);
    }

    [Fact]
    public void Untextured_primitives_require_their_actual_inherited_draw_mode()
    {
        BattleRenderer renderer = new(320, 240);
        renderer.Render(new BattleStage(0, [], []), []);
        Assert.Throws<NotSupportedException>(() => renderer.DrawNativeModuleEffects([new(NativeBattleModuleDrawKind.Flash, 128)], null, []));
    }

    [Fact]
    public void Installed_defensive_meshes_render_original_gouraud_primitives_for_each_variant()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed executable is required.");
        ImmutableArray<NativeBattleDefensiveResource> resources = NativeBattleDefensiveResourceReader.Read(File.ReadAllBytes(Path.Combine(directory, "FF8_EN.exe")));
        Assert.Equal(new[] { 1536, 768, 1536 }, resources.Select(resource => resource.Bytes.Length));
        BattleRenderer renderer = new(320, 240);
        foreach (NativeBattleDefensiveResource resource in resources)
        {
            byte[] pixels = renderer.Render(new BattleStage(0, [], []), [], new(new(0, 0, 30), Vector3.Zero));
            renderer.DrawNativeModuleEffects([new(NativeBattleModuleDrawKind.Defensive, 0) { Variant = resource.Variant, Phase = 2, Scale = 4096, Anchor = new(0, 0, 0, 0) }], null, resources, 1);
            Assert.True(renderer.DrawnTriangles > 0);
            Assert.Contains(Enumerable.Range(0, pixels.Length / 4), index => pixels[index * 4] != 17 || pixels[index * 4 + 1] != 20 || pixels[index * 4 + 2] != 29);
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EFFECT_EVIDENCE") is string evidence)
            {
                Directory.CreateDirectory(evidence);
                using System.Drawing.Bitmap bitmap = BattlePreviewCanvas.BitmapFromRgba(pixels, 320, 240);
                bitmap.Save(Path.Combine(evidence, $"native-defensive-{resource.Variant}.png"));
            }
        }
    }

    private static NativeBattleModuleDraw Sprite(int mode)
    {
        byte[] bytes = new byte[20];
        bytes[0] = 32;
        bytes[1] = 128;
        bytes[2] = 32;
        bytes[3] = 0x2E;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)(0x100 | mode << 5));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), -16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), -16);
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = new(0, 0, 0, 0, new byte[8].ToImmutableArray()), SpriteFrame = new(-1, false, bytes.ToImmutableArray()), Anchor = new(0, 0, 0, 0), Scale = 4096, Direction = 4096 };
    }

    private static NativeBattleEffectTextureState Textures()
    {
        byte[] words = new byte[1024 * 512 * 2];
        for (int index = 0; index < words.Length; index += 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(index), 0x8003);
        }
        return NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, words.ToImmutableArray())] };
    }
}
