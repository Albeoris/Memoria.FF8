using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect315ResourcesReader
{
    public static NativeBattleEffect315Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag314.tim\0")
        {
            throw new InvalidDataException("Effect315 requires its original initializer texture identity.");
        }
        ImmutableArray<byte> vibration = Bytes(VibrationAddress, VibrationLength);
        NativeBattleEffect315Resources result = new(SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableArray(), MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableArray(), new("FF8_EN:CD2B08", Bytes(CameraAddress, CameraLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAAddress, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundBAddress, 4)), new(memory.SourceHash, [BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan().Slice(4))], vibration), Bytes(PointRowsAddress, 24), Bytes(VectorRowsAddress, 48), Bytes(CasterColorsAddress, 48), Bytes(RecipientColorsAddress, 96));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect315Resources? nativeEffect315Resources;

    public NativeBattleEffect315Resources ReadNativeEffect315Resources() => nativeEffect315Resources ??= NativeBattleEffect315ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
