using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleReflectBeam135ResourcesTests
{
    [Fact]
    public void Original_four_ACTs_sprites_static_mesh_camera_and_selectors_close_exact_sources()
    {
        using GameAssets game = Open();
        NativeBattleReflectBeam135Resources resources = game.ReadNativeReflectBeam135Resources();
        Assert.Same(resources, game.ReadNativeReflectBeam135Resources());
        NativeBattleReflectBeam135ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new[] { 0x2878, 0x1328, 0x1B5C, 0x744 }, new[] { resources.MainAct.Data.Length, resources.TargetAct.Data.Length, resources.SecondaryAct.Data.Length, resources.ImpactAct.Data.Length });
        Assert.Equal(new[] { 0xC4, 0xF4, 0x1C0 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length });
        Assert.Equal(0x19C8, resources.StaticMesh.Bytes.Length);
        Assert.Equal(0x11C, resources.Camera.Data.Length);
        Assert.Equal(new short[] { 29, 30, 32 }, resources.BoneSelectors);
        Assert.Equal(0x641A0u, resources.Sound);
        Assert.Same(resources.MainAct, resources.Act(MainActAddress));
        Assert.Same(resources.TargetAct, resources.Act(TargetActAddress));
        Assert.Same(resources.SecondaryAct, resources.Act(SecondaryActAddress));
        Assert.Same(resources.ImpactAct, resources.Act(ImpactActAddress));
        Assert.Same(resources.SpriteA, resources.Sprite(SpriteAAddress));
        Assert.Same(resources.SpriteB, resources.Sprite(SpriteBAddress));
        Assert.Same(resources.SpriteC, resources.Sprite(SpriteCAddress));
        Assert.Throws<NotSupportedException>(() => resources.Act(1));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(1));
        NativeBattleReflectBeam135Resources restored = JsonSerializer.Deserialize<NativeBattleReflectBeam135Resources>(JsonSerializer.Serialize(resources))!;
        NativeBattleReflectBeam135ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Main_and_secondary_interpolation_require_the_original_544_byte_shared_scratch()
    {
        using GameAssets game = Open();
        NativeBattleReflectBeam135Resources resources = game.ReadNativeReflectBeam135Resources();
        int interpolated = 0;
        foreach ((NativeBattleGfAnimationSource source, int cache, int duration) in new[] { (resources.MainAct, 0x2B8, 20), (resources.SecondaryAct, 0x1F8, 13) })
        {
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[cache].ToImmutableArray()).Initialize7016B0(source, cache);
            for (int tick = 0; tick < duration; tick++)
            {
                NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
                {
                    if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                    {
                        int geometry = BitConverter.ToInt32(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4, 4));
                        int vertices = BitConverter.ToInt32(source.Data.AsSpan().Slice(geometry + 4, 4));
                        Assert.Equal(68, vertices);
                        Assert.Equal(MorphBytes, vertices * 8);
                        interpolated++;
                    }
                    return current;
                });
                workspace = next.Workspace;
            }
        }
        Assert.True(interpolated > 0);
        Assert.Equal(FirstPacketOffset, MorphOffset + MorphBytes);
        Assert.Equal(PacketBankBytes, SecondPacketOffset - FirstPacketOffset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Truncated_or_misidentified_original_sources_fail_before_runtime(int selected)
    {
        using GameAssets game = Open();
        NativeBattleReflectBeam135Resources resources = game.ReadNativeReflectBeam135Resources();
        NativeBattleReflectBeam135Resources altered = selected == 0 ? resources with { MainAct = resources.MainAct with { Data = resources.MainAct.Data.RemoveAt(resources.MainAct.Data.Length - 1) } } : selected == 1 ? resources with { SpriteB = resources.SpriteB with { Address = SpriteAAddress } } : resources with { BoneSelectors = [29, 30] };
        Assert.Throws<InvalidDataException>(() => NativeBattleReflectBeam135ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to validate Reflect Beam ingress.");
        return new(directory!);
    }
}
