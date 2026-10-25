using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect308Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, ImmutableArray<byte>> ScriptSources, ImmutableDictionary<int, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<int, int> WorkspaceLengths, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<byte, uint> SelectedSpritePointers, ImmutableDictionary<uint, NativeBattleModuleResource> ProjectedQuads, uint Sound, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public ImmutableArray<byte> ReadScript(uint address) => ScriptSources.TryGetValue(address, out ImmutableArray<byte> source) ? source : throw new NotSupportedException($"Effect308 script 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(int selector) => Programmes.TryGetValue(selector, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect308 programme selector{selector} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgrammeAddress(uint address) => Programmes.Values.FirstOrDefault(source => source.Address == address) ?? throw new NotSupportedException($"Effect308 programme 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource Act(uint address) => ReadProgrammeAddress(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect308 geometry 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(int selector) => selector is >= 0 and <= 255 && SelectedSpritePointers.TryGetValue((byte)selector, out uint address) ? Sprite(address) : throw new NotSupportedException($"Effect308 sprite selector{selector} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect308 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadProjectedQuads(uint address) => ProjectedQuads.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect308 smoke source 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSmoke(uint address) => ReadProjectedQuads(address);

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect308 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != TextureName || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || ScriptSources is null || ScriptSources.Count != 3 || Programmes is null || Programmes.Count != 4 || WorkspaceLengths is null || WorkspaceLengths.Count != 4 || Geometry is null || Geometry.Count != GeometryAddresses.Length || Sprites is null || Sprites.Count != 3 || SelectedSpritePointers is null || SelectedSpritePointers.Count != 3 || ProjectedQuads is null || ProjectedQuads.Count != 2 || Files is null || Files.Count != 2 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect308 requires its complete original selected resources.");
        }
        ValidateTim(MainTim.Data, 9);
        foreach ((uint address, int length) in ScriptRanges)
        {
            ImmutableArray<byte> bytes = ReadScript(address);
            if (bytes.IsDefault || bytes.Length != length || !bytes.AsSpan().SequenceEqual(ReadStatic(address, length)))
            {
                throw new InvalidDataException("Effect308 script template disagrees with its addressed source.");
            }
        }
        for (int selector = 0; selector < ProgrammeAddresses.Length; selector++)
        {
            NativeBattleGfAnimationSource source = ReadProgramme(selector);
            uint directory = ProgrammeDirectoryAddress + (uint)(selector * 8);
            if (source.Address != ProgrammeAddresses[selector] || source.Data.IsDefault || source.Data.Length != ProgrammeLengths[selector] || WorkspaceLengths[selector] != ProgrammeWorkspaceLengths[selector] || BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(directory, 4)) != source.Address || BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(directory + 4, 4)) != WorkspaceLengths[selector] || !source.Data.AsSpan().SequenceEqual(ReadStatic(source.Address, source.Data.Length)))
            {
                throw new InvalidDataException("Effect308 ACT directory disagrees with its original bounded source.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[WorkspaceLengths[selector]].ToImmutableArray()).Initialize7016B0(source, WorkspaceLengths[selector]);
        }
        for (int index = 0; index < GeometryAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource source = ReadGeometry(GeometryAddresses[index]);
            if (source.Address != GeometryAddresses[index] || source.Bytes.IsDefault || source.Bytes.Length != GeometryLengths[index] || ValidateMesh(source.Bytes, 12) != source.Bytes.Length || !source.Bytes.AsSpan().SequenceEqual(ReadStatic(source.Address, source.Bytes.Length)))
            {
                throw new InvalidDataException("Effect308 geometry leaves its original addressed source.");
            }
        }
        for (int index = 0; index < SpriteSelectors.Length; index++)
        {
            byte selector = SpriteSelectors[index];
            NativeBattleModuleResource sprite = ReadSprite((int)selector);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrames[index] || BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SpriteTableAddress + (uint)(selector * 4), 4)) != sprite.Address || !sprite.Bytes.AsSpan().SequenceEqual(ReadStatic(sprite.Address, sprite.Bytes.Length)))
            {
                throw new InvalidDataException("Effect308 sprite directory leaves its original source.");
            }
            for (int phase = 0; phase < SpriteFrames[index]; phase++)
            {
                _ = sprite.ProjectedFrame571C80(phase);
            }
        }
        RequireSmoke(0x1026038, 328, 13, 12);
        RequireSmoke(0x1025D20, 364, 12, 11);
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != CameraModelOffset || Word(Camera.Data, 6) != CameraLength || !Camera.Data.AsSpan().SequenceEqual(ReadStatic(CameraAddress, CameraLength)) || Dword(Vibration.Data, 0) != 0 || Dword(Vibration.Data, 4) != 256 || Word(Vibration.Data, 256) != 40 || Word(Vibration.Data, 258) != 0 || !Vibration.Data.AsSpan().SequenceEqual(ReadStatic(VibrationAddress, VibrationLength)) || Sound != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4)))
        {
            throw new InvalidDataException("Effect308 camera, vibration or packed sound leaves its selected source.");
        }
        NativeBattleCameraBank camera = new(Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = camera.TrackOffset(selector);
        }
        foreach (int fileId in new[] { 360, 361 })
        {
            NativeBattleEffectFileAsset file = Files[fileId];
            if (file.FileId != fileId || file.Name != $"MA8DEF_{fileId - 360}.TIM" || file.Data.IsDefault || file.Data.Length != 33824)
            {
                throw new InvalidDataException("Effect308 completion file leaves its selected original input.");
            }
            ValidateTim(file.Data, 8);
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }

    private void RequireSmoke(uint address, int length, int frames, int initialLife)
    {
        NativeBattleModuleResource source = ReadProjectedQuads(address);
        if (source.Address != address || source.Bytes.IsDefault || source.Bytes.Length != length || Word(source.Bytes, 8) != frames || !source.Bytes.AsSpan().SequenceEqual(ReadStatic(address, length)))
        {
            throw new InvalidDataException("Effect308 smoke frame source leaves its selected front.");
        }
        for (int life = initialLife; life >= 1; life--)
        {
            int offset = Word(source.Bytes, 8 + (frames - life) * 2);
            int count = checked((int)Dword(source.Bytes, offset));
            if (count is < 0 or > 2 || offset > source.Bytes.Length - 4 - count * 20)
            {
                throw new InvalidDataException("Effect308 smoke quad directory leaves its selected records.");
            }
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> data, int headerLength = 8)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > (data.Length - headerLength) / 8 || (headerLength == 12 && Dword(data, 8) is < 1 or > 32) || cursor != checked(headerLength + vertices * 8 * (headerLength == 12 ? (int)Dword(data, 8) : 1)))
        {
            throw new InvalidDataException("Effect308 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect308 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect308 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect308 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect308 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect308 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect308 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect308 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
