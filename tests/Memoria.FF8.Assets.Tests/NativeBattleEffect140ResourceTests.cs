using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect140ResourceTests
{
    [Fact]
    public void Selected_model_programmes_sprites_and_installed_files_are_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect140Resources resources = game.ReadNativeEffect140Resources();
        resources.Validate();
        Assert.Equal(ModelLength, resources.Model.Bytes.Length);
        Assert.Equal(59, resources.Model.PoseSource.BoneCount);
        Assert.Single(resources.Model.Geometry);
        for (int index = 0; index < ProgrammeAddresses.Length; index++)
        {
            Assert.Equal(ProgrammeLengths[index], resources.Act(ProgrammeAddresses[index]).Data.Length);
            Assert.Equal(ProgrammeWorkspaceLengths[index], resources.WorkspaceLength(ProgrammeAddresses[index]));
        }
        Assert.Equal(SpriteLength, resources.Sprite(SpriteAddress).Bytes.Length);
        Assert.True(resources.Sprite(0x11D0164).Bytes.Length > 12);
        for (int id = 528; id <= 536; id++)
        {
            Assert.Equal(id, resources.ReadFile(id).FileId);
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(527));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ModelSkeletonAddress, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MutableMeshAddresses[0] + 8, 8));
    }

    [Fact]
    public void Mutable_sources_bind_once_and_retained_W_survives_JSON_without_refill()
    {
        using GameAssets game = Open();
        NativeBattleEffect140Resources resources = game.ReadNativeEffect140Resources();
        (NativeBattleEffect140State state, NativeBattleFileStaging staging) = NativeBattleEffect140State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        uint w = MutableMeshAddresses[0] + 14;
        staging = staging.Write(w, BitConverter.GetBytes((ushort)0xCAFE));
        state = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect140State>(NativeBattleSnapshotJson.Serialize(state))!;
        staging = NativeBattleSnapshotJson.Deserialize<NativeBattleFileStaging>(NativeBattleSnapshotJson.Serialize(staging))!;
        (state, staging) = state.BindSources(staging, resources);
        Assert.True(state.StaticSourcesBound);
        Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), staging.Read(w, 2));
        Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), resources.ReadCurrentMesh(staging, MutableMeshAddresses[0]).Bytes.AsSpan(14, 2).ToArray());
        NativeBattleEffect140Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect140Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.Sprite(0x11D0164).Bytes.ToArray(), restored.Sprite(0x11D0164).Bytes.ToArray());
    }

    [Fact]
    public void Selected_catalog_hydrates_the_same_resource_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect140Resources resources = game.ReadNativeEffect140Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([140])[139];
        Assert.Same(resources, binding.Effect140Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag139.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect140 resources.");
        return new(directory!);
    }
}
