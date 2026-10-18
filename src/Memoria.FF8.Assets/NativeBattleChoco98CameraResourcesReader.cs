using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco98CameraResourcesReader
{
    public static NativeBattleChoco98CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint[] addresses = [0x152B3D0, 0x152B418, 0x152B460, 0x152B4F0, 0x152B538];
        ImmutableArray<NativeBattleMoogleCameraSource> sources = addresses.Select(address => new NativeBattleMoogleCameraSource(address, memory.Read(address, 0x48).ToImmutableArray())).ToImmutableArray();
        byte[] bytes = memory.Read(0x152B4A8, 0x48);
        ImmutableArray<short> values = Enumerable.Range(0, 36).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
        NativeBattleChoco98CameraResources resources = new(sources, new(0x152B4A8, values));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco98CameraResources? nativeChoco98Camera;

    public NativeBattleChoco98CameraResources ReadNativeChoco98CameraResources() => nativeChoco98Camera ??= NativeBattleChoco98CameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
