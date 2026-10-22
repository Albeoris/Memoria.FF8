using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleSaliva138ResourcesReader
{
    public static NativeBattleSaliva138Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag137.tim\0")
        {
            throw new InvalidDataException("Saliva requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Act(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        byte[] drift = memory.Read(AgeDriftAddress, 36);
        byte[] deformation = memory.Read(DeformationTableAddress, 0x2004);
        NativeBattleSaliva138Resources resources = new(Act(MainActAddress, 0x1198), Act(SecondaryActAddress, 0xC00), Act(ImpactActAddress, 0xE58), Sprite(SpriteAAddress), Sprite(SpriteBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAddress), new(0, MatrixAddress, 0, 0, memory.Read(MatrixAddress, 32).ToImmutableArray()), new("FF8_EN:11E3024", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), Enumerable.Range(0, 18).Select(index => BinaryPrimitives.ReadInt16LittleEndian(drift.AsSpan(index * 2))).ToImmutableArray(), memory.Read(BoneAnchorAddress, 8).ToImmutableArray(), Enumerable.Range(0, 2049).Select(index => BinaryPrimitives.ReadInt32LittleEndian(deformation.AsSpan(index * 4))).ToImmutableArray(), memory.Read(DistanceTableAddress, 0x4000).ToImmutableArray());
        resources.Validate();
        RequireGeometry(resources.MainAct, [144], [1], [[0, 0, 0, 16, 0, 0, 0, 64]]);
        RequireGeometry(resources.SecondaryAct, [48], [3], [[0, 0, 0, 32, 0, 0, 0, 0]]);
        RequireGeometry(resources.ImpactAct, [33, 49], [2, 1], [[0, 0, 16, 16, 0, 0, 0, 0], [0, 0, 0, 0, 16, 32, 0, 0]]);
        return resources;
    }
    private static void RequireGeometry(NativeBattleGfAnimationSource source, int[] vertices, int[] variants, int[][] groups)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        if (BinaryPrimitives.ReadInt32LittleEndian(bytes) != vertices.Length)
        {
            throw new InvalidDataException("Saliva ACT geometry directory differs from its original source.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            if (offset < 0 || offset > bytes.Length - 12 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)) != vertices[index] || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)) != variants[index])
            {
                throw new InvalidDataException("Saliva ACT vertex or variant count differs from its original source.");
            }
            int cursor = checked(offset + BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset)));
            for (int group = 0; group < 8; group++)
            {
                if (cursor < 0 || cursor > bytes.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor)) != groups[index][group])
                {
                    throw new InvalidDataException("Saliva ACT polygon groups differ from their original source.");
                }
                cursor = checked(cursor + 4 + groups[index][group] * strides[group]);
                if (cursor > bytes.Length)
                {
                    throw new InvalidDataException("Saliva ACT polygons leave their source.");
                }
            }
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSaliva138Resources? nativeSaliva138Resources;
    public NativeBattleSaliva138Resources ReadNativeSaliva138Resources() => nativeSaliva138Resources ??= NativeBattleSaliva138ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
