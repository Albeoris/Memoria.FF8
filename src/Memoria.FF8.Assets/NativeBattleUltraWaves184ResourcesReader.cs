using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleUltraWaves184ResourcesReader
{
    public static NativeBattleUltraWaves184Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0xD348B8, 11)) != "mag153.tim\0")
        {
            throw new InvalidDataException("Ultra Waves requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleModuleAnchor> vectors = Enumerable.Range(0, 8).Select(index =>
        {
            byte[] bytes = memory.Read(0xD34778u + (uint)index * 8, 8);
            return new NativeBattleModuleAnchor(Word(bytes, 0), Word(bytes, 2), Word(bytes, 4), Word(bytes, 6));
        }).ToImmutableArray();
        NativeBattleUltraWaves184Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD34450), new("FF8_EN:D328B4", memory.Read(0xD328B4, 0x1B74).ToImmutableArray()), new("FF8_EN:D32750", memory.Read(0xD32750, 0x164).ToImmutableArray()), Dword(0xD346C0), Enumerable.Range(0, 35).Select(index => Dword(0xD346C8u + (uint)index * 4)).ToImmutableArray(), Enumerable.Range(0, 16).Select(index => Dword(0xD347B8u + (uint)index * 4)).ToImmutableArray(), vectors, Enumerable.Range(0, 8).Select(index => unchecked((int)Dword(0xD347F8u + (uint)index * 4))).ToImmutableArray(), memory.Read(0xD34868, 48).ToImmutableArray());
        ValidatePayloads(resources);
        return resources;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }
    public static void ValidatePayloads(NativeBattleUltraWaves184Resources resources)
    {
        resources.Validate();
        ReadOnlySpan<byte> mesh = resources.Ring.Bytes.AsSpan();
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh);
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(mesh.Slice(4));
        if (vertices != 25 || cursor != 8 + vertices * 8)
        {
            throw new InvalidDataException("Ultra Waves ring has an incomplete packed vertex directory.");
        }
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Slice(cursor, 4));
            cursor += 4;
            if (count != (group == 3 ? 16 : 0))
            {
                throw new InvalidDataException("Ultra Waves ring requires its actual textured quad group.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Slice(cursor + 4 + corner * 2, 2));
                    if ((index & 1) != 0 || index * 4 + 8 > vertices * 8)
                    {
                        throw new InvalidDataException("Ultra Waves ring selects an absent packed vertex.");
                    }
                }
                cursor += 24;
            }
        }
        if (cursor != mesh.Length)
        {
            throw new InvalidDataException("Ultra Waves ring has an incomplete primitive boundary.");
        }
        int start = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int end = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        if (NativeBattlePoseDecoder.ReadFrameCounts(resources.CasterDat.Data.AsSpan().Slice(start, end - start)).Length != 9)
        {
            throw new InvalidDataException("Ultra Waves caster binding has an incomplete animation directory.");
        }
    }
    private static short Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
}

public sealed partial class GameAssets
{
    private NativeBattleUltraWaves184Resources? nativeUltraWaves184Resources;
    public NativeBattleUltraWaves184Resources ReadNativeUltraWaves184Resources() => nativeUltraWaves184Resources ??= NativeBattleUltraWaves184ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
