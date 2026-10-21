using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRemainingCinematicResourcesTests
{
    [Theory]
    [InlineData(75, 4, false, 51924, "4B288AFFFCEB90B6BFEAFB2C43D7C765D1219BE622697C245158FCC8260C0F6E", 101968, "7FD26A5FBF9E31EA9338F29AEA7DD3643B7E55ED4FE519C5AA4593E9C02DB1F2")]
    [InlineData(77, 9, true, 73056, "56F9112EC476BA61646046C7A266D298A34031FFD4B7107F58CFBBAAB892C2E1", 193920, "DF7AFBDC4DA624CD0E1DF64AA9B56E2F90CE695417D4C24E846BA97C94B424D9")]
    [InlineData(208, 10, true, 41464, "208E05775739E1A62DB08F5209D4452BC73DDEAC6704B3014C52F8CBC19E19E7", 181888, "F65D635BF84A766FEE46D578FB9E991C429C354B0398DD42BD48D0132D9CAF95")]
    [InlineData(211, 9, true, 41732, "E938117EA2E1EC54D24935F657BAA2D79A5DA8225D99EFC2D66F824620BBFA31", 169080, "2F85551541A284CAC3E99969C3B153732F745822CC3B528F2322B532ED1E1B56")]
    [InlineData(219, 15, false, 92072, "24EE6D9054FF2F2EB15787F1469897A38E96A0CCE5F3F284CDC57137D33639A8", 189096, "597E59CF6FBC729F902CC8F6D8B0BDE2B325FA53880F7F8635C3DAC35439E1A1")]
    [InlineData(220, 5, false, 29620, "6CEACE4AC38F0EC9FFE5476A7182806266523D5C635C9978A1A30E023A733949", 129624, "BD0DFF23507A5231FDDA21E6D95D395B05968998CBF8168A87CF8E1F4739FA27")]
    public void Remaining_profiles_read_original_initial_files_tables_and_bounded_stream_catalog(int effect, int textureCount, bool vibration, int firstLength, string firstHash, int secondLength, string secondHash)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory original = new(executable);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        Assert.True(NativeBattleCinematicProfile.IsSupported(effect));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(profile.InitializationAddress));
        Assert.Equal(textureCount, profile.TextureCount);
        Assert.False(profile.Lit);
        Assert.Equal(vibration, profile.HasVibrationDirectory);
        Assert.Equal(profile.OwnerDirectoryAddress + 0x28u, profile.BoneTable);
        Assert.Equal(18, profile.BoneCount);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([checked((ushort)effect)])[effect - 1];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        NativeBattleEffectInitializerAsset[] initial = binding.InitializerAssets!.Value.ToArray();
        Assert.Equal(2, initial.Length);
        Assert.Equal(profile.InitialFile, initial[0].Name);
        Assert.Equal(profile.InitialFile[..^1] + "1", initial[1].Name);
        Assert.Equal(firstLength, initial[0].Data.Length);
        Assert.Equal(firstHash, Convert.ToHexString(SHA256.HashData(initial[0].Data.AsSpan())));
        Assert.Equal(secondLength, initial[1].Data.Length);
        Assert.Equal(secondHash, Convert.ToHexString(SHA256.HashData(initial[1].Data.AsSpan())));
        Assert.Equal(profile.File0Publication, binding.InitializerResources!.Value[0].GlobalAddress);
        Assert.Equal(profile.File1Publication, binding.InitializerResources!.Value[1].GlobalAddress);
        Assert.Equal(vibration, BinaryPrimitives.ReadUInt32LittleEndian(initial[0].Data.AsSpan().Slice(0x24)) != 0);

        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(effect);
        resources.Validate();
        Assert.Same(resources, assets.ReadNativeCinematicResources(effect));
        Assert.Equal(profile.InitializationAddress, resources.StaticAddress);
        Assert.Equal(profile.StaticLength, resources.StaticData.Data.Length);
        Assert.Equal(vibration, resources.Vibration is not null);
        NativeBattleScriptedInitializationDirectories directories = assets.ReadNativeCinematicInitializationDirectories(effect);
        Assert.Equal(original.Read(profile.InitializationAddress, 0x1C), directories.InitializationDirectory.Data.ToArray());
        Assert.Equal(original.Read(profile.OwnerDirectoryAddress, 0x14), directories.OwnerDirectory.Data.ToArray());
        foreach ((uint address, int length) in new[]
        {
            (profile.BoneTable, profile.BoneCount * 4),
            (profile.PrimitiveTable, 60 * 4),
            (profile.PrimitiveSizes, 20),
            (profile.SpriteTable, 11 * 4),
            (profile.PlacementTable, 5 * 4),
            (profile.ParticleInstructionTable, 24 * 4),
            (profile.DrawTable, 73 * 4),
            (profile.InstructionTable, 0x147 * 4)
        })
        {
            Assert.Equal(original.Read(address, length), resources.ReadStatic(address, length).ToArray());
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(profile.InitializationAddress - 1, 1).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(profile.InitializationAddress + (uint)profile.StaticLength, 1).ToArray());
        INativeBattleScriptedTextureProfile textures = resources;
        for (int texture = 0; texture < textureCount; texture++)
        {
            _ = textures.Rectangle(texture, false);
            _ = textures.Rectangle(texture, true);
            _ = textures.Source(texture, false);
            _ = textures.Source(texture, true);
        }
        int[] expectedFiles = Enumerable.Range(profile.SourceBase, profile.LastSource - profile.SourceBase + 1).Concat(Enumerable.Range(0x16B, 3)).Distinct().Order().ToArray();
        Assert.Equal(expectedFiles, resources.Files.Select(file => (int)file.FileId).Order().ToArray());
        Assert.All(resources.Files, file => Assert.False(file.Bytes.IsDefaultOrEmpty));
        if (effect == 77)
        {
            Assert.Equal("mag076_b.02", Assert.Single(resources.Files, file => file.FileId == 0x18B).Name);
        }
        if (effect == 219)
        {
            for (int part = 2; part <= 4; part++)
            {
                Assert.Equal($"mag218_b.{part:D2}", Assert.Single(resources.Files, file => file.FileId == 0x2C4 + part).Name);
            }
        }
    }
}
