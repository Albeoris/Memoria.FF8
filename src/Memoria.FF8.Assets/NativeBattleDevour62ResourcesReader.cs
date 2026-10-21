using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDevour62ResourcesReader
{
    public static NativeBattleDevour62Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint[] addresses = [0x1596A88, 0x1596A98, 0x1596AA8, 0x1596AB8, 0x1596AC8];
        string[] names = ["mag061_0us.tim", "mag061_1.tim", "mag061_2.tim", "mag061_3.tim", "mag061.tim"];
        for (int index = 0; index < addresses.Length; index++)
        {
            if (Encoding.ASCII.GetString(memory.Read(addresses[index], names[index].Length + 1)) != names[index] + "\0")
            {
                throw new InvalidDataException("Devour requires all five original initializer texture names.");
            }
        }
        NativeBattleDevour62Resources result = new(new("FF8_EN:15962D8", memory.Read(0x15962D8, 1968).ToImmutableArray()), Enumerable.Range(0, 9).Select(index =>
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15962B4u + (uint)index * 4, 4));
            return new NativeBattleDevour62SoundSource(address, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4)));
        }).ToImmutableArray());
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDevour62Resources? nativeDevour62Resources;

    public NativeBattleDevour62Resources ReadNativeDevour62Resources()
    {
        return nativeDevour62Resources ??= NativeBattleDevour62ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
