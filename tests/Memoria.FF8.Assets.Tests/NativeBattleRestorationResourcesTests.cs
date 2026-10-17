using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRestorationResourcesTests
{
    [Theory]
    [InlineData((ushort)24, 27, "Esuna", 2856, 0x49442u, 0x53FCu)]
    [InlineData((ushort)27, 25, "Full-life", 2648, 0x49445u, 0x57C4u)]
    public void Installed_kernel_and_authenticated_resources_keep_distinct_original_effects_and_unfixed_directories(ushort effect, int actionId, string name, int animationBytes, uint sound, uint firstSource)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.MagicId == effect);
        Assert.Equal(actionId, action.Id);
        Assert.Equal(name, action.Name);
        NativeBattleRestorationResources resources = game.ReadNativeRestorationResources(effect);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, game.ReadNativeEffectCatalogForEffects([effect]));
        Assert.Equal(profile.Entry, selected.Binding.Entry);
        Assert.Equal(profile.Initializer, selected.Binding.Initializer);
        Assert.Equal(profile.InitializerGlobal, selected.Binding.InitializerResource!.GlobalAddress);
        Assert.Equal(effect, resources.Effect);
        Assert.Equal(animationBytes, resources.Animation.Data.Length);
        Assert.Equal(sound, resources.Sound);
        Assert.Equal(0x104, resources.OriginalExecutableDirectory.Length);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.OriginalExecutableDirectory.AsSpan()));
        Assert.Equal(firstSource, BinaryPrimitives.ReadUInt32LittleEndian(resources.OriginalExecutableDirectory.AsSpan().Slice(4)));
        Assert.NotEqual(NativeBattleCuragaExecutableState.Address, profile.ExecutableAddress);
        Assert.Same(resources, game.ReadNativeRestorationResources(effect));
    }

    [Theory]
    [InlineData((ushort)24, 0, 0x5C0C)]
    [InlineData((ushort)27, 12, 0x67E4)]
    public void Original_sparse_banks_fix_only_once_and_prove_the_reached_animated_anchor_source(ushort effect, int initialAnimatedSource, int expectedExtent)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRestorationResources resources = game.ReadNativeRestorationResources(effect);
        NativeBattleRestorationExecutableSource source = resources.Executable!;
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleRestorationExecutableState retained = effect == 24 ? cold.EsunaExecutable! : cold.FullLifeExecutable!;
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(effect), retained);
        NativeBattleRestorationExecutableState initialized = retained.Initialize(source);
        Assert.Equal(expectedExtent, source.Data.Length);
        int maximumNodeEnd = 0;
        for (int index = 0; index < 16; index++)
        {
            uint relative = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(4 + 4 * index));
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(profile.ExecutableAddress + 4u + 4u * (uint)index, 4));
            Assert.Equal(relative == 0 ? 0 : profile.ExecutableAddress + relative, pointer);
            if (relative == 0)
            {
                continue;
            }
            maximumNodeEnd = Math.Max(maximumNodeEnd, checked((int)relative + 0x158));
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint original = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(checked((int)relative + field)));
                Assert.Equal(unchecked(original + profile.ExecutableAddress), BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(pointer + (uint)field, 4)));
            }
        }
        Assert.Equal(expectedExtent, maximumNodeEnd);
        uint animated = BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(profile.ExecutableAddress + 4u + 4u * (uint)initialAnimatedSource, 4));
        Assert.Equal((byte)4, initialized.Read(animated + 0x11, 1)[0]);
        Assert.Equal((byte)0, initialized.Read(animated + 0x13, 1)[0]);
        Assert.Same(initialized, initialized.Initialize(source));
    }
}
