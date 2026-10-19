using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCarbuncle278ResourcesTests
{
    [Fact]
    public void Installed_resources_supply_complete_original_mirror_model_and_signed_bounds()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleCarbuncle278Resources resources = game.ReadNativeCarbuncle278Resources();
        Assert.Same(resources, game.ReadNativeCarbuncle278Resources());
        resources.Validate();
        Assert.Equal(0x10C0D08u, resources.MirrorBoundsMesh.Address);
        Assert.Equal(115u, BinaryPrimitives.ReadUInt32LittleEndian(resources.MirrorBoundsMesh.Bytes.AsSpan().Slice(4)));
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-1606, 0, -400), new(1606, 0, 3200)), NativeBattleStaticMeshBounds.Read6D6070(resources.MirrorBoundsMesh.Bytes.AsSpan()));
        Assert.Throws<InvalidDataException>(() => (resources with { MirrorBoundsMesh = resources.MirrorBoundsMesh with { Address = 0 } }).Validate());
    }
}
