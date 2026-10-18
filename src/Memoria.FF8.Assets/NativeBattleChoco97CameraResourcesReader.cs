using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco97CameraResourcesReader
{
    public static NativeBattleChoco97CameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint address = 0x152BA38;
        byte[] bytes = memory.Read(address, 0x120);
        ImmutableArray<NativeBattleMoogleCameraSource>.Builder sources = ImmutableArray.CreateBuilder<NativeBattleMoogleCameraSource>(4);
        for (int index = 0; index < 4; index++)
        {
            sources.Add(new(address + (uint)(index * 0x48), bytes.AsSpan(index * 0x48, 0x48).ToArray().ToImmutableArray()));
        }
        NativeBattleChoco97CameraResources result = new(sources.MoveToImmutable());
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97CameraResources? nativeChoco97Camera;

    public NativeBattleChoco97CameraResources ReadNativeChoco97CameraResources() => nativeChoco97Camera ??= NativeBattleChoco97CameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
