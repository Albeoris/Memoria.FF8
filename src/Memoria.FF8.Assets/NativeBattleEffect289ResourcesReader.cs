using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect289ResourcesReader
{
    public static NativeBattleEffect289Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag288.tim\0")
        {
            throw new InvalidDataException("Effect289 requires its selected original initializer texture identity.");
        }
        NativeBattleEffect289Resources result = new(new(MainProgrammeAddress, Bytes(MainProgrammeAddress, MainProgrammeLength)), new(ChildProgrammeAddress, Bytes(ChildProgrammeAddress, ChildProgrammeLength)), MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableArray(), SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableArray(), new("FF8_EN:10724E0", Bytes(CameraAddress, CameraLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), Bytes(PointAddress, 8), Bytes(LookupAddress, LookupLength));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect289Resources? nativeEffect289Resources;

    public NativeBattleEffect289Resources ReadNativeEffect289Resources() => nativeEffect289Resources ??= NativeBattleEffect289ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
