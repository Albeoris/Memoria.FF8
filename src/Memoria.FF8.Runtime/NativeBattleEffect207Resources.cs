using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect207Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleGfAnimationSource Programme, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, NativeBattleModuleResource ProjectedSprite, ImmutableArray<byte> OriginalCopiedMeshData, ImmutableArray<byte> Selectors, uint Sound, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address == ProgrammeAddress ? Programme : throw new NotSupportedException($"Effect207 programme 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource Act(uint address) => ReadProgramme(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? geometry) ? geometry : throw new NotSupportedException($"Effect207 geometry 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => address == OriginalMeshAddress ? new(0, address, OriginalCopiedMeshData.AsSpan().Slice(0, OriginalMeshLength).ToArray().ToImmutableArray()) : ReadGeometry(address);
    public NativeBattleModuleResource Sprite(uint address) => address == SpriteAddress ? ProjectedSprite : throw new NotSupportedException($"Effect207 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect207 static range 0x{address:X8}/{length} is unsupported.");
    }

    public short PaletteWord(int variant, int w)
    {
        if ((uint)variant >= 32 || (uint)w >= 128)
        {
            throw new InvalidDataException("Effect207 shaping lookup leaves its selected source.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(ReadStatic(ShapingTableAddress + (uint)(variant * 256 + w * 2), 2));
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag206.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Programme is null || Programme.Address != ProgrammeAddress || Programme.Data.IsDefault || Programme.Data.Length != ProgrammeLength || Geometry is null || Geometry.Count != GeometryAddresses.Length || ProjectedSprite is null || ProjectedSprite.Address != SpriteAddress || ProjectedSprite.Bytes.IsDefault || ProjectedSprite.Bytes.Length != SpriteLength || OriginalCopiedMeshData.IsDefault || OriginalCopiedMeshData.Length != MutableMeshLength || Selectors.IsDefault || Selectors.Length != 8 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect207 requires its complete selected original resources.");
        }
        ValidateTim(MainTim.Data, 9);
        if (ValidateMesh(ReadMesh(OriginalMeshAddress).Bytes) != OriginalMeshLength)
        {
            throw new InvalidDataException("Effect207 original copied geometry leaves its source.");
        }
        for (int index = 0; index < GeometryAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource geometry = ReadGeometry(GeometryAddresses[index]);
            if (geometry.Address != GeometryAddresses[index] || geometry.Bytes.Length != GeometryLengths[index] || ValidateMesh(geometry.Bytes, 12) != geometry.Bytes.Length)
            {
                throw new InvalidDataException("Effect207 programme geometry leaves its selected source.");
            }
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(Programme, ProgrammeWorkspaceLength);
        if (Word(ProjectedSprite.Bytes, 8) != 16)
        {
            throw new InvalidDataException("Effect207 sprite frame count changed.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = ProjectedSprite.ProjectedFrame571C80(frame);
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != CameraModelOffset || Word(Camera.Data, 6) != CameraLength)
        {
            throw new InvalidDataException("Effect207 camera directory leaves its original source.");
        }
        NativeBattleCameraBank camera = new(Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = camera.TrackOffset(selector);
        }
        if (!Selectors.AsSpan().SequenceEqual(ReadStatic(SelectorsAddress, 8)) || Sound != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4)) || !OriginalCopiedMeshData.AsSpan().SequenceEqual(ReadStatic(OriginalMeshAddress, MutableMeshLength)))
        {
            throw new InvalidDataException("Effect207 decoded fields disagree with their addressed sources.");
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
        if (vertices <= 0 || vertices > (data.Length - headerLength) / 8 || (headerLength == 12 && Dword(data, 8) is < 1 or > 32) || cursor != checked(headerLength + vertices * 8 * (headerLength == 12 ? (int)Dword(data, 8) : 1)))
        {
            throw new InvalidDataException("Effect207 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect207 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect207 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect207 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect207 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect207 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect207 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect207 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
