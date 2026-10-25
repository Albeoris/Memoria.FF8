using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect320Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, NativeBattleModuleResource ProjectedSprite, uint Sound, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect320 programme 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource Act(uint address) => ReadProgramme(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect320 geometry 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => address == SpriteAddress ? ProjectedSprite : throw new NotSupportedException($"Effect320 sprite 0x{address:X8} is unsupported.");

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect320 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != TextureName || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Programmes is null || Programmes.Count != ProgrammeAddresses.Length || Geometry is null || Geometry.Count != GeometryAddresses.Length || ProjectedSprite is null || ProjectedSprite.Address != SpriteAddress || ProjectedSprite.Bytes.IsDefault || ProjectedSprite.Bytes.Length != SpriteLength || StaticFronts is null)
        {
            throw new InvalidDataException("Effect320 requires its complete selected original resources.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < ProgrammeAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = ReadProgramme(ProgrammeAddresses[index]);
            if (source.Address != ProgrammeAddresses[index] || source.Data.IsDefault || source.Data.Length != ProgrammeLengths[index] || !source.Data.AsSpan().SequenceEqual(ReadStatic(source.Address, source.Data.Length)))
            {
                throw new InvalidDataException("Effect320 programme disagrees with its original addressed source.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[index]);
        }
        for (int index = 0; index < GeometryAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource geometry = ReadGeometry(GeometryAddresses[index]);
            if (geometry.Address != GeometryAddresses[index] || geometry.Bytes.IsDefault || geometry.Bytes.Length != GeometryLengths[index] || ValidateMesh(geometry.Bytes, 12) != geometry.Bytes.Length || !geometry.Bytes.AsSpan().SequenceEqual(ReadStatic(geometry.Address, geometry.Bytes.Length)))
            {
                throw new InvalidDataException("Effect320 geometry leaves its original addressed source.");
            }
        }
        if (Word(ProjectedSprite.Bytes, 8) != 16 || !ProjectedSprite.Bytes.AsSpan().SequenceEqual(ReadStatic(SpriteAddress, SpriteLength)))
        {
            throw new InvalidDataException("Effect320 sprite does not retain its original sixteen frames.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = ProjectedSprite.ProjectedFrame571C80(frame);
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != CameraModelOffset || Word(Camera.Data, 6) != CameraLength || !Camera.Data.AsSpan().SequenceEqual(ReadStatic(CameraAddress, CameraLength)))
        {
            throw new InvalidDataException("Effect320 camera directory leaves its original source.");
        }
        NativeBattleCameraBank camera = new(Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = camera.TrackOffset(selector);
        }
        if (Sound != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4)))
        {
            throw new InvalidDataException("Effect320 decoded sound disagrees with its original source.");
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
            throw new InvalidDataException("Effect320 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect320 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect320 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect320 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect320 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect320 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect320 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect320 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
