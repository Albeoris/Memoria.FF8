using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleActorDirectorResourcesReader
{
    public static NativeBattleActorDirectorResources Read(byte[] executable, ushort effect)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(effect);
        NativeBattleActorDirectorResources resources = new(effect, new($"FF8_EN:{profile.CameraAddress:X}", memory.Read(profile.CameraAddress, profile.CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.SoundAddress, 4)), new(memory.Read(profile.SourceAddress, profile.SourceLength).ToImmutableArray()) { ResourceAddress = profile.SourceAddress });
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleActorDirectorResources> nativeActorDirectorResources = [];

    public NativeBattleActorDirectorResources ReadNativeActorDirectorResources(ushort effect)
    {
        if (!nativeActorDirectorResources.TryGetValue(effect, out NativeBattleActorDirectorResources? resources))
        {
            resources = NativeBattleActorDirectorResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeActorDirectorResources.Add(effect, resources);
        }
        return resources;
    }
}
