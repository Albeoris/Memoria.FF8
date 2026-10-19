using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffectFileDirectoryReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> Read(byte[] executable, IEnumerable<short> identifiers)
    {
        NativeExecutableMemory memory = new(executable);
        return identifiers.Select(id =>
        {
            if (id < 0)
            {
                throw new InvalidDataException("Native effect file directory requires its actual nonnegative identifier.");
            }
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCCu + (uint)id * 4, 4));
            byte[] bytes = memory.Read(pointer, 128);
            int length = bytes.AsSpan().IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException("Native effect file directory requires its original terminated name.");
            }
            return new NativeBattleEffectFileAsset(id, Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }
}
