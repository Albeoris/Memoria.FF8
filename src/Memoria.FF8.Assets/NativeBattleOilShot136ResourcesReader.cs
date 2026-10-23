using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleOilShot136ResourcesReader
{
    public static NativeBattleOilShot136Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag135.tim\0")
        {
            throw new InvalidDataException("Oil Shot requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Act(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        byte[] deformation = memory.Read(DeformationTableAddress, 0x2004);
        NativeBattleOilShot136Resources resources = new(Act(MainActAddress, 0xD28), Act(SecondaryActAddress, 0x1A6C), Act(ImpactActAddress, 0xC74), Act(EmitterActAddress, 0x290), Act(LargeParticleActAddress, 0x158), Act(LargeImpactActAddress, 0x888), Sprite(SpriteAAddress), Sprite(SpriteBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAddress), new(0, MatrixAddress, 0, 0, memory.Read(MatrixAddress, 32).ToImmutableArray()), new("FF8_EN:11F7EDC", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(BoneAnchorAddress, 8).ToImmutableArray(), Enumerable.Range(0, 2049).Select(index => BinaryPrimitives.ReadInt32LittleEndian(deformation.AsSpan(index * 4))).ToImmutableArray());
        resources.Validate();
        RequireGeometry(resources.MainAct, [64], [2], [[0, 0, 0, 0, 0, 0, 0, 48]]);
        RequireGeometry(resources.SecondaryAct, [63, 63], [3, 2], [[0, 0, 0, 40, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 40]]);
        RequireGeometry(resources.ImpactAct, [64], [2], [[0, 0, 0, 48, 0, 0, 0, 0]]);
        RequireGeometry(resources.EmitterAct, [4, 4], [1, 1], [[0, 0, 0, 1, 0, 0, 0, 0], [0, 0, 0, 1, 0, 0, 0, 0]]);
        RequireGeometry(resources.LargeParticleAct, [4, 4], [1, 1], [[0, 0, 0, 1, 0, 0, 0, 0], [0, 0, 0, 1, 0, 0, 0, 0]]);
        RequireGeometry(resources.LargeImpactAct, [64], [1], [[0, 0, 0, 0, 0, 48, 0, 0]]);
        return resources;
    }
    private static void RequireGeometry(NativeBattleGfAnimationSource source, int[] vertices, int[] variants, int[][] groups)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        if (BinaryPrimitives.ReadInt32LittleEndian(bytes) != vertices.Length)
        {
            throw new InvalidDataException("Oil Shot ACT geometry directory differs from its original source.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            if (offset < 0 || offset > bytes.Length - 12 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)) != vertices[index] || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)) != variants[index])
            {
                throw new InvalidDataException("Oil Shot ACT vertex or variant count differs from its original source.");
            }
            int cursor = checked(offset + BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset)));
            for (int group = 0; group < 8; group++)
            {
                if (cursor < 0 || cursor > bytes.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor)) != groups[index][group])
                {
                    throw new InvalidDataException("Oil Shot ACT polygon groups differ from their original source.");
                }
                cursor = checked(cursor + 4 + groups[index][group] * strides[group]);
                if (cursor > bytes.Length)
                {
                    throw new InvalidDataException("Oil Shot ACT polygons leave their source.");
                }
            }
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleOilShot136Resources? nativeOilShot136Resources;
    public NativeBattleOilShot136Resources ReadNativeOilShot136Resources() => nativeOilShot136Resources ??= NativeBattleOilShot136ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
