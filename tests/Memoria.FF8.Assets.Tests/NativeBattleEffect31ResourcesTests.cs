using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect31ResourcesTests
{
    [Fact]
    public void Immutable_reads_preserve_control_holes_and_require_current_stream_owners()
    {
        uint control = ControlAddresses[0];
        ImmutableDictionary<uint, ImmutableArray<byte>> fronts = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(control, new byte[0x148].ToImmutableArray()).Add(control + 0x148, new byte[6].ToImmutableArray()).Add(control + 0x150, new byte[6].ToImmutableArray());
        NativeBattleEffect31Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, fronts);
        Assert.Equal(6, resources.ReadStatic(control + 0x148, 6).Length);
        Assert.Equal(6, resources.ReadStatic(control + 0x150, 6).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(control, 0x158));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(control + 0x14E, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(control + 0x156, 2));
        foreach ((uint address, int length) in MutableStreamRanges)
        {
            NativeBattleEffect31Resources fake = resources with { StaticFronts = fronts.SetItem(address, new byte[length].ToImmutableArray()) };
            Assert.Throws<NotSupportedException>(() => fake.ReadStatic(address, length));
        }
    }

    [Fact]
    public void Installed_resources_retain_complete_shapes_successors_programmes_and_cache()
    {
        using GameAssets game = Open();
        NativeBattleEffect31Resources resources = game.ReadNativeEffect31Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect31Resources());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([Effect])[Effect - 1];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource(InitializerTextureName, InitializerGlobal), binding.InitializerResource);
        Assert.Same(resources, binding.Effect31Resources);
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(10u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Mesh(MeshAddresses[0]).Bytes.AsSpan(8, 4)));
        Assert.Equal(0x61B68u, resources.Sound);
        Assert.Equal(ProgrammeLengths.ToArray(), ProgrammeAddresses.Select(address => resources.Programme(address).Data.Length).ToArray());
        for (int selected = 0; selected < SpriteAddresses.Length; selected++)
        {
            NativeBattleModuleResource sprite = resources.Sprite(SpriteAddresses[selected]);
            Assert.Equal(SpriteLengths[selected], sprite.Bytes.Length);
            Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(10 + SpriteFrameCounts[selected] * 2, 2)));
        }
        NativeBattleEffect31Resources restored = JsonSerializer.Deserialize<NativeBattleEffect31Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { MainTim = restored.MainTim with { Data = restored.MainTim.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { StaticFronts = restored.StaticFronts.Remove(ControlAddresses[0] + 0x150) }).Validate());
        NativeBattleStaticMeshResource mesh = restored.Mesh(MeshAddresses[0]);
        byte[] bytes = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 2);
        Assert.Throws<InvalidDataException>(() => (restored with { Meshes = restored.Meshes.SetItem(mesh.Address, mesh with { Bytes = bytes.ToImmutableArray() }) }).Validate());
        NativeBattleModuleResource firstSprite = restored.Sprite(SpriteAddresses[0]);
        byte[] spriteBytes = firstSprite.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(spriteBytes.AsSpan(10 + SpriteFrameCounts[0] * 2, 2), 0);
        Assert.Throws<InvalidDataException>(() => (restored with { Sprites = restored.Sprites.SetItem(firstSprite.Address, firstSprite with { Bytes = spriteBytes.ToImmutableArray() }) }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect31 resources.");
        return new(directory!);
    }
}
