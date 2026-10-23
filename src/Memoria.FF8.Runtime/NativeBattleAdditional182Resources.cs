using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAdditional182Resources(NativeBattleStaticMeshResource HistoryMesh, NativeBattleStaticMeshResource PrimaryMesh, ImmutableArray<NativeBattleStaticMeshResource> MeshVariants, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleEffectHeapAllocation CameraClear, NativeBattleEffectHeapAllocation CameraSet, uint SoundClear, uint SoundSet, ImmutableArray<uint> Colors)
{
    public void Validate()
    {
        MeshResource(HistoryMesh, HistoryMeshAddress, 0x560, 35, [0, 8, 0, 24, 0, 16, 0, 0]);
        MeshResource(PrimaryMesh, PrimaryMeshAddress, 0x270, 25, [0, 0, 0, 16, 0, 0, 0, 0]);
        if (MeshVariants.IsDefault || MeshVariants.Length != MeshVariantAddresses.Length)
        {
            throw new InvalidDataException("Additional182 requires its twelve addressed mesh variants.");
        }
        for (int index = 0; index < MeshVariants.Length; index++)
        {
            MeshResource(MeshVariants[index], MeshVariantAddresses[index], 0x270, 25, [0, 0, 0, 0, 0, 16, 0, 0]);
        }
        RequireSprite(SpriteA, SpriteAAddress, 0x10C, 9);
        RequireSprite(SpriteB, SpriteBAddress, 0x1AC, 16);
        RequireCamera(CameraClear, "FF8_EN:D39370", CameraClearLength);
        RequireCamera(CameraSet, "FF8_EN:D39FD8", CameraSetLength);
        if (SoundClear != 0x494AB || SoundSet != 0x494C1 || Colors.IsDefault || !Colors.AsSpan().SequenceEqual(new uint[] { 0xC04040, 0xA04060, 0x804080, 0x6040A0, 0x4040C0, 0x6040A0, 0x804080, 0xA04060 }))
        {
            throw new InvalidDataException("Additional182 requires its original positioned sounds and complete color table.");
        }
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address)
    {
        if (address == HistoryMeshAddress)
        {
            return HistoryMesh;
        }
        if (address == PrimaryMeshAddress)
        {
            return PrimaryMesh;
        }
        return MeshVariants.FirstOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Additional182 selected an unsupported addressed mesh.");
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => ReadMesh(address);
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Additional182 selected an unsupported addressed sprite.")
    };

    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public NativeBattleEffectHeapAllocation Camera(bool hitFlag4Set) => hitFlag4Set ? CameraSet : CameraClear;
    public uint Sound(bool hitFlag4Set) => hitFlag4Set ? SoundSet : SoundClear;

    private static void RequireCamera(NativeBattleEffectHeapAllocation camera, string identity, int length)
    {
        if (camera is null || camera.Identity != identity || camera.Data.IsDefault || camera.Data.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(camera.Data.AsSpan().Slice(6)) != length)
        {
            throw new InvalidDataException("Additional182 requires its original branch-selected camera resource.");
        }
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Additional182 requires its complete original projected sprite source.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Additional182 requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Additional182 mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Additional182 mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Additional182 mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
