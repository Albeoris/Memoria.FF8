using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMeltEye158ResourcesReader
{
    public static NativeBattleMeltEye158Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(NativeBattleMeltEye158Profile.TextureNameAddress, 11)) != "mag157.tim\0")
        {
            throw new InvalidDataException("Melt-Eye requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleModuleAnchor>.Builder vectors = ImmutableArray.CreateBuilder<NativeBattleModuleAnchor>(11);
        for (int index = 0; index < 11; index++)
        {
            ReadOnlySpan<byte> vector = memory.Read(NativeBattleMeltEye158Profile.VectorsAddress + (uint)(index * 8), 8);
            vectors.Add(new(BinaryPrimitives.ReadInt16LittleEndian(vector), BinaryPrimitives.ReadInt16LittleEndian(vector.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(vector.Slice(4)), BinaryPrimitives.ReadInt16LittleEndian(vector.Slice(6))));
        }
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleMeltEye158Resources resources = new(new("FF8_EN:DAFBF8", memory.Read(NativeBattleMeltEye158Profile.CameraAddress, NativeBattleMeltEye158Profile.CameraLength).ToImmutableArray()), Sprite(NativeBattleMeltEye158Profile.SpriteAAddress), Sprite(NativeBattleMeltEye158Profile.SpriteBAddress), Sprite(NativeBattleMeltEye158Profile.SpriteCAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, NativeBattleMeltEye158Profile.MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, NativeBattleMeltEye158Profile.MeshBAddress), vectors.MoveToImmutable(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(NativeBattleMeltEye158Profile.SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMeltEye158Resources? nativeMeltEye158Resources;
    public NativeBattleMeltEye158Resources ReadNativeMeltEye158Resources() => nativeMeltEye158Resources ??= NativeBattleMeltEye158ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
