using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePetrifyStare127ResourcesReader
{
    public static NativeBattlePetrifyStare127Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x124159C, 11)) != "mag126.tim\0")
        {
            throw new InvalidDataException("Petrify Stare requires its original initializer texture name.");
        }
        NativeBattlePetrifyStare127Resources resources = new(new(0x12377BC, memory.Read(0x12377BC, 0x44C8).ToImmutableArray()), new(0x123BC84, memory.Read(0x123BC84, 0x5828).ToImmutableArray()), new("FF8_EN:12414AC", memory.Read(0x12414AC, 0xEC).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1241598, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattlePetrifyStare127Resources? nativePetrifyStare127Resources;
    public NativeBattlePetrifyStare127Resources ReadNativePetrifyStare127Resources() => nativePetrifyStare127Resources ??= NativeBattlePetrifyStare127ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
