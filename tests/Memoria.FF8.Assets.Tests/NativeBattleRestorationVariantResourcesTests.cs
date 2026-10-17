using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRestorationVariantResourcesTests
{
    [Theory]
    [InlineData((ushort)57, 0x7FD150u, 0x7FD130u, 0x260D5F0u, "mag056.tim", 0x6534)]
    [InlineData((ushort)67, 0x7DEA20u, 0x7DEA00u, 0x26005D0u, "mag023-066.tim", 0x5C0C)]
    public void Authenticated_original_variants_keep_their_own_initializer_animation_directory_and_cold_flag(ushort effect, uint entry, uint initializer, uint initializerGlobal, string file, int extent)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([effect])[effect - 1];
        NativeBattleRestorationResources resources = Assert.IsType<NativeBattleRestorationResources>(binding.RestorationResources);
        Assert.Equal(entry, binding.Entry);
        Assert.Equal(initializer, binding.Initializer);
        Assert.Equal(initializerGlobal, binding.InitializerResource!.GlobalAddress);
        Assert.Equal(file.ToUpperInvariant(), binding.InitializerAsset!.Name.ToUpperInvariant());
        Assert.Same(resources, game.ReadNativeRestorationResources(effect));
        Assert.Equal(0xB28, resources.Animation.Data.Length);
        Assert.Equal(300098u, resources.Sound);
        NativeBattleRestorationExecutableSource source = Assert.IsType<NativeBattleRestorationExecutableSource>(resources.Executable);
        Assert.Equal(extent, source.Data.Length);
        Assert.Equal(source.Data.AsSpan().Slice(0, 0x104).ToArray(), resources.OriginalExecutableDirectory.ToArray());
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleRestorationExecutableState retained = Assert.IsType<NativeBattleRestorationExecutableState>(cold.GetRestorationExecutable(effect));
        Assert.Equal(new NativeBattleRestorationExecutableState(effect, 0, null), retained);
        NativeBattleRestorationExecutableState initialized = retained.Initialize(source);
        Assert.Equal(1u, initialized.InitializationFlag);
        Assert.Same(initialized, initialized.Initialize(source));
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        for (int index = 0; index < 16; index++)
        {
            uint original = BinaryPrimitives.ReadUInt32LittleEndian(resources.OriginalExecutableDirectory.AsSpan().Slice(4 + 4 * index));
            Assert.Equal(original == 0 ? 0u : profile.ExecutableAddress + original, BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(profile.ExecutableAddress + 4u + 4u * (uint)index, 4)));
        }
        NativeBattleRestorationExecutableState restored = JsonSerializer.Deserialize<NativeBattleRestorationExecutableState>(JsonSerializer.Serialize(initialized))!;
        Assert.Equal(initialized.Data!.Value.ToArray(), restored.Data!.Value.ToArray());
        Assert.Throws<InvalidDataException>(() => retained.Initialize(game.ReadNativeRestorationResources(24).Executable!));
    }

    [Theory]
    [InlineData((ushort)57, (byte)0)]
    [InlineData((ushort)57, (byte)1)]
    [InlineData((ushort)67, (byte)0)]
    [InlineData((ushort)67, (byte)1)]
    public void Actual_TIM_entry_obeys_the_original_skip_bit_without_aliasing_another_executable(ushort effect, byte flags)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        ImmutableArray<NativeBattleEffectBinding> catalog = game.ReadNativeEffectCatalogForEffects([effect]);
        NativeBattleRestorationResources resources = catalog[effect - 1].RestorationResources!;
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, catalog);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        (memory, selected) = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        // Controlled postformula component: effect57 has no named kernel action; this is an explicit Esuna header shell.
        (packets, int header) = packets.Prepare(new(0, effect == 67 ? (byte)4 : (byte)2, 11, -1, 27, effect, null));
        (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        selected = selected with { Parameters = new(0, flags, 1, header, 0, 0) };
        NativeBattleRestorationFrame frame = NativeBattleRestorationController.Initialize(memory, selected, packets, resources);
        Assert.Equal(NativeBattleRestorationProfile.ForEffect(effect).MainCallback, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0u, frame.Memory.GetRestorationExecutable(effect)!.InitializationFlag);
        Assert.Equal(0u, frame.Memory.EsunaExecutable!.InitializationFlag);
        if (flags == 0)
        {
            NativeBattleEffectTextureState textures = Assert.IsType<NativeBattleEffectTextureState>(frame.Memory.Textures);
            Assert.Equal(resources.Animation, textures.Animation!.Source);
            Assert.True(textures.Count > 0);
            NativeBattleEffectTextureDrain uploaded = textures.Drain505D20(frame.Memory.Arena);
            Assert.Equal(0, uploaded.State.Count);
            Assert.NotEmpty(uploaded.Uploads);
        }
        else
        {
            Assert.Equal(JsonSerializer.Serialize(memory.Textures), JsonSerializer.Serialize(frame.Memory.Textures));
        }
        NativeBattleRestorationFrame clone = JsonSerializer.Deserialize<NativeBattleRestorationFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(clone));
    }

    [Fact]
    public void Remedy_equal_original_bytes_still_materialize_distinct_mutable_addresses()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRestorationResources esuna = game.ReadNativeRestorationResources(24);
        NativeBattleRestorationResources remedy = game.ReadNativeRestorationResources(67);
        Assert.Equal(esuna.Animation.Data.ToArray(), remedy.Animation.Data.ToArray());
        NativeBattleRestorationExecutableSource esunaSource = Assert.IsType<NativeBattleRestorationExecutableSource>(esuna.Executable);
        NativeBattleRestorationExecutableSource remedySource = Assert.IsType<NativeBattleRestorationExecutableSource>(remedy.Executable);
        Assert.Equal(esunaSource.Data.ToArray(), remedySource.Data.ToArray());
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleEffectMemory initialized = cold.WithRestorationExecutable(cold.EsunaExecutable!.Initialize(esunaSource));
        Assert.Equal(1u, initialized.EsunaExecutable!.InitializationFlag);
        NativeBattleRestorationExecutableState unchanged = Assert.IsType<NativeBattleRestorationExecutableState>(initialized.RemedyExecutable);
        Assert.Equal(0u, unchanged.InitializationFlag);
        NativeBattleRestorationExecutableState own = unchanged.Initialize(remedySource);
        Assert.NotEqual(initialized.EsunaExecutable!.Read(NativeBattleRestorationProfile.Esuna.ExecutableAddress + 4, 4), own.Read(NativeBattleRestorationProfile.Remedy.ExecutableAddress + 4, 4));
    }

    [Fact]
    public void Effect57_extra_frame_zero_source6_uses_twenty_original_FT4_faces_and_survives_JSON()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRestorationResources resources = game.ReadNativeRestorationResources(57);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.Effect57;
        NativeBattleRestorationExecutableState bank = game.ReadColdProcessNativeBattleMemory().EffectMemory!.Effect57Executable!.Initialize(resources.Executable!);
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(profile.ExecutableAddress + 4 + 6 * 4, 4));
        Assert.NotEqual(0u, source);
        Assert.Equal((byte)0, bank.Read(source + 0x13, 1)[0]);
        ImmutableArray<byte> mesh = NativeBattleRestorationMeshAssetsTests.ReadMesh(bank, 6);
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(4)));
        int groups = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan());
        Assert.Equal(new byte[12], mesh.AsSpan().Slice(groups, 12).ToArray());
        Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(groups + 12)));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleEsunaMeshContext context = NativeBattleRestorationMeshPackets.Prepare88E090(0, 6, 0, 0, null).Context;
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, context);
        Assert.Equal(20, batch.Attempts.Length);
        Assert.Equal(20 * 40, batch.AllocationBytes);
        Assert.All(batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        NativeBattleRestorationExecutableState restored = JsonSerializer.Deserialize<NativeBattleRestorationExecutableState>(JsonSerializer.Serialize(bank))!;
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleRestorationMeshPackets.EmitBatch88E240(NativeBattleRestorationMeshAssetsTests.ReadMesh(restored, 6), matrix, projection, context)));
    }
}
