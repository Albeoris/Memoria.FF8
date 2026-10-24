using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect80NodeResourceBank(uint Address, ImmutableArray<byte> Data)
{
    public void Validate()
    {
        if (!NodeSourceLengths.TryGetValue(Address, out int length) || Data.IsDefault || Data.Length != length || Dword(0) != 0)
        {
            throw new InvalidDataException("Effect80 requires its selected original cold relative node bank.");
        }
        int nodes = 0;
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = Directory(table, index);
                if (pointer != 0 && (pointer >= Data.Length || table == 0 && pointer > Data.Length - 344))
                {
                    throw new InvalidDataException("Effect80 relative node directory leaves its exact source front.");
                }
                if (table != 0 || pointer == 0)
                {
                    continue;
                }
                nodes++;
                for (int field = 0xC8; field < 0x148; field += 4)
                {
                    if (Dword(checked((int)pointer + field)) >= Data.Length)
                    {
                        throw new InvalidDataException("Effect80 channel relocation leaves its own current node bank.");
                    }
                }
            }
        }
        int expected = Address == 0x1749C64 ? 7 : Address == 0x174F0BC ? 4 : 7;
        if (nodes != expected)
        {
            throw new InvalidDataException("Effect80 source has different original node-record coverage.");
        }
    }

    public uint Directory(int table, int index)
    {
        if ((uint)table >= 4 || (uint)index >= 16)
        {
            throw new InvalidDataException("Effect80 directory selection leaves its four sixteen-pointer tables.");
        }
        return Dword(4 + table * 64 + index * 4);
    }

    private uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(offset, 4));
}

public sealed record NativeBattleEffect80Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, NativeBattleEffect80NodeResourceBank> OriginalNodeBanks, ImmutableDictionary<uint, ImmutableArray<byte>> OriginalInitializedSources, ImmutableArray<NativeBattleGfAnimationSource> Acts, ImmutableDictionary<uint, ImmutableArray<byte>> Sprites, ImmutableDictionary<uint, uint> Sounds, ImmutableDictionary<uint, ImmutableArray<byte>> StaticTables, uint TargetRecordAddress)
{
    private static readonly int[][] Vertices = [[114, 120, 90], [114, 90, 120]];
    private static readonly int[][] Shapes = [[1, 1, 2], [1, 2, 1]];
    private static readonly int[][][] Groups = [[[0, 0, 32, 96, 0, 0, 0, 0], [0, 0, 0, 0, 0, 100, 0, 0], [0, 0, 0, 0, 0, 60, 0, 0]], [[0, 0, 32, 96, 0, 0, 0, 0], [0, 0, 0, 0, 0, 60, 0, 0], [0, 0, 0, 0, 0, 100, 0, 0]]];

