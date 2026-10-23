using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSandStorm155Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSandStorm155Resources(NativeBattleStaticMeshResource Mesh, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        RequireSprite(SpriteA, SpriteAAddress, 0x1AC, 16);
        RequireSprite(SpriteB, SpriteBAddress, 0x110, 10);
        if (Camera.Identity != "FF8_EN:DB7DC4" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61AE5)
        {
            throw new InvalidDataException("Sand Storm requires its complete original camera and sound.");
        }
        if (Mesh.Address != MeshAddress || Mesh.Bytes.IsDefault || Mesh.Bytes.Length != MeshLength || BinaryPrimitives.ReadInt32LittleEndian(Mesh.Bytes.AsSpan()) != 0x988 || BinaryPrimitives.ReadInt32LittleEndian(Mesh.Bytes.AsSpan().Slice(4)) != VertexCount)
        {
            throw new InvalidDataException("Sand Storm requires its complete original 304-vertex mesh.");
        }
        int cursor = 0x988;
        int[] counts = [0, 0, 0, 224, 0, 0, 0, 64];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < counts.Length; group++)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(Mesh.Bytes.AsSpan().Slice(cursor)) != counts[group])
            {
                throw new InvalidDataException("Sand Storm requires its original 224 FT4 and 64 GT4 faces.");
            }
            cursor = checked(cursor + 4 + counts[group] * strides[group]);
        }
        if (cursor != MeshLength)
        {
            throw new InvalidDataException("Sand Storm mesh groups leave their source owner.");
        }
    }
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Sand Storm selected an unsupported sprite source.")
    };
    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Sand Storm requires its complete original sprite source.");
        }
        for (int index = 0; index < count; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
