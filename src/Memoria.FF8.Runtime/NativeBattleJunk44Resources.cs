using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleJunk44Resources(NativeBattleGfAnimationSource Programme, NativeBattleModuleResource Sprite, ImmutableArray<byte> DebrisTable, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    public void Validate()
    {
        if (Programme is null || Programme.Address != ProgrammeAddress || Programme.Data.IsDefault || Programme.Data.Length != ProgrammeLength || Dword(Programme.Data, 0) != 1 || Dword(Programme.Data, 4) != 0xE0 || Dword(Programme.Data, 0xE0) != 1 || Dword(Programme.Data, 0xE4) != 12)
        {
            throw new InvalidDataException("Junk requires its complete one-node twelve-frame programme.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(Programme, ProgrammeWorkspaceLength);
        _ = ReadGeometry(ProgrammeAddress + 12);
        if (Sprite is null || Sprite.Address != SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != SpriteLength || Dword(Sprite.Bytes, 0) != 0x06010524 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 13)
        {
            throw new InvalidDataException("Junk requires its original projected thirteen-frame sprite.");
        }
        for (int frame = 0; frame < 13; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
        if (DebrisTable.IsDefault || DebrisTable.Length != 168 || Meshes.IsDefault || Meshes.Length != 10 || Meshes.Select(mesh => mesh.Address).Distinct().Count() != 10)
        {
            throw new InvalidDataException("Junk requires its seven debris rows and ten addressed meshes.");
        }
        for (int row = 0; row < 7; row++)
        {
            int count = BinaryPrimitives.ReadInt16LittleEndian(DebrisTable.AsSpan().Slice(row * 24 + 16));
            if (count != (row == 5 ? 4 : 1))
            {
                throw new InvalidDataException("Junk debris rows require their original frame counts.");
            }
            for (int frame = 0; frame < count; frame++)
            {
                NativeBattleStaticMeshResource mesh = ReadMesh(Dword(DebrisTable, row * 24 + frame * 4));
                ValidateMesh(mesh, 8, false);
            }
        }
        if (Camera is null || Camera.Identity != "FF8_EN:1354A30" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 32 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x64292)
        {
            throw new InvalidDataException("Junk requires its original camera and positioned sound descriptor.");
        }
        if (Files.IsDefault || Files.Length != 2 || Files[0].FileId != 360 || Files[1].FileId != 361 || Files.Any(file => file.Data.IsDefault || file.Data.Length != 33824))
        {
            throw new InvalidDataException("Junk requires both original completion texture files.");
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address == ProgrammeAddress ? Programme : throw new NotSupportedException("Junk selected an unsupported programme.");
    public NativeBattleModuleResource ReadSprite(uint address) => address == SpriteAddress ? Sprite : throw new NotSupportedException("Junk selected an unsupported projected sprite.");
    public ImmutableArray<byte> ReadTable(int index)
    {
        if ((uint)index >= 7)
        {
            throw new InvalidDataException("Junk debris row leaves its original table.");
        }
        return DebrisTable.AsSpan().Slice(index * 24, 24).ToArray().ToImmutableArray();
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.FirstOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Junk selected an unsupported debris mesh.");

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        if (address != ProgrammeAddress + 12)
        {
            throw new NotSupportedException("Junk selected an unsupported programme geometry.");
        }
        NativeBattleStaticMeshResource geometry = new(0, address, Programme.Data.AsSpan().Slice(12, 212).ToArray().ToImmutableArray());
        ValidateMesh(geometry, 12, true);
        return geometry;
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0 || address < DebrisTableAddress || (ulong)address + (uint)length > DebrisTableAddress + 168)
        {
            throw new NotSupportedException("Junk immutable table read leaves its original front.");
        }
        return DebrisTable.AsSpan().Slice(checked((int)(address - DebrisTableAddress)), length).ToArray().ToImmutableArray();
    }

    private static void ValidateMesh(NativeBattleStaticMeshResource mesh, int header, bool programme)
    {
        int vertices = checked((int)Dword(mesh.Bytes, 4));
        int cursor = checked((int)Dword(mesh.Bytes, 0));
        if (vertices <= 0 || vertices > 4096 || cursor != header + vertices * 8 || programme && (vertices != 9 || Dword(mesh.Bytes, 8) != 1))
        {
            throw new InvalidDataException("Junk geometry requires its actual vertex directory.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < strides.Length; group++)
        {
            int count = checked((int)Dword(mesh.Bytes, cursor));
            cursor = checked(cursor + 4);
            if (count > 4096 || count < 0 || count != 0 && (programme ? group != 3 : group is not (2 or 3 or 4 or 6 or 7)) || programme && group == 3 && count != 4)
            {
                throw new NotSupportedException("Junk selected an unsupported primitive group.");
            }
            for (int face = 0; face < count; face++)
            {
                int start = checked(cursor + face * strides[group]);
                if (start > mesh.Bytes.Length - strides[group])
                {
                    throw new InvalidDataException("Junk face leaves its immutable mesh.");
                }
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(start + 4 + corner * 2, 2)) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Junk face index leaves its actual vertex bank.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Junk geometry must close its complete immutable front.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (bytes.IsDefault || offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Junk resource read leaves its immutable front.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
