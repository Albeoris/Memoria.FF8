using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDegenerator306ResourcesReader
{
    public static NativeBattleDegenerator306Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0xCEB2D0, 11)) != "mag173.tim\0")
        {
            throw new InvalidDataException("Degenerator requires its original initializer texture name.");
        }
        byte[] cycle = memory.Read(0xCEB2A0, 48);
        NativeBattleDegenerator306Resources result = new(new("FF8_EN:CE454C", memory.Read(0xCE454C, 0x1B74).ToImmutableArray()), new("FF8_EN:CE4164", memory.Read(0xCE4164, 1000).ToImmutableArray()), Mesh(0xCEAB94), Mesh(0xCEAF2C), Mesh(0xCE643C), Mesh(0xCEA2E4), Mesh(0xCE7A2C), Sprite(0xCE60C0), Sprite(0xCE62A8), Enumerable.Range(0, 12).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(cycle.AsSpan(index * 4))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xCEB284, 4)));
        ValidatePayloads(result);
        return result;

        NativeBattleStaticMeshResource Mesh(uint address) => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
    public static void ValidatePayloads(NativeBattleDegenerator306Resources resources)
    {
        resources.Validate();
        Mesh(resources.MeshA, [12, 0, 0, 0, 12, 12, 0, 0]);
        Mesh(resources.MeshB, [0, 0, 0, 0, 0, 24, 0, 0]);
        Mesh(resources.MeshC, [0, 0, 24, 120, 0, 0, 0, 24]);
        Mesh(resources.MeshD, [0, 0, 0, 64, 0, 0, 0, 0]);
        Mesh(resources.CustomMesh, [0, 0, 48, 288, 0, 0, 0, 0]);
        int animation = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int sequence = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        ImmutableArray<byte> animationBytes = resources.CasterDat.Data.Slice(animation, sequence - animation);
        ImmutableArray<byte> frames = NativeBattlePoseDecoder.ReadFrameCounts(animationBytes.AsSpan());
        if (frames.Length != 9)
        {
            throw new InvalidDataException("Degenerator requires all original caster binding animations.");
        }
    }
    private static void Mesh(NativeBattleStaticMeshResource mesh, int[] expected)
    {
        int vertices = Int(4);
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] counts = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            int count = Int(cursor);
            if (count != expected[group])
            {
                throw new InvalidDataException("Degenerator mesh primitive groups disagree with the original addressed source.");
            }
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < counts[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Degenerator mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor = checked(cursor + strides[group]);
            }
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Degenerator primitive directory leaves its complete bounded resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDegenerator306Resources? nativeDegenerator306Resources;
    public NativeBattleDegenerator306Resources ReadNativeDegenerator306Resources() => nativeDegenerator306Resources ??= NativeBattleDegenerator306ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
