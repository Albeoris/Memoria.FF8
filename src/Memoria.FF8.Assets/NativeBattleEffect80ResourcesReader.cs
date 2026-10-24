using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect80ResourcesReader
{
    public static NativeBattleEffect80Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag079.tim\0")
        {
            throw new InvalidDataException("Effect80 requires its original initializer texture name.");
        }
        NativeBattleEffect80Resources resources = new(mainTim, new("effect80-camera", Bytes(CameraAddress, CameraLength)), new("effect80-vibration", Bytes(VibrationAddress, VibrationLength)), NodeSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => new NativeBattleEffect80NodeResourceBank(pair.Key, Bytes(pair.Key, pair.Value))), InitializedSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => Bytes(pair.Key, pair.Value)), Enumerable.Range(0, 2).Select(index => new NativeBattleGfAnimationSource(ActAddresses[index], Bytes(ActAddresses[index], ActLengths[index]))).ToImmutableArray(), SpriteAddresses.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address)), new uint[] { 0x157B2E4, 0x157B2E8 }.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4))), new (uint Address, int Length)[] { (AgeTableAddress, AgeTableLength), (DistanceAddress, DistanceLength), (ShapeTableAddress, ShapeTableLength), (BirthTableAddress, BirthTableLength) }.ToImmutableDictionary(pair => pair.Address, pair => Bytes(pair.Address, pair.Length)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(TargetPointerAddress, 4)));
        resources.Validate();
        return resources;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect80Resources? nativeEffect80Resources;

    public NativeBattleEffect80Resources ReadNativeEffect80Resources()
    {
        if (nativeEffect80Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect80Resources = NativeBattleEffect80ResourcesReader.Read(ReadNativeExecutable(), new("mag079.tim", Read(magic, "mag079.tim").ToImmutableArray()));
        }
        return nativeEffect80Resources;
    }
}
