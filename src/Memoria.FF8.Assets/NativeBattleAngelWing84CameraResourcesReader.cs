using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAngelWing84CameraResourcesReader
{
    public static NativeBattleAngelWing84CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleMoogleCameraSource> sources = new uint[] { 0x1575DE0, 0x1575E28, 0x1575E70, 0x1575F20, 0x1575F90, 0x1575FD8 }.Select(address => new NativeBattleMoogleCameraSource(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, 72))).ToImmutableArray();
        NativeBattleMoogleCameraCurve Curve(uint address, int count) => new(address, Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(checked(address + (uint)(index * 2)), 2))).ToImmutableArray());
        NativeBattleAngelWing84CameraResources resources = new(sources, [Curve(0x1575EB8, 50), Curve(0x1575F68, 18)]);
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAngelWing84CameraResources? nativeAngelWing84CameraResources;
    public NativeBattleAngelWing84CameraResources ReadNativeAngelWing84CameraResources() => nativeAngelWing84CameraResources ??= NativeBattleAngelWing84CameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
