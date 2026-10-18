using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMoogleCameraResourcesReader
{
    public static NativeBattleMoogleCameraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint address = 0x1532C68;
        byte[] bytes = memory.Read(address, 12 * 0x48);
        ImmutableArray<NativeBattleMoogleCameraSource>.Builder sources = ImmutableArray.CreateBuilder<NativeBattleMoogleCameraSource>();
        for (int index = 0; index < 12; index++)
        {
            byte[] row = bytes.AsSpan(index * 0x48, 0x48).ToArray();
            if (BinaryPrimitives.ReadUInt32LittleEndian(row) != 0 || BinaryPrimitives.ReadInt16LittleEndian(row.AsSpan(0x24)) != 0)
            {
                throw new InvalidDataException("Native Moogle camera source has a different curve or anchor-mode envelope.");
            }
            sources.Add(new(checked(address + (uint)(index * 0x48)), row.ToImmutableArray()));
        }
        return new(sources.ToImmutable());
    }
}
public sealed partial class GameAssets
{
    private NativeBattleMoogleCameraResources? nativeMoogleCamera;
    public NativeBattleMoogleCameraResources ReadNativeMoogleCameraResources() => nativeMoogleCamera ??= NativeBattleMoogleCameraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
