using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect303VibrationResource(uint Address, NativeVibrationBank Bank);

public sealed record NativeBattleEffect303Resources(NativeBattleGfAnimationSource MainProgramme, NativeBattleGfAnimationSource ChildProgramme, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleModuleResource ParticleSprite, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound, NativeBattleEffect303VibrationResource Vibration, NativeBattleEffectFileAsset CompletionFile, ImmutableArray<byte> ChildPoints32, ImmutableArray<byte> MuzzlePoints16)
{
    public void Validate()
    {
        RequireProgramme(MainProgramme, MainProgrammeAddress, MainProgrammeLength, MainProgrammeWorkspaceLength, 5, 22);
        RequireProgramme(ChildProgramme, ChildProgrammeAddress, ChildProgrammeLength, ChildProgrammeWorkspaceLength, 4, 16);
        if (Camera is null || Camera.Identity != "FF8_EN:10340AC" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != CameraLength || Sound != 0x6419D)
        {
            throw new InvalidDataException("Effect303 requires its complete addressed camera and sound.");
        }
        if (Meshes.IsDefault || Meshes.Length != 4 || ChildPoints32.IsDefault || ChildPoints32.Length != 32 || MuzzlePoints16.IsDefault || MuzzlePoints16.Length != 16)
        {
            throw new InvalidDataException("Effect303 requires four complete meshes and its selected packed point fronts.");
        }
        for (int index = 0; index < Meshes.Length; index++)
        {
            ValidateMesh(Meshes[index], index);
        }
        RequireSprite(ParticleSprite, ParticleSpriteAddress, ParticleSpriteLength, 10, 10);
        RequireSprite(Sprite, SpriteAddress, SpriteLength, 12, 7);
        if (Word(MuzzlePoints16, 6) != 3 || Word(MuzzlePoints16, 14) != 39)
        {
            throw new InvalidDataException("Effect303 requires its actual current caster bone selectors.");
        }
        if (Vibration is null || Vibration.Address != VibrationAddress || Vibration.Bank is null || Vibration.Bank.Bytes.IsDefault || Vibration.Bank.Bytes.Length != VibrationLength || Vibration.Bank.Offsets.IsDefault || !Vibration.Bank.Offsets.SequenceEqual(new[] { 0, 256 }))
        {
            throw new InvalidDataException("Effect303 requires its complete addressed vibration bank.");
        }
        Vibration.Bank.Validate();
        if (Vibration.Bank.ReadWord(256) != 14 || Vibration.Bank.ReadWord(258) != 0)
        {
            throw new InvalidDataException("Effect303 vibration channel lengths changed.");
        }
        ValidateCompletionFile();
        foreach (NativeBattleGfAnimationSource source in new[] { MainProgramme, ChildProgramme })
        {
            for (int index = 0; index < 3; index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        MainProgrammeAddress => MainProgramme,
        ChildProgrammeAddress => ChildProgramme,
        _ => throw new NotSupportedException("Effect303 selected an unsupported programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { MainProgramme, ChildProgramme })
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
                if (vertices is 0 or > 48 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Effect303 requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [14, 18, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group != 3)
                    {
                        throw new NotSupportedException("Effect303 selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Effect303 geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Effect303 face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect303 selected an unsupported addressed geometry front.");
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.SingleOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Effect303 selected an unsupported static mesh.");

    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        ParticleSpriteAddress => ParticleSprite,
        SpriteAddress => Sprite,
        _ => throw new NotSupportedException("Effect303 selected an unsupported sprite.")
    };

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect303 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect303 static read leaves its selected immutable fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        yield return (MainProgramme.Address, MainProgramme.Data);
        yield return (ChildProgramme.Address, ChildProgramme.Data);
        foreach (NativeBattleStaticMeshResource mesh in Meshes)
        {
            yield return (mesh.Address, mesh.Bytes);
        }
        yield return (ParticleSprite.Address, ParticleSprite.Bytes);
        yield return (Sprite.Address, Sprite.Bytes);
        yield return (CameraAddress, Camera.Data);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (VibrationAddress, Vibration.Bank.Bytes);
        yield return (ChildPointsAddress, ChildPoints32);
        yield return (MuzzlePointsAddress, MuzzlePoints16);
    }

    private static void ValidateMesh(NativeBattleStaticMeshResource mesh, int index)
    {
        int vertices = MeshVertexCounts[index];
        if (mesh is null || mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index] || Dword(mesh.Bytes, 0) != 8 + vertices * 8 || Dword(mesh.Bytes, 4) != vertices)
        {
            throw new InvalidDataException("Effect303 requires its complete selected static mesh front.");
        }
        int cursor = 8 + vertices * 8;
        int[] sizes = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] triangles = [76, 30, 30, 10];
        for (int group = 0; group < 8; group++)
        {
            uint count = Dword(mesh.Bytes, cursor);
            int expected = group == 2 ? triangles[index] : group == 3 && index == 0 ? 2 : 0;
            if (count != expected)
            {
                throw new InvalidDataException("Effect303 static mesh face counts changed.");
            }
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                int first = checked(cursor + face * sizes[group]);
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    uint offset = (uint)Word(mesh.Bytes, first + 4 + corner * 2) * 4;
                    if ((offset & 7) != 0 || offset >= vertices * 8)
                    {
                        throw new InvalidDataException("Effect303 static face leaves its selected vertex bank.");
                    }
                }
            }
            cursor = checked(cursor + (int)count * sizes[group]);
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Effect303 static mesh has an incomplete addressed directory.");
        }
    }

    private void ValidateCompletionFile()
    {
        if (CompletionFile is null || CompletionFile.FileId != CompletionFileId || !string.Equals(CompletionFile.Name, "MA8DEF_0.TIM", StringComparison.OrdinalIgnoreCase) || CompletionFile.Data.IsDefault || CompletionFile.Data.Length != 33824 || Dword(CompletionFile.Data, 0) != 16 || Dword(CompletionFile.Data, 4) != 8)
        {
            throw new InvalidDataException("Effect303 completion requires its selected original indexed texture.");
        }
        if (Dword(CompletionFile.Data, 8) != 1036 || Dword(CompletionFile.Data, 1044) != 32780 || Word(CompletionFile.Data, 12) != 624 || Word(CompletionFile.Data, 14) != 224 || Word(CompletionFile.Data, 16) != 16 || Word(CompletionFile.Data, 18) != 32 || Word(CompletionFile.Data, 1048) != 704 || Word(CompletionFile.Data, 1050) != 256 || Word(CompletionFile.Data, 1052) != 64 || Word(CompletionFile.Data, 1054) != 256)
        {
            throw new InvalidDataException("Effect303 completion texture rectangles changed.");
        }
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int frames, byte kind)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || Dword(sprite.Bytes, 0) != ((uint)kind << 24 | 0x010524) || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Effect303 requires its original selected projected sprite front.");
        }
        for (int frame = 0; frame < frames; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != 3)
        {
            throw new InvalidDataException("Effect303 requires its complete selected programme source.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Effect303 programme node count or duration changed.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 2)
        {
            throw new InvalidDataException("Effect303 WORD read leaves its complete resource.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Effect303 resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
