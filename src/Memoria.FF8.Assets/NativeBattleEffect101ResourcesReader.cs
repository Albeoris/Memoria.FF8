using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect101ResourcesReader
{
    public static NativeBattleEffect101Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x13249E0, 11)) != "mag100.tim\0")
        {
            throw new InvalidDataException("Effect101 requires its original initializer texture name.");
        }
        NativeBattleEffect101Resources resources = new(new(0x1321BEC, memory.Read(0x1321BEC, 0x2CD4).ToImmutableArray()), new("FF8_EN:13248C0", memory.Read(0x13248C0, 0x11C).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x13249DC, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect101Resources? nativeEffect101Resources;
    public NativeBattleEffect101Resources ReadNativeEffect101Resources() => nativeEffect101Resources ??= NativeBattleEffect101ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
