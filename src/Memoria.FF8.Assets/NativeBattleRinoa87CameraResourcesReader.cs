using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRinoa87CameraResourcesReader
{
    public static NativeBattleRinoa87CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleMoogleCameraSource> sources = NativeBattleRinoa87CameraResources.Addresses.Select(address => new NativeBattleMoogleCameraSource(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, 72))).ToImmutableArray();
        NativeBattleMoogleCameraCurve Curve(uint address, int count) => new(address, Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(checked(address + (uint)(index * 2)), 2))).ToImmutableArray());
        NativeBattleRinoa87CameraResources resources = new(sources, [Curve(0x15602A8, 28), Curve(0x1560448, 15), Curve(0x15604B0, 32)]);
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRinoa87CameraResources? nativeRinoa87CameraResources;
    public NativeBattleRinoa87CameraResources ReadNativeRinoa87CameraResources() => nativeRinoa87CameraResources ??= NativeBattleRinoa87CameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
