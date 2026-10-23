using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleSonic139ResourcesReader
{
    public static NativeBattleSonic139Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag138.tim\0")
        {
            throw new InvalidDataException("Sonic Wave requires its original initializer texture name.");
        }
        byte[] sqrt = memory.Read(SqrtLookupAddress, 512);
        byte[] sine = memory.Read(SineAddress, 8192);
        byte[] colors = memory.Read(ColorsAddress, 16);
        NativeBattleSonic139Resources resources = new(new("FF8_EN:11D4E90", memory.Read(SourceMeshAddress, EntryCopyLength).ToImmutableArray()), new("FF8_EN:11D7900", memory.Read(MutableFramesAddress, MutableFramesLength).ToImmutableArray()), new(MainActAddress, memory.Read(MainActAddress, 0x26AC).ToImmutableArray()), new(TargetActAddress, memory.Read(TargetActAddress, 0x1F80).ToImmutableArray()), new("FF8_EN:11DD21C", memory.Read(CameraAddress, 0x11C).ToImmutableArray()), memory.Read(AnchorAddress, 8).ToImmutableArray(), Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(sqrt.AsSpan(index * 2, 2))).ToImmutableArray(), Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(sine.AsSpan(index * 2, 2))).ToImmutableArray(), Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(colors.AsSpan(index * 4, 4))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSonic139Resources? nativeSonic139Resources;
    public NativeBattleSonic139Resources ReadNativeSonic139Resources() => nativeSonic139Resources ??= NativeBattleSonic139ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
