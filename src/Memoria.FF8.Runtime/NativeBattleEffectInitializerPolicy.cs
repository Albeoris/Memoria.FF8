using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffectInitializerPolicy
{
    public static ImmutableArray<NativeBattleEffectInitializerResource> Resources(NativeBattleEffectBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.InitializerResources is { } resources)
        {
            if (resources.IsDefaultOrEmpty || binding.InitializerResource is not null || binding.InitializerAsset is not null)
            {
                throw new InvalidDataException("An explicit initializer resource list must be nonempty and cannot mix singular owners.");
            }
            if (resources.Any(resource => resource is null || string.IsNullOrEmpty(resource.Name) || resource.GlobalAddress == 0) || resources.Select(resource => resource.GlobalAddress).Distinct().Count() != resources.Length)
            {
                throw new InvalidDataException("Initializer resources require ordered named assets and distinct nonzero publication globals.");
            }
            return resources;
        }
        if (binding.InitializerAssets is not null)
        {
            throw new InvalidDataException("Initializer asset lists require an explicit initializer resource list.");
        }
        return binding.InitializerResource is { } resource ? [resource] : [];
    }

    public static ImmutableArray<NativeBattleEffectInitializerAsset> Assets(NativeBattleEffectBinding binding)
    {
        ImmutableArray<NativeBattleEffectInitializerResource> resources = Resources(binding);
        if (binding.InitializerAssets is { } assets)
        {
            if (assets.IsDefaultOrEmpty || assets.Length != resources.Length || assets.Where((asset, index) => asset is null || asset.Data.IsDefault || asset.Name != resources[index].Name).Any())
            {
                throw new InvalidDataException("Initializer asset lists must contain every ordered original resource.");
            }
            return assets;
        }
        return binding.InitializerAsset is { } asset ? [asset] : [];
    }
}
