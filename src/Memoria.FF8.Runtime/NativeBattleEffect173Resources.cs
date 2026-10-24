using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect173Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> ProjectedSprites, NativeBattleGfAnimationSource Programme, ImmutableArray<uint> Palette, ImmutableArray<uint> MeshSources, ImmutableArray<uint> FaceRecordAddresses, uint ProgrammeFlags, ImmutableArray<uint> Sounds, ImmutableArray<byte> BirthRows, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect173 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => ProjectedSprites.TryGetValue(address, out NativeBattleModuleResource? sprite) ? sprite : throw new NotSupportedException($"Effect173 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address == ProgrammeAddress ? Programme : throw new NotSupportedException($"Effect173 programme 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource Act(uint address) => ReadProgramme(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => address == ProgrammeGeometryAddress ? new(0, address, Programme.Data.AsSpan(12, ProgrammeGeometryLength).ToArray().ToImmutableArray()) : ReadMesh(address);

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect173 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag172.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Meshes is null || Meshes.Count != MeshAddresses.Length || ProjectedSprites is null || ProjectedSprites.Count != SpriteAddresses.Length || Programme is null || Programme.Address != ProgrammeAddress || Programme.Data.IsDefault || Programme.Data.Length != ProgrammeLength || Palette.IsDefault || Palette.Length != 16 || MeshSources.IsDefault || MeshSources.Length != 5 || FaceRecordAddresses.IsDefault || FaceRecordAddresses.Length != 5 || Sounds.IsDefault || Sounds.Length != 2 || BirthRows.IsDefault || BirthRows.Length != 32 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect173 requires its complete selected original resources.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource source = ReadMesh(MeshAddresses[index]);
            if (source.Address != MeshAddresses[index] || source.Bytes.IsDefault || source.Bytes.Length != MeshLengths[index] || ValidateMesh(source.Bytes) != source.Bytes.Length)
            {
                throw new InvalidDataException("Effect173 mesh leaves its original physical extent.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrameCounts[index])
            {
                throw new InvalidDataException("Effect173 sprite leaves its original physical extent.");
            }
            for (int frame = 0; frame < SpriteFrameCounts[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (unchecked((short)Word(Sprite(SpriteAddresses[0]).Bytes, 10 + SpriteFrameCounts[0] * 2)) >= 0)
        {
            throw new InvalidDataException("Effect173 palette lifetime requires the original sprite NEXT terminator.");
        }
        for (int index = 0; index < Palette.Length; index++)
        {
            if (Palette[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(PaletteAddress + (uint)index * 4, 4)))
            {
                throw new InvalidDataException("Effect173 palette disagrees with its addressed source.");
            }
        }
        for (int index = 0; index < 5; index++)
        {
            if (MeshSources[index] != MeshAddresses[index + 2] || MeshSources[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(MeshSourcesAddress + (uint)index * 4, 4)) || FaceRecordAddresses[index] != NativeBattleEffect173Profile.FaceRecordAddresses[index] || FaceRecordAddresses[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(FaceRecordDirectoryAddress + (uint)index * 4, 4)))
            {
                throw new InvalidDataException("Effect173 mesh and current face-record directories disagree with their addressed sources.");
            }
        }
        for (int index = 0; index < Sounds.Length; index++)
        {
            if (Sounds[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress + (uint)index * 4, 4)))
            {
                throw new InvalidDataException("Effect173 sound cell disagrees with its addressed source.");
            }
        }
        if (ProgrammeFlags != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(ProgrammeFlagsAddress, 4)) || !BirthRows.AsSpan().SequenceEqual(ReadStatic(BirthRowsAddress, 32)))
        {
            throw new InvalidDataException("Effect173 programme flags or birth rows disagree with their original sources.");
        }
        if (Dword(Programme.Data, 0) != 1 || Dword(Programme.Data, 8) != 12 || ValidateMesh(ReadGeometry(ProgrammeGeometryAddress).Bytes, 12) != ProgrammeGeometryLength)
        {
            throw new InvalidDataException("Effect173 programme geometry leaves its selected source.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(Programme, ProgrammeWorkspaceLength);
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != CameraLength)
        {
            throw new InvalidDataException("Effect173 camera directory leaves its original input.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> data, int headerLength = 8)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > (data.Length - headerLength) / 8 || (headerLength == 12 && Dword(data, 8) != 1) || cursor != checked(headerLength + vertices * 8))
        {
            throw new InvalidDataException("Effect173 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect173 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect173 face index leaves its physical vertex rows.");
                    }
                }
            }
            cursor += count * strides[group];
        }
        return cursor;
    }

    private static void ValidateTim(ImmutableArray<byte> data, uint flags)
    {
        if (Dword(data, 0) != 16 || Dword(data, 4) != flags)
        {
            throw new InvalidDataException("Effect173 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect173 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect173 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect173 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect173 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
