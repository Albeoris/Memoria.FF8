using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSnipeLaser302Resources(NativeBattleGfAnimationSource ProgrammeA, NativeBattleGfAnimationSource ProgrammeB, NativeBattleGfAnimationSource ProgrammeC, NativeBattleGfAnimationSource ProgrammeD, NativeBattleStaticMeshResource StaticMesh, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> SourcePoints16)
{
    public void Validate()
    {
        RequireProgramme(ProgrammeA, ProgrammeAAddress, ProgrammeALength, ProgrammeAWorkspaceLength, 12, 38);
        RequireProgramme(ProgrammeB, ProgrammeBAddress, ProgrammeBLength, ProgrammeBWorkspaceLength, 3, 36);
        RequireProgramme(ProgrammeC, ProgrammeCAddress, ProgrammeCLength, ProgrammeCWorkspaceLength, 5, 24);
        RequireProgramme(ProgrammeD, ProgrammeDAddress, ProgrammeDLength, ProgrammeDWorkspaceLength, 3, 10);
        if (Camera is null || Camera.Identity != "FF8_EN:103B914" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 52 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61B7F)
        {
            throw new InvalidDataException("Snipe Laser requires its complete original camera and positioned sound descriptor.");
        }
        if (SourcePoints16.IsDefault || SourcePoints16.Length != 16 || BinaryPrimitives.ReadInt16LittleEndian(SourcePoints16.AsSpan().Slice(6)) != 11 || BinaryPrimitives.ReadInt16LittleEndian(SourcePoints16.AsSpan().Slice(14)) != 11)
        {
            throw new InvalidDataException("Snipe Laser requires its original packed muzzle point and bone selector11.");
        }
        RequireSprite(SpriteA, SpriteAAddress, SpriteALength, 7);
        RequireSprite(SpriteB, SpriteBAddress, SpriteBLength, 0);
        ValidateStaticMesh();
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        ProgrammeAAddress => ProgrammeA,
        ProgrammeBAddress => ProgrammeB,
        ProgrammeCAddress => ProgrammeC,
        ProgrammeDAddress => ProgrammeD,
        _ => throw new NotSupportedException("Snipe Laser selected an unsupported original programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                uint vertices = Dword(source.Data, offset + 4);
                uint shapes = Dword(source.Data, offset + 8);
                int polygon = checked((int)Dword(source.Data, offset));
                if (vertices is 0 or > 64 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Snipe Laser requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (2 or 3 or 4 or 5 or 7))
                    {
                        throw new NotSupportedException("Snipe Laser selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Snipe Laser geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Snipe Laser face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Snipe Laser selected an unsupported addressed geometry front.");
    }

    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Snipe Laser selected an unsupported projected sprite.")
    };

    public NativeBattleStaticMeshResource Mesh => StaticMesh;

    public NativeBattleStaticMeshResource ReadStatic(uint address) => address == StaticMeshAddress ? StaticMesh : throw new NotSupportedException("Snipe Laser selected an unsupported direct mesh.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => ReadStatic(address);

    private void ValidateStaticMesh()
    {
        if (StaticMesh is null || StaticMesh.Address != StaticMeshAddress || StaticMesh.Bytes.IsDefault || StaticMesh.Bytes.Length != StaticMeshLength || Dword(StaticMesh.Bytes, 0) != 8 + 220 * 8 || Dword(StaticMesh.Bytes, 4) != 220)
        {
            throw new InvalidDataException("Snipe Laser requires its complete direct mesh front.");
        }
        int cursor = checked((int)Dword(StaticMesh.Bytes, 0));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        uint[] counts = [0, 0, 0, 64, 0, 80, 0, 16];
        for (int group = 0; group < 8; group++)
        {
            if (Dword(StaticMesh.Bytes, cursor) != counts[group])
            {
                throw new InvalidDataException("Snipe Laser direct mesh group counts changed.");
            }
            for (int face = 0; face < counts[group]; face++)
            {
                int first = checked(cursor + 4 + face * strides[group]);
                for (int corner = 0; corner < 4; corner++)
                {
                    uint offset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(StaticMesh.Bytes.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                    if ((offset & 7) != 0 || offset >= 220 * 8)
                    {
                        throw new InvalidDataException("Snipe Laser direct face index leaves its original vertices.");
                    }
                }
            }
            cursor = checked(cursor + 4 + (int)counts[group] * strides[group]);
        }
        if (cursor != StaticMeshLength)
        {
            throw new InvalidDataException("Snipe Laser direct mesh extent changed.");
        }
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, byte kind)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || Dword(sprite.Bytes, 0) != ((uint)kind << 24 | 0x010524) || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 16)
        {
            throw new InvalidDataException("Snipe Laser requires its original selected projected sprite front.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != (address == ProgrammeAAddress ? 5u : 2u))
        {
            throw new InvalidDataException("Snipe Laser requires complete original addressed programme sources.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Snipe Laser requires its original selected node counts and programme durations.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Snipe Laser resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