    public ImmutableArray<byte> ReadOriginalNodeBank(uint address) => OriginalNodeBanks.TryGetValue(address, out NativeBattleEffect80NodeResourceBank? bank) ? bank.Data : throw new NotSupportedException("Effect80 requires one of its original selected node-bank inputs.");
    public ImmutableArray<byte> ReadOriginalInitializedSource(uint address) => OriginalInitializedSources.TryGetValue(address, out ImmutableArray<byte> bytes) ? bytes : throw new NotSupportedException("Effect80 requires an exact original initialized source input.");
    public NativeBattleGfAnimationSource Act(uint address) => Acts.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Effect80 ACT source is unsupported.");
    public ImmutableArray<byte> Sprite(uint address) => Sprites.TryGetValue(address, out ImmutableArray<byte> bytes) ? bytes : throw new NotSupportedException("Effect80 sprite source is unsupported.");

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag079.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || OriginalNodeBanks is null || OriginalNodeBanks.Count != 3 || OriginalInitializedSources is null || OriginalInitializedSources.Count != 2 || Acts.IsDefault || Acts.Length != 2 || Sprites is null || Sprites.Count != 2 || Sounds is null || Sounds.Count != 2 || StaticTables is null || StaticTables.Count != 4 || TargetRecordAddress != TargetAddress)
        {
            throw new InvalidDataException("Effect80 requires its exact original selected resource fronts.");
        }
        ValidateTim();
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 12 || Word(Camera.Data, 6) != CameraLength || !Camera.Data.AsSpan().Slice(8, 4).SequenceEqual(new byte[] { 6, 0, 0, 2 }))
        {
            throw new InvalidDataException("Effect80 camera requires its original registration directories.");
        }
        if (Dword(Vibration.Data, 0) != 0 || Dword(Vibration.Data, 4) != 256 || Word(Vibration.Data, 256) != 58 || Word(Vibration.Data, 258) != 0)
        {
            throw new InvalidDataException("Effect80 vibration leaves its original selected channel.");
        }
        foreach ((uint address, int length) in NodeSourceLengths)
        {
            if (!OriginalNodeBanks.TryGetValue(address, out NativeBattleEffect80NodeResourceBank? bank) || bank.Address != address || bank.Data.Length != length)
            {
                throw new InvalidDataException("Effect80 is missing an exact original node source.");
            }
            bank.Validate();
        }
        foreach ((uint address, int length) in InitializedSourceLengths)
        {
            ImmutableArray<byte> bytes = ReadOriginalInitializedSource(address);
            if (bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect80 initialized input dimensions differ from their physical owners.");
            }
        }
        if (Dword(ReadOriginalInitializedSource(TrailPointerAddress), 0) != TrailAddress)
        {
            throw new InvalidDataException("Effect80 initialized trail pointer changed its original target.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleGfAnimationSource source = Acts[index];
            int table = checked((int)Dword(source.Data, 4));
            if (source.Address != ActAddresses[index] || source.Data.IsDefault || source.Data.Length != ActLengths[index] || Dword(source.Data, 0) != 3 || Dword(source.Data, table) != 4 || Dword(source.Data, table + 4) != (index == 0 ? 128 : 24))
            {
                throw new InvalidDataException("Effect80 ACT source changed its original directory.");
            }
            int clear = index == 0 ? 196 : 216;
            _ = new NativeBattleGfAnimationWorkspace(new byte[clear].ToImmutableArray()).Initialize7016B0(source, clear);
            for (int geometry = 0; geometry < 3; geometry++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
            }
            ImmutableArray<byte> sprite = Sprite(SpriteAddresses[index]);
            if (sprite.IsDefault || sprite.Length != SpriteLengths[index] || Word(sprite, 8) != (index == 0 ? 19 : 12))
            {
                throw new InvalidDataException("Effect80 sprite changed its original frame directory.");
            }
        }
        foreach ((uint address, int length) in new (uint, int)[] { (AgeTableAddress, AgeTableLength), (DistanceAddress, DistanceLength), (ShapeTableAddress, ShapeTableLength), (BirthTableAddress, BirthTableLength) })
        {
            if (!StaticTables.TryGetValue(address, out ImmutableArray<byte> bytes) || bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect80 is missing a bounded original read-only table.");
            }
        }
        if (!Sounds.TryGetValue(0x157B2E4, out uint first) || first != 0x668C2 || !Sounds.TryGetValue(0x157B2E8, out uint second) || second != 0x66925)
        {
            throw new InvalidDataException("Effect80 requires both original sound descriptor cells.");
        }
    }

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        for (int index = 0; index < Acts.Length; index++)
        {
            NativeBattleGfAnimationSource source = Acts[index];
            for (int geometry = 0; geometry < 3; geometry++)
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
                    throw new InvalidDataException("Effect80 ACT geometry changed its selected shape bounds.");
                }
                int cursor = offset + polygon;
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < 8; group++)
                {
                    int count = checked((int)Dword(source.Data, cursor));
                    cursor += 4;
                    if (count != Groups[index][geometry][group] || count > (source.Data.Length - cursor) / strides[group])
                    {
                        throw new InvalidDataException("Effect80 ACT face group leaves its exact source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            int row = Word(source.Data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                            if ((row & 7) != 0 || row > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Effect80 ACT face index leaves its current physical rows.");
                            }
                        }
                    }
                    cursor += count * strides[group];
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect80 geometry address is outside its selected ACT sources.");
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect80 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect80 static reads exclude current mutable node, pointer and trail owners.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Bytes)> StaticFronts()
    {
        yield return (CameraAddress, Camera.Data);
        yield return (VibrationAddress, Vibration.Data);
        yield return (TargetPointerAddress, BitConverter.GetBytes(TargetRecordAddress).ToImmutableArray());
        foreach (NativeBattleGfAnimationSource source in Acts)
        {
            yield return (source.Address, source.Data);
        }
        foreach (KeyValuePair<uint, ImmutableArray<byte>> source in Sprites.Concat(StaticTables))
        {
            yield return (source.Key, source.Value);
        }
        foreach ((uint address, uint value) in Sounds)
        {
            yield return (address, BitConverter.GetBytes(value).ToImmutableArray());
        }
    }

    private void ValidateTim()
    {
        ImmutableArray<byte> bytes = MainTim.Data;
        if (Dword(bytes, 0) != 16 || Dword(bytes, 4) != 9)
        {
            throw new InvalidDataException("Effect80 requires its original indexed TIM layout.");
        }
        int cursor = 8;
        foreach (ushort[] rectangle in new ushort[][] { [320, 240, 256, 13], [448, 256, 256, 256] })
        {
            int length = checked((int)Dword(bytes, cursor));
            if (length != 12 + rectangle[2] * rectangle[3] * 2 || !Enumerable.Range(0, 4).Select(index => Word(bytes, cursor + 4 + index * 2)).SequenceEqual(rectangle))
            {
                throw new InvalidDataException("Effect80 TIM block changed its original rectangle.");
            }
            cursor += length;
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("Effect80 TIM has bytes outside its exact two blocks.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
}
