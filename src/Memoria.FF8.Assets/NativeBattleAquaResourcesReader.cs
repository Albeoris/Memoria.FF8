using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAquaResourcesReader
{
    public static NativeBattleAquaResources Read(byte[] executable, ushort effect)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleAquaProfile profile = NativeBattleAquaProfile.ForEffect(effect);
        ImmutableDictionary<uint, ImmutableArray<byte>>.Builder sprites = ImmutableDictionary.CreateBuilder<uint, ImmutableArray<byte>>();
        foreach (uint canonical in new uint[] { 0x15A38DC, 0x15A3A3C, 0x15A3B9C, 0x15A3CFC, 0x15A3E5C, 0x15A4008 })
        {
            uint address = profile.SpriteAddress(canonical);
            sprites.Add(address, memory.Read(address, canonical == 0x15A3E5C ? 428 : 352).ToImmutableArray());
        }
        NativeBattleAquaResources result = new(effect, new($"FF8_EN:{profile.AnimationAddress:X}", memory.Read(profile.AnimationAddress, effect == 55 ? 308 : 416).ToImmutableArray()), sprites.ToImmutable(), memory.Read(profile.WaveVerticesAddress, 2504).ToImmutableArray(), memory.Read(profile.MeshAddress, 10600).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.SoundAddress, 4)), effect == 72 ? memory.Read(0x158D9AC, 7028).ToImmutableArray() : null);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleAquaResources> nativeAquaResources = [];

    public NativeBattleAquaResources ReadNativeAquaResources(ushort effect)
    {
        if (!nativeAquaResources.TryGetValue(effect, out NativeBattleAquaResources? selected))
        {
            selected = NativeBattleAquaResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), effect);
            nativeAquaResources.Add(effect, selected);
        }
        return selected;
    }
}
