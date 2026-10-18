using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99CameraResourcesReader
{
    public static NativeBattleChoco99CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint[] addresses = [0x152AC48, 0x152AC90, 0x152ACD8, 0x152AD20, 0x152AD68, 0x152AE40, 0x152AE88];
        NativeBattleChoco99CameraResources resources = new(addresses.Select(address => new NativeBattleMoogleCameraSource(address, memory.Read(address, 0x48).ToImmutableArray())).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco99CameraResources? nativeChoco99Camera;

    public NativeBattleChoco99CameraResources ReadNativeChoco99CameraResources() => nativeChoco99Camera ??= NativeBattleChoco99CameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
