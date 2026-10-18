using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99SecondaryResourcesReader
{
    public static NativeBattleChoco99SecondaryResources Read(ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        return new(ReadMesh(480, 0x19DD2D0, 0x1240, 99, 5, 160), ReadMesh(481, 0x19DE510, 0xE70, 133, 3, 108));

        NativeBattleStaticMeshResource ReadMesh(short id, uint address, int length, int vertices, int expectedGroup, int faces)
        {
            NativeBattleEffectFileAsset[] matches = files.Where(file => file.FileId == id).ToArray();
            if (matches.Length != 1 || matches[0].Data.IsDefault || matches[0].Data.Length != length)
            {
                throw new InvalidDataException("Native Choco secondary requires its complete original mesh file.");
            }
            ImmutableArray<byte> bytes = matches[0].Data;
            int cursor = checked(8 + vertices * 8);
            if (Read(0) != cursor || Read(4) != vertices)
            {
                throw new InvalidDataException("Native Choco secondary mesh requires its original vertex directory.");
            }
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < strides.Length; group++)
            {
                int count = Read(cursor);
                if (count != (group == expectedGroup ? faces : 0))
                {
                    throw new InvalidDataException("Native Choco secondary mesh requires its original primitive groups.");
                }
                cursor = checked(cursor + 4 + count * strides[group]);
            }
            if (cursor != length)
            {
                throw new InvalidDataException("Native Choco secondary mesh leaves its supplied physical extent.");
            }
            return new((byte)(id - 480), address, bytes);

            int Read(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco99SecondaryResources? nativeChoco99Secondary;

    public NativeBattleChoco99SecondaryResources ReadNativeChoco99SecondaryResources() => nativeChoco99Secondary ??= NativeBattleChoco99SecondaryResourcesReader.Read(ReadNativeChoco99IoResources().Files);
}
