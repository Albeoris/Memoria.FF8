using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRinoa88CameraResourcesReader
{
    public static NativeBattleRinoa88CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleRinoa88CameraResources result = new(NativeBattleRinoa88CameraResources.Addresses.Select(address => new NativeBattleMoogleCameraSource(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, 72))).ToImmutableArray());
        result.Validate();
        return result;
    }
}
