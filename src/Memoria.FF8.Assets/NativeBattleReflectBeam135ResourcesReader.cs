using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleReflectBeam135ResourcesReader
{
    public static NativeBattleReflectBeam135Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag134.tim\0")
        {
            throw new InvalidDataException("Reflect Beam requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        byte[] selectors = memory.Read(BoneSelectorsAddress, 6);
        NativeBattleReflectBeam135Resources resources = new(Source(MainActAddress, 0x2878), Source(TargetActAddress, 0x1328), Source(SecondaryActAddress, 0x1B5C), Source(ImpactActAddress, 0x744), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, StaticMeshAddress), new("FF8_EN:1201D8C", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt16LittleEndian(selectors.AsSpan(index * 2, 2))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        ValidatePayloads(resources);
        return resources;
    }

    public static void ValidatePayloads(NativeBattleReflectBeam135Resources resources)
    {
        resources.Validate();
        ValidateAct(resources.MainAct, 0x2B8, 20, [(49, 1), (33, 1), (17, 1), (68, 2), (68, 2)], [[0, 0, 0, 0, 16, 32, 0, 0], [0, 0, 16, 0, 0, 0, 0, 16], [0, 0, 0, 0, 16, 0, 0, 0], [0, 0, 0, 16, 0, 0, 0, 32], [0, 0, 0, 0, 0, 48, 0, 0]]);
        ValidateAct(resources.TargetAct, 0x310, 24, [(18, 2), (49, 1)], [[0, 0, 0, 7, 0, 0, 0, 1], [0, 0, 0, 0, 16, 32, 0, 0]]);
        ValidateAct(resources.SecondaryAct, 0x1F8, 13, [(49, 1), (33, 1), (68, 2), (17, 1)], [[0, 0, 0, 0, 16, 32, 0, 0], [0, 0, 16, 0, 0, 0, 0, 16], [0, 0, 0, 0, 0, 48, 0, 0], [0, 0, 0, 0, 16, 0, 0, 0]]);
        ValidateAct(resources.ImpactAct, 0x58, 11, [(49, 1)], [[0, 0, 0, 0, 16, 32, 0, 0]]);
        int end = ValidateGeometry(resources.StaticMesh.Bytes, 0, 8, 220, 1, [0, 0, 0, 0, 0, 80, 0, 80]);
        if (end != resources.StaticMesh.Bytes.Length)
        {
            throw new InvalidDataException("Reflect Beam static mesh groups do not close its original payload.");
        }
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int cache, int duration, (int Vertices, int Variants)[] shapes, int[][] groups)
    {
        int animation = Integer(source.Data, 4);
        for (int index = 0; index < shapes.Length; index++)
        {
            int geometry = Integer(source.Data, 8 + index * 4);
            (int vertices, int variants) = shapes[index];
            int end = ValidateGeometry(source.Data, geometry, 12, vertices, variants, groups[index]);
            int expectedEnd = index + 1 == shapes.Length ? animation : Integer(source.Data, 12 + index * 4);
            if (end != expectedEnd)
            {
                throw new InvalidDataException("Reflect Beam ACT primitive groups leave their original geometry span.");
            }
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[cache].ToImmutableArray()).Initialize7016B0(source, cache);
        for (int tick = 0; tick < duration; tick++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
            {
                if ((uint)node.GeometryIndex >= shapes.Length || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes[node.GeometryIndex].Variants || node.Word42 >= shapes[node.GeometryIndex].Variants)
                {
                    throw new InvalidDataException("Reflect Beam ACT selects an absent original geometry or vertex variant.");
                }
                if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096) && shapes[node.GeometryIndex].Vertices * 8 > MorphBytes)
                {
                    throw new InvalidDataException("Reflect Beam ACT interpolation leaves its original working scratch.");
                }
                return current;
            });
            if (next.Remaining != duration - tick - 1)
            {
                throw new InvalidDataException("Reflect Beam ACT changes its original duration.");
            }
            workspace = next.Workspace;
        }
    }

    private static int ValidateGeometry(ImmutableArray<byte> bytes, int geometry, int header, int vertices, int variants, int[] groups)
    {
        int cursor = checked(geometry + Integer(bytes, geometry));
        if (Integer(bytes, geometry + 4) != vertices || header == 12 && Integer(bytes, geometry + 8) != variants || cursor != geometry + header + vertices * variants * 8)
        {
            throw new InvalidDataException("Reflect Beam requires its original packed vertex banks.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = Integer(bytes, cursor);
            cursor = checked(cursor + 4);
            if (count != groups[group] || count < 0 || cursor > bytes.Length - checked(count * strides[group]))
            {
                throw new InvalidDataException("Reflect Beam primitive groups leave their original source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < 3 + group % 2; corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2, 2)) * 4;
                    if ((vertex & 7) != 0 || vertex > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Reflect Beam face selects an absent packed vertex.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        return cursor;
    }
    private static int Integer(ImmutableArray<byte> bytes, int offset)
    {
        if (bytes.IsDefault || offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Reflect Beam read leaves its original resource.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleReflectBeam135Resources? nativeReflectBeam135Resources;
    public NativeBattleReflectBeam135Resources ReadNativeReflectBeam135Resources() => nativeReflectBeam135Resources ??= NativeBattleReflectBeam135ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
