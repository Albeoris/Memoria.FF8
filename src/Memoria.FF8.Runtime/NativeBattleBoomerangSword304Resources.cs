using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBoomerangSword304Resources(NativeBattleGfAnimationSource Programme, NativeBattleStaticMeshResource Mesh, NativeBattleStaticMeshResource TrailMesh, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        RequireProgramme(Programme, ProgrammeAddress, ProgrammeLength, ProgrammeWorkspaceLength, 6, 14);
        RequireMesh(Mesh, MeshAddress, MeshLength, 52, [0, 0, 63, 15, 0, 0, 0, 0]);
        RequireMesh(TrailMesh, TrailMeshAddress, TrailMeshLength, 14, [0, 0, 0, 0, 16, 0, 0, 0]);
        RequireSprite();
        if (Camera is null || Camera.Identity != "FF8_EN:10312CC" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 32 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641AC)
        {
            throw new InvalidDataException("Boomerang Sword requires its complete original camera and positioned sound descriptor.");
        }
        foreach (NativeBattleGfAnimationSource source in new[] { Programme })
        {
            for (int index = 0; index < 4; index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        ProgrammeAddress => Programme,
        _ => throw new NotSupportedException("Boomerang Sword selected an unsupported original programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { Programme })
        {
            for (int index = 0; index < 4; index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                uint vertices = Dword(source.Data, offset + 4);
                uint shapes = Dword(source.Data, offset + 8);
                int polygon = checked((int)Dword(source.Data, offset));
                if (vertices is 0 or > 51 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Boomerang Sword requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (2 or 3 or 7))
                    {
                        throw new NotSupportedException("Boomerang Sword selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Boomerang Sword geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Boomerang Sword face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Boomerang Sword selected an unsupported addressed geometry front.");
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => address switch
    {
        MeshAddress => Mesh,
        TrailMeshAddress => TrailMesh,
        _ => throw new NotSupportedException("Boomerang Sword selected an unsupported direct mesh.")
    };

    public NativeBattleStaticMeshResource ReadStatic(uint address) => ReadMesh(address);
    public NativeBattleModuleResource ReadSprite(uint address) => address == SpriteAddress ? Sprite : throw new NotSupportedException("Boomerang Sword selected an unsupported projected sprite.");

    private static void RequireMesh(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, uint[] counts)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Dword(mesh.Bytes, 0) != 8 + vertices * 8 || Dword(mesh.Bytes, 4) != vertices)
        {
            throw new InvalidDataException("Boomerang Sword requires its complete direct mesh front.");
        }
        int cursor = checked((int)Dword(mesh.Bytes, 0));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            if (Dword(mesh.Bytes, cursor) != counts[group])
            {
                throw new InvalidDataException("Boomerang Sword direct mesh group counts changed.");
            }
            for (int face = 0; face < counts[group]; face++)
            {
                int first = checked(cursor + 4 + face * strides[group]);
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    uint offset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                    if ((offset & 7) != 0 || offset >= vertices * 8)
                    {
                        throw new InvalidDataException("Boomerang Sword direct face index leaves its original vertices.");
                    }
                }
            }
            cursor = checked(cursor + 4 + (int)counts[group] * strides[group]);
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Boomerang Sword direct mesh extent changed.");
        }
    }

    private void RequireSprite()
    {
        if (Sprite is null || Sprite.Address != SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != SpriteLength || Dword(Sprite.Bytes, 0) != 0x08010524 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 10)
        {
            throw new InvalidDataException("Boomerang Sword requires its original kind8 projected sprite front.");
        }
        for (int phase = 0; phase < 10; phase++)
        {
            _ = Sprite.ProjectedFrame571C80(phase);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != 4)
        {
            throw new InvalidDataException("Boomerang Sword requires the complete original addressed programme sources.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Boomerang Sword requires its original selected node counts and fourteen-frame programme.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Boomerang Sword resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
