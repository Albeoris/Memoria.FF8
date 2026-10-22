using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMicroMissiles299ResourcesTests
{
    [Fact]
    public void Original_ACT_sprite_camera_and_caster_directory_have_complete_bounded_payloads()
    {
        using GameAssets game = Open();
        NativeBattleMicroMissiles299Resources resources = game.ReadNativeMicroMissiles299Resources();
        Assert.Same(resources, game.ReadNativeMicroMissiles299Resources());
        NativeBattleMicroMissiles299ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new[] { 0x2594, 0x12D4, 0x1658 }, new[] { resources.Missile.Data.Length, resources.Impact.Data.Length, resources.WideImpact.Data.Length });
        Assert.Equal(new[] { 0x144, 0x144, 0x18C }, new[] { resources.ParticleSprite.Bytes.Length, resources.TrailSprite.Bytes.Length, resources.ImpactSprite.Bytes.Length });
        Assert.Equal(916, resources.Camera.Data.Length);
        Assert.Equal(0x55787u, resources.Sound);
        NativeBattleMicroMissiles299Resources restored = JsonSerializer.Deserialize<NativeBattleMicroMissiles299Resources>(JsonSerializer.Serialize(resources))!;
        NativeBattleMicroMissiles299ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Original_selected_programmes_fit_their_real_workspaces_and_morph_bank()
    {
        using GameAssets game = Open();
        NativeBattleMicroMissiles299Resources resources = game.ReadNativeMicroMissiles299Resources();
        foreach ((NativeBattleGfAnimationSource source, int size, int duration) in new[] { (resources.Missile, 0x7D8, 45), (resources.Impact, 0xA0, 12), (resources.WideImpact, 0x114, 16) })
        {
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[size].ToImmutableArray()).Initialize7016B0(source, size);
            int maximum = 0;
            for (int frame = 0; frame < duration; frame++)
            {
                NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
                {
                    int geometry = Int(source.Data, 8 + node.GeometryIndex * 4);
                    maximum = Math.Max(maximum, Int(source.Data, geometry + 4));
                    return current;
                });
                Assert.Equal(duration - frame - 1, advanced.Remaining);
                workspace = advanced.Workspace;
            }
            Assert.InRange(maximum, 1, 64);
        }
        Assert.Equal(512, NativeBattleMicroMissiles299Profile.MorphBytes);
        Assert.Equal(NativeBattleMicroMissiles299Profile.FirstPacketOffset, NativeBattleMicroMissiles299Profile.MorphOffset + NativeBattleMicroMissiles299Profile.MorphBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Truncated_ACT_payloads_and_invalid_vertex_directories_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleMicroMissiles299Resources resources = game.ReadNativeMicroMissiles299Resources();
        NativeBattleGfAnimationSource source = selected == 0 ? resources.Missile : selected == 1 ? resources.Impact : resources.WideImpact;
        NativeBattleGfAnimationSource truncated = source with { Data = source.Data.RemoveAt(source.Data.Length - 1) };
        NativeBattleMicroMissiles299Resources altered = selected == 0 ? resources with { Missile = truncated } : selected == 1 ? resources with { Impact = truncated } : resources with { WideImpact = truncated };
        Assert.Throws<InvalidDataException>(() => NativeBattleMicroMissiles299ResourcesReader.ValidatePayloads(altered));
        byte[] bytes = source.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Int(source.Data, 8) + 4), 65);
        NativeBattleGfAnimationSource invalid = source with { Data = bytes.ToImmutableArray() };
        altered = selected == 0 ? resources with { Missile = invalid } : selected == 1 ? resources with { Impact = invalid } : resources with { WideImpact = invalid };
        Assert.Throws<InvalidDataException>(() => NativeBattleMicroMissiles299ResourcesReader.ValidatePayloads(altered));
    }

    private static int Int(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset, 4));
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Micro Missiles ingress.");
        return new(directory!);
    }
}
