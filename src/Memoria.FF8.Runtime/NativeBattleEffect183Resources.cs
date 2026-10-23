using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect183Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect183Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, ImmutableArray<uint> Colors, uint Sound)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0x420, 31, [0, 0, 6, 18, 0, 0, 0, 6]);
        MeshResource(MeshB, MeshBAddress, 0xAB0, 81, [0, 0, 16, 48, 0, 0, 0, 16]);
        uint[] colors = [0x808080, 0x608080, 0x408080, 0x406080, 0x404080, 0x604080, 0x804080, 0x804060, 0x804040, 0x806060];
        if (Colors.IsDefault || !Colors.AsSpan().SequenceEqual(colors) || Sound != 0x49490)
        {
            throw new InvalidDataException("Effect183 requires its complete original color table and sound descriptor.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        _ => throw new NotSupportedException("Effect183 selected an unsupported addressed mesh.")
    };

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Effect183 requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Effect183 mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Effect183 mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Effect183 mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
