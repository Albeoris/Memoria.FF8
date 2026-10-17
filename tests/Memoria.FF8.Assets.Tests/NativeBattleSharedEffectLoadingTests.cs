using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSharedEffectLoadingTests
{
    [Fact]
    public void Cold_effect_scalar_coverage_rejects_undeclared_unknowns()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectMemory memory = assets.ReadColdProcessNativeBattleMemory().EffectMemory!;
        foreach (var property in typeof(NativeBattleEffectMemory).GetProperties())
        {
            Type? scalar = Nullable.GetUnderlyingType(property.PropertyType);
            if (scalar is null || !scalar.IsPrimitive)
            {
                continue;
            }
            object? value = property.GetValue(memory);
            Assert.True(value is not null, $"{property.Name} has no authenticated cold scalar value or explicit entry ownership.");
            Assert.Equal(0L, Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Shiva_uses_the_shared_initializer_and_preserves_current_process_parameters()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        var catalog = assets.ReadNativeEffectCatalogForEffects([117]);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(117, catalog);
        Assert.Equal(0x6C16A0u, selected.Binding.Entry);
        Assert.Equal(0x6C16B0u, selected.Binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag116.tim", 0x2521650), selected.Binding.InitializerResource);
        NativeBattleEffectMemory memory = assets.ReadColdProcessNativeBattleMemory().EffectMemory!.SetDirectParameters(0x50BDC0, 0xF1, 65530, 7);
        Assert.Equal((byte?)8, memory.Parameter80);
        Assert.Equal((byte?)2, memory.Parameter82);
        var initialized = NativeBattleOriginalEffects.Initialize(memory, selected);
        Assert.Equal((byte?)8, initialized.Memory.Parameter80);
        Assert.Equal((byte?)2, initialized.Memory.Parameter82);
        Assert.Equal(selected.Binding.InitializerAsset!.Data.ToArray(), initialized.Memory.Arena!.Read(0, initialized.Memory.Cursor));
        Assert.Equal(new NativeBattleEffectResourcePointer(0x2521650, 0), Assert.Single(initialized.Effect.ResourcePointers));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(initialized.Memory))!;
        restored.Validate();
        Assert.Equal((byte?)8, restored.Parameter80);
        Assert.Equal((byte?)2, restored.Parameter82);
        Assert.Equal(initialized.Memory.Arena.Read(0, initialized.Memory.Cursor), restored.Arena!.Read(0, restored.Cursor));
    }
}
