using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleBreath129ResourcesReader
{
    public static NativeBattleBreath129Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag128.tim\0")
        {
            throw new InvalidDataException("Breath requires its original initializer texture name.");
        }
        byte[] sqrt = memory.Read(SqrtTableAddress, 512);
        byte[] trig = memory.Read(TrigonometricTableAddress, 0x4000);
        NativeBattleBreath129Resources resources = new(new("FF8_EN:12272DC", Bytes(MeshAddress, MutableSourcesLength)), new(FirstActAddress, Bytes(FirstActAddress, 0x1AB0)), new(SecondActAddress, Bytes(SecondActAddress, 0xC30)), new(ThirdActAddress, Bytes(ThirdActAddress, 0x7F4)), new(FourthActAddress, Bytes(FourthActAddress, 0xA50)), new(0, SpriteAddress, 0, 0, Bytes(SpriteAddress, 0xF8)), new("FF8_EN:12291CC", Bytes(CameraAddress, 0x128)), Bytes(CapturedPairsAddress, 32), Bytes(ControlPairsAddress, 32), Bytes(ControlTrianglesAddress, 0x6F0), Bytes(StreamDirectoryAddress, 0x500), Bytes(DistanceTableAddress, 0x4000), Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(sqrt.AsSpan(index * 2, 2))).ToImmutableArray(), Enumerable.Range(0, 8192).Select(index => BinaryPrimitives.ReadInt16LittleEndian(trig.AsSpan(index * 2, 2))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBreath129Resources? nativeBreath129Resources;
    public NativeBattleBreath129Resources ReadNativeBreath129Resources() => nativeBreath129Resources ??= NativeBattleBreath129ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
