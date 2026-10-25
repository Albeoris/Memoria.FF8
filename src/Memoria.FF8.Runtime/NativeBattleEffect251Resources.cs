using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect251Resources(NativeBattleEffectHeapAllocation EmbeddedTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleGfAnimationSource ReadProgrammeAddress(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect251 programme 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource Act(uint address) => ReadProgrammeAddress(address);
    public NativeBattleGfAnimationSource Source(uint address) => ReadProgrammeAddress(address);
    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect251 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => ReadSprite(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect251 geometry 0x{address:X8} is unsupported.");
    public byte[] Read(uint address, int length) => ReadStatic(address, length);

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect251 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (EmbeddedTim is null || EmbeddedTim.Data.IsDefault || EmbeddedTim.Data.Length != EmbeddedTimLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Programmes is null || Programmes.Count != ProgrammeAddresses.Length || Geometry is null || Geometry.Count != GeometryAddresses.Length || Sprites is null || Sprites.Count != SpriteAddresses.Length || Files is null || Files.Count != 1 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect251 requires its complete original selected resources.");
        }
        ValidateTim(EmbeddedTim.Data);
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 6) != CameraLength || !Camera.Data.AsSpan().SequenceEqual(ReadStatic(CameraAddress, CameraLength)))
        {
            throw new InvalidDataException("Effect251 requires its original control and camera bank.");
        }
        int cameraModelOffset = Word(Camera.Data, 4);
        NativeBattleCameraBank camera = new(Camera.Data.AsSpan().Slice(cameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = camera.TrackOffset(selector);
        }
        for (int index = 0; index < ProgrammeAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = ReadProgrammeAddress(ProgrammeAddresses[index]);
            if (source.Address != ProgrammeAddresses[index] || source.Data.IsDefault || source.Data.Length != ProgrammeLengths[index] || !source.Data.AsSpan().SequenceEqual(ReadStatic(source.Address, source.Data.Length)))
            {
                throw new InvalidDataException("Effect251 ACT leaves its original addressed source.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[index]);
        }
        for (int index = 0; index < GeometryAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource geometry = ReadGeometry(GeometryAddresses[index]);
            if (geometry.Address != GeometryAddresses[index] || geometry.Bytes.IsDefault || geometry.Bytes.Length != GeometryLengths[index] || ValidateGeometry(geometry.Bytes) != geometry.Bytes.Length || !geometry.Bytes.AsSpan().SequenceEqual(ReadStatic(geometry.Address, geometry.Bytes.Length)))
            {
                throw new InvalidDataException("Effect251 geometry leaves its original addressed source.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = ReadSprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrames[index] || !sprite.Bytes.AsSpan().SequenceEqual(ReadStatic(sprite.Address, sprite.Bytes.Length)))
            {
                throw new InvalidDataException("Effect251 sprite leaves its selected original source.");
            }
            int first = sprite.Address == 0xC79220 ? 1 : 0;
            for (int phase = first; phase < SpriteFrames[index]; phase++)
            {
                _ = sprite.ProjectedFrame571C80(phase);
            }
        }
        if (!Files.TryGetValue(StreamFileId, out NativeBattleEffectFileAsset? file) || file.FileId != StreamFileId || file.Name != StreamFileName || file.Data.IsDefault || file.Data.Length != StreamFileLength)
        {
            throw new InvalidDataException("Effect251 requires its original streamed file input.");
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
        if (!ReadStatic(0x1438778, 4).AsSpan().SequenceEqual(new byte[] { 0x30, 0x30, 0x30, 0x30 }) || !ReadStatic(0x145D2B0, 4).AsSpan().SequenceEqual(new byte[] { 0x31, 0x30, 0, 0 }) || !ReadStatic(0x145D2B4, 4).AsSpan().SequenceEqual(new byte[] { 0x41, 0x42, 0x31, 0x30 }))
        {
            throw new InvalidDataException("Effect251 requires its original ACT command-byte tables.");
        }
    }

    private static int ValidateGeometry(ImmutableArray<byte> bytes)
    {
        int count = checked((int)Dword(bytes, 4));
        int shapes = checked((int)Dword(bytes, 8));
        int cursor = checked((int)Dword(bytes, 0));
        if (count <= 0 || shapes <= 0 || cursor != checked(12 + count * shapes * 8))
        {
            throw new InvalidDataException("Effect251 geometry has an invalid shape directory.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int faces = checked((int)Dword(bytes, cursor));
            cursor += 4;
            if (faces > (bytes.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect251 geometry primitive group leaves its source.");
            }
            for (int face = 0; face < faces; face++)
            {
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int offset = Word(bytes, cursor + strides[group] * face + 4 + 2 * corner);
                    if ((offset & 1) != 0 || offset / 2 >= count)
                    {
                        throw new InvalidDataException("Effect251 geometry selects an absent physical vertex.");
                    }
                }
            }
            cursor = checked(cursor + faces * strides[group]);
        }
        return cursor;
    }

    private static void ValidateTim(ImmutableArray<byte> bytes)
    {
        if (Dword(bytes, 0) != 16 || Dword(bytes, 4) != 9)
        {
            throw new InvalidDataException("Effect251 requires its original embedded TIM.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(bytes, cursor));
            if (length < 12 || length > bytes.Length - cursor || length != checked(12 + Word(bytes, cursor + 8) * Word(bytes, cursor + 10) * 2))
            {
                throw new InvalidDataException("Effect251 embedded TIM block leaves its source.");
            }
            cursor += length;
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("Effect251 embedded TIM contains trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset >= 0 && offset <= bytes.Length - 4 ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) : throw new InvalidDataException("Effect251 DWORD leaves its original resource.");
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset >= 0 && offset <= bytes.Length - 2 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2)) : throw new InvalidDataException("Effect251 WORD leaves its original resource.");
}
