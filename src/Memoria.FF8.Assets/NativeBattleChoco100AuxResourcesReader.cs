using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleChoco99CameraYProgram? nativeChoco100CameraY;

    public NativeBattleChoco99CameraYProgram ReadNativeChoco100CameraYProgram()
    {
        if (nativeChoco100CameraY is null)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            byte[] bytes = memory.Read(0x1529F70, 18);
            nativeChoco100CameraY = new(0x1529F70, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray());
        }
        return nativeChoco100CameraY;
    }
}
