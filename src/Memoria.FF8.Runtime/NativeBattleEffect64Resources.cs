using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect64Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, uint Sound, ImmutableArray<NativeBattleGfAnimationSource> Acts, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableArray<byte> ProgrammeDescriptors, ImmutableDictionary<uint, ImmutableArray<byte>> OriginalScripts)
{
    private static readonly int[][] Vertices = [[25, 33], [204, 44, 44]];
    private static readonly int[][] Shapes = [[1, 1], [1, 1, 1]];
    private static readonly int[][][] Groups = [[[0, 0, 0, 16, 0, 0, 0, 0], [0, 0, 0, 0, 16, 16, 0, 0]], [[0, 0, 0, 0, 0, 0, 0, 128], [0, 0, 0, 5, 0, 0, 0, 25], [0, 0, 0, 0, 0, 30, 0, 0]]];

    public NativeBattleGfAnimationSource Act(uint address) => Acts.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Effect64 ACT source is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException("Effect64 sprite source is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public ImmutableArray<byte> ReadOriginalScript(uint address) => OriginalScripts.TryGetValue(address, out ImmutableArray<byte> source) ? source : throw new NotSupportedException("Effect64 original script input is unsupported.");

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag063.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength)
        {
            throw new InvalidDataException("Effect64 requires its complete named texture input.");
        }
        ValidateTim();
        if (Camera is null || Camera.Identity != "FF8_EN:1342D40" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || Sound != 0x668B3 || Dword(Vibration.Data, 0) != 256 || Dword(Vibration.Data, 4) != 264)
        {
            throw new InvalidDataException("Effect64 requires its exact camera and vibration fronts.");
        }
        new NativeVibrationBank(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Vibration.Data.AsSpan())), [256, 264], Vibration.Data).Validate();
        if (Acts.IsDefault || Acts.Length != 2 || Sprites is null || Sprites.Count != 2 || ProgrammeDescriptors.IsDefault || ProgrammeDescriptors.Length != 24 || OriginalScripts is null || OriginalScripts.Count != ScriptAddresses.Length)
        {
            throw new InvalidDataException("Effect64 resources have different selected directory dimensions.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleGfAnimationSource source = Acts[index];
            if (source.Address != ProgrammeAddresses[index] || source.Data.IsDefault || source.Data.Length != ProgrammeLengths[index] || Dword(source.Data, 0) != Vertices[index].Length || Dword(ProgrammeDescriptors, index * 12) != source.Address || Dword(ProgrammeDescriptors, index * 12 + 4) != ProgrammeWorkspaceLengths[index] || Dword(ProgrammeDescriptors, index * 12 + 8) != 0x30)
            {
                throw new InvalidDataException("Effect64 requires its original ACT and inline ASCII selector rows.");
            }
            int table = checked((int)Dword(source.Data, 4));
            int nodes = index == 0 ? 2 : 18;
            if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != (index == 0 ? 12 : 60))
            {
                throw new InvalidDataException("Effect64 ACT duration or node count changed.");
            }
            for (int node = 0; node < nodes; node++)
            {
                int offset = checked(table + (int)Dword(source.Data, table + 8 + node * 4));
                if ((Dword(source.Data, offset + 4) & 0x2000) != 0)
                {
                    throw new InvalidDataException("Effect64 selected ACT has an unsupported shape channel.");
                }
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[index]);
            for (int geometry = 0; geometry < Vertices[index].Length; geometry++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
            }
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != (index == 0 ? 18 : 16))
            {
                throw new InvalidDataException("Effect64 projected sprite source changed.");
            }
            for (int frame = 0; frame < Word(sprite.Bytes, 8); frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        foreach ((uint address, int length) in ScriptLengths)
        {
            ImmutableArray<byte> source = ReadOriginalScript(address);
            if (source.IsDefault || source.Length != length || Word(source, 0) >= 71 || Word(source, length - 2) != 0x1100 && (Word(source, length - 2) != 0 || Word(source, length - 4) != 0x1100))
            {
                throw new InvalidDataException("Effect64 original mutable script input has different selected bounds.");
            }
        }
    }

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        for (int index = 0; index < Acts.Length; index++)
        {
            NativeBattleGfAnimationSource source = Acts[index];
            for (int geometry = 0; geometry < Vertices[index].Length; geometry++)
            {
                int offset = checked((int)Dword(source.Data, 8 + geometry * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                int vertices = Vertices[index][geometry];
                int shapes = Shapes[index][geometry];
                int polygon = checked(12 + vertices * shapes * 8);
                if (Dword(source.Data, offset) != polygon || Dword(source.Data, offset + 4) != vertices || Dword(source.Data, offset + 8) != shapes)
                {
                    throw new InvalidDataException("Effect64 ACT geometry changed its selected shape bounds.");
                }
                int cursor = offset + polygon;
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < 8; group++)
                {
                    int count = checked((int)Dword(source.Data, cursor));
                    cursor += 4;
                    if (count != Groups[index][geometry][group] || count > (source.Data.Length - cursor) / strides[group])
                    {
                        throw new InvalidDataException("Effect64 ACT face group leaves its exact source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            int row = Word(source.Data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                            if ((row & 7) != 0 || row > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Effect64 ACT face index leaves its current physical rows.");
                            }
                        }
                    }
                    cursor += count * strides[group];
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect64 geometry address is outside its selected ACT sources.");
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect64 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect64 static reads exclude current mutable script owners.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Bytes)> StaticFronts()
    {
        yield return (CameraAddress, Camera.Data);
        yield return (VibrationAddress, Vibration.Data);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (SpriteDirectoryAddress, SpriteAddresses.SelectMany(BitConverter.GetBytes).ToImmutableArray());
        yield return (ProgrammeDirectoryAddress, ProgrammeDescriptors);
        foreach (NativeBattleGfAnimationSource source in Acts)
        {
            yield return (source.Address, source.Data);
        }
        foreach (NativeBattleModuleResource source in Sprites.Values)
        {
            yield return (source.Address, source.Bytes);
        }
    }

    private void ValidateTim()
    {
        ImmutableArray<byte> bytes = MainTim.Data;
        if (Dword(bytes, 0) != 16 || Dword(bytes, 4) != 9)
        {
            throw new InvalidDataException("Effect64 requires its original indexed TIM layout.");
        }
        int cursor = 8;
        foreach (ushort[] rectangle in new ushort[][] { [320, 240, 256, 5], [384, 256, 256, 256] })
        {
            int length = checked((int)Dword(bytes, cursor));
            if (length != 12 + rectangle[2] * rectangle[3] * 2 || !Enumerable.Range(0, 4).Select(index => Word(bytes, cursor + 4 + index * 2)).SequenceEqual(rectangle))
            {
                throw new InvalidDataException("Effect64 TIM block changed its original rectangle.");
            }
            cursor += length;
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("Effect64 TIM has bytes outside its exact two blocks.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
}
