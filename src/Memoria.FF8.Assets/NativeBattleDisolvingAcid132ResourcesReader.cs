using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleDisolvingAcid132ResourcesReader
{
    public static NativeBattleDisolvingAcid132Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag131.tim\0")
        {
            throw new InvalidDataException("Disolving Acid requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Act(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        byte[] envelope = memory.Read(EnvelopeAddress, 50);
        NativeBattleDisolvingAcid132Resources resources = new(Act(PrimaryActAddress, 0xCA0), Act(SecondaryActAddress, 0xCA0), Act(ImpactActAddress, 0x578), Sprite(TemplateSpriteAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), new("FF8_EN:1214E1C", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(EmissionVectorsAddress, 80).ToImmutableArray(), Enumerable.Range(0, 25).Select(index => BinaryPrimitives.ReadInt16LittleEndian(envelope.AsSpan(index * 2))).ToImmutableArray(), memory.Read(DistanceTableAddress, 0x4000).ToImmutableArray());
        resources.Validate();
        RequireGeometry(resources.PrimaryAct, [68], [2], [[0, 0, 0, 16, 0, 0, 0, 32]]);
        RequireGeometry(resources.SecondaryAct, [68], [2], [[0, 0, 0, 16, 0, 0, 0, 32]]);
        RequireGeometry(resources.ImpactAct, [33], [1], [[0, 0, 0, 0, 16, 16, 0, 0]]);
        return resources;
    }
    private static void RequireGeometry(NativeBattleGfAnimationSource source, int[] vertices, int[] variants, int[][] groups)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        if (BinaryPrimitives.ReadInt32LittleEndian(bytes) != vertices.Length)
        {
            throw new InvalidDataException("Disolving Acid ACT geometry directory differs from its original source.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            if (offset < 0 || offset > bytes.Length - 12 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)) != vertices[index] || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)) != variants[index])
            {
                throw new InvalidDataException("Disolving Acid ACT vertex or variant count differs from its original source.");
            }
            int cursor = checked(offset + BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset)));
            for (int group = 0; group < 8; group++)
            {
                if (cursor < 0 || cursor > bytes.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor)) != groups[index][group])
                {
                    throw new InvalidDataException("Disolving Acid ACT polygon groups differ from their original source.");
                }
                cursor = checked(cursor + 4 + groups[index][group] * strides[group]);
                if (cursor > bytes.Length)
                {
                    throw new InvalidDataException("Disolving Acid ACT polygons leave their source.");
                }
            }
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDisolvingAcid132Resources? nativeDisolvingAcid132Resources;
    public NativeBattleDisolvingAcid132Resources ReadNativeDisolvingAcid132Resources() => nativeDisolvingAcid132Resources ??= NativeBattleDisolvingAcid132ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
