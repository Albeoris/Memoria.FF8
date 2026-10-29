using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRestorationAdmissionResourcesTests
{
    [Theory]
    [InlineData((ushort)24, 27, "Esuna")]
    [InlineData((ushort)27, 25, "Full-life")]
    public void Selective_original_catalog_initializes_own_TIM_entry_camera_and_persistent_bank(ushort effect, int actionId, string name)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.NativeAction is { Command: 2 } native && native.MagicId == effect);
        Assert.Equal(actionId, action.Id);
        Assert.Equal(name, action.Name);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        ImmutableArray<NativeBattleEffectBinding> catalog = game.ReadNativeEffectCatalogForEffects([effect]);
        NativeBattleEffectBinding binding = catalog[effect - 1];
        NativeBattleRestorationResources resources = Assert.IsType<NativeBattleRestorationResources>(binding.RestorationResources);
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.Equal(effect, resources.Effect);
        Assert.NotNull(binding.InitializerAsset);
        Assert.NotNull(resources.Executable);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, catalog);
        (memory, selected) = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, int header) = packets.Prepare(new(0, 2, 11, -1, checked((ushort)actionId), effect, null));
        (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        selected = selected with { Parameters = new(0, 0, 1, header, 0, 0) };
        NativeBattleRestorationFrame frame = NativeBattleRestorationController.Initialize(memory, selected, packets, resources);
        Assert.Equal(profile.Descriptors[0], frame.Effect.ReturnedDescriptor);
        Assert.Equal(profile.MainCallback, frame.Effect.Pools[0].Slots[0].Callback);
        NativeBattleEffectAnimationRegisters animation = Assert.IsType<NativeBattleEffectAnimationRegisters>(frame.Memory.Textures!.Animation);
        Assert.Equal(resources.Animation, animation.Source);
        Assert.Equal(0, animation.BaseOffset);
        Assert.Equal(checked(frame.State.SourceArenaOffset + 2 * profile.DrawBankBytes), frame.State.AuxiliaryCursor);
        NativeBattleRestorationExecutableState retained = (effect == 24 ? frame.Memory.EsunaExecutable : frame.Memory.FullLifeExecutable)!;
        NativeBattleRestorationExecutableState initialized = retained.Initialize(resources.Executable!);
        Assert.Equal(1u, initialized.InitializationFlag);
        NativeBattleRestorationExecutableState clone = NativeBattleSnapshotJson.Deserialize<NativeBattleRestorationExecutableState>(NativeBattleSnapshotJson.Serialize(initialized))!;
        Assert.Equal(initialized.Read(profile.ExecutableAddress + 4, 64), clone.Read(profile.ExecutableAddress + 4, 64));
        Assert.Same(initialized, initialized.Initialize(resources.Executable!));
    }
}
