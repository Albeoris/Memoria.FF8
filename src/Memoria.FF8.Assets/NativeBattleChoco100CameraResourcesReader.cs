using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100CameraResourcesReader
{
    public static NativeBattleChoco100CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleChoco100CameraResources resources = new(Enumerable.Range(0, 8).Select(index =>
        {
            uint address = 0x1529CD8u + (uint)(index * 0x48);
            return new NativeBattleMoogleCameraSource(address, memory.Read(address, 0x48).ToImmutableArray());
        }).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco100CameraResources? nativeChoco100Camera;

    public NativeBattleChoco100CameraResources ReadNativeChoco100CameraResources() => nativeChoco100Camera ??= NativeBattleChoco100CameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
