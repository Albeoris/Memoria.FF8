using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCuraResourcesReader
{
    public static NativeBattleCuraResources Read(byte[] executable)
    {
        const uint animationAddress = 0x1611104;
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, 8);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
        if (length != 0x161194C - animationAddress || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2)
        {
            throw new InvalidDataException("Native889D60 animation does not end at its original first scene resource.");
        }
        NativeBattleEffectHeapAllocation animation = new("FF8_EN:1611104", NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, length));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x161079C, 0x1610948, 0x16109BC, 0x1610A10, 0x1610AD4, 0x1610C00, 0x1610DAC, 0x1610F58 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address)).ToImmutableArray();
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x1610798, 4).AsSpan());
        ImmutableArray<NativeBattleCuraSceneResource> scenes = [new(0x161194C, NativeBattleEffectStaticResourcesReader.Read(executable, 0x161194C, 0x1611C20 - 0x161194C)), new(0x1611C20, NativeBattleEffectStaticResourcesReader.Read(executable, 0x1611C20, 0x1613684 - 0x1611C20))];
        ImmutableArray<ImmutableArray<short>> schedules = new uint[] { 0x1613684, 0x16136C0, 0x16136FC }.Select(address =>
        {
            ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(executable, address, 60);
            return Enumerable.Range(0, 30).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(index * 2))).ToImmutableArray();
        }).ToImmutableArray();
        return new(animation, sprites, sound) { Scenes = scenes, EmissionSchedules = schedules };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCuraResources? nativeCuraResources;

    public NativeBattleCuraResources ReadNativeCuraResources()
    {
        return nativeCuraResources ??= NativeBattleCuraResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
