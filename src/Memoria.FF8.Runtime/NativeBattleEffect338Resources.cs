using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect338Resources(NativeBattleEffectOwnedModelResource Model, ImmutableArray<NativeBattleEffectHeapAllocation> Textures, NativeBattleEffectHeapAllocation Camera, NativeVibrationBank Vibration, uint Sound, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableArray<byte> Points, NativeBattleEffectFileAsset CompletionFile)
{
    public void Validate()
    {
        NativeBattleEffect338Skeleton.RequireResource(Model);
        if (Textures.IsDefault || Textures.Length != 3 || Textures.Select(texture => texture.Data.Length).Where((length, index) => length != new[] { 16928, 135200, 16928 }[index]).Any() || Camera.Data.IsDefault || Camera.Data.Length != 1172 || Points.IsDefault || Points.Length != 64 || Programmes.Count != 3 || Meshes.Count != 2 || Sprites.Count != 3 || CompletionFile.FileId != 362 || CompletionFile.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Effect338 requires its selected original embedded resource families and completion file.");
        }
        Vibration.Validate();
        foreach ((uint address, int length) in new (uint, int)[] { (0x1372E60, 5036), (0x137420C, 5036), (0x13755B8, 4252) })
        {
            if (!Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) || source.Address != address || source.Data.IsDefault || source.Data.Length != length)
            {
                throw new InvalidDataException("Effect338 programme leaves its original counted and terminated source.");
            }
        }
    }

    public NativeBattleGfAnimationSource Programme(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException("Effect338 selected an unsupported original programme address.");
    public NativeBattleStaticMeshResource Mesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? mesh) ? mesh : throw new NotSupportedException("Effect338 selected an unsupported original mesh address.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? sprite) ? sprite : throw new NotSupportedException("Effect338 selected an unsupported original sprite address.");
}
