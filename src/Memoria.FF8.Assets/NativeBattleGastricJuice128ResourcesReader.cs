using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleGastricJuice128ResourcesReader
{
    public static NativeBattleGastricJuice128Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag127.tim\0")
        {
            throw new InvalidDataException("Gastric Juice requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Act(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleGastricJuice128Resources resources = new(Act(MainActAddress, 0x1178), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), new("FF8_EN:1233630", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(DistanceTableAddress, 0x4000).ToImmutableArray());
        resources.Validate();
        RequireGeometry(resources.MainAct, [64], [2], [[0, 0, 0, 0, 0, 0, 0, 48]]);
        return resources;
    }
    private static void RequireGeometry(NativeBattleGfAnimationSource source, int[] vertices, int[] variants, int[][] groups)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        if (BinaryPrimitives.ReadInt32LittleEndian(bytes) != vertices.Length)
        {
            throw new InvalidDataException("Gastric Juice ACT geometry directory differs from its original source.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            if (offset < 0 || offset > bytes.Length - 12 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)) != vertices[index] || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)) != variants[index])
            {
                throw new InvalidDataException("Gastric Juice ACT vertex or variant count differs from its original source.");
            }
            int cursor = checked(offset + BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset)));
            for (int group = 0; group < 8; group++)
            {
                if (cursor < 0 || cursor > bytes.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor)) != groups[index][group])
                {
                    throw new InvalidDataException("Gastric Juice ACT polygon groups differ from their original source.");
                }
                cursor = checked(cursor + 4 + groups[index][group] * strides[group]);
                if (cursor > bytes.Length)
                {
                    throw new InvalidDataException("Gastric Juice ACT polygons leave their source.");
                }
            }
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGastricJuice128Resources? nativeGastricJuice128Resources;
    public NativeBattleGastricJuice128Resources ReadNativeGastricJuice128Resources() => nativeGastricJuice128Resources ??= NativeBattleGastricJuice128ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
