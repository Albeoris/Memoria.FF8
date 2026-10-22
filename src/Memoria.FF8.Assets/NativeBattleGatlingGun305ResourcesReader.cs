using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGatlingGun305ResourcesReader
{
    public static NativeBattleGatlingGun305Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0xCEDF30, 11)) != "mag149.tim\0")
        {
            throw new InvalidDataException("Gatling Gun requires its original initializer texture name.");
        }
        NativeBattleGatlingGun305Resources result = new(new("FF8_EN:CEB5E8", memory.Read(0xCEB5E8, 0x1B74).ToImmutableArray()), new("FF8_EN:CEB2DC", memory.Read(0xCEB2DC, 780).ToImmutableArray()), Mesh(0xCED698), Mesh(0xCED998), Mesh(0xCEDC60), Sprite(0xCED358), Sprite(0xCED15C), Sprite(0xCED504), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xCEDF28, 4)));
        ValidatePayloads(result);
        return result;

        NativeBattleStaticMeshResource Mesh(uint address) => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
    public static void ValidatePayloads(NativeBattleGatlingGun305Resources resources)
    {
        resources.Validate();
        Mesh(resources.MeshA, [0, 0, 12, 12, 0, 0, 0, 0]);
        Mesh(resources.MeshB, [0, 0, 0, 8, 0, 0, 0, 8]);
        Mesh(resources.MeshC, [0, 0, 0, 8, 0, 0, 0, 8]);
        int animation = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int sequence = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        ImmutableArray<byte> animationBytes = resources.CasterDat.Data.Slice(animation, sequence - animation);
        ImmutableArray<byte> frames = NativeBattlePoseDecoder.ReadFrameCounts(animationBytes.AsSpan());
        if (frames.Length != 9)
        {
            throw new InvalidDataException("Gatling Gun requires all original caster binding animations.");
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
                throw new InvalidDataException("Gatling Gun mesh primitive groups disagree with the original addressed source.");
            }
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < counts[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Gatling Gun mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor = checked(cursor + strides[group]);
            }
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Gatling Gun primitive directory leaves its complete bounded resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGatlingGun305Resources? nativeGatlingGun305Resources;
    public NativeBattleGatlingGun305Resources ReadNativeGatlingGun305Resources() => nativeGatlingGun305Resources ??= NativeBattleGatlingGun305ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
