using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect70Resources(NativeBattleEffectInitializerAsset MainTim, ImmutableArray<NativeBattleGfAnimationSource> Acts, ImmutableArray<NativeBattleModuleResource> PuffSprites, NativeBattleStaticMeshResource ParticleMesh, ImmutableArray<byte> DefaultDispatch, ImmutableArray<byte> MainDispatch, NativeVibrationBank Vibration, ImmutableDictionary<uint, uint> Sounds, ImmutableDictionary<short, NativeBattleEffectFileAsset> Files)
{
    private static readonly int[][] VertexCounts = [[80], [48, 61], [48, 61], [65, 65, 48, 33, 25, 114, 114, 61, 32, 39, 12, 64, 80, 61, 25], [65, 65, 80]];
    private static readonly int[][] ShapeCounts = [[1], [2, 1], [2, 1], [2, 2, 2, 4, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 1], [2, 2, 2]];
    private static readonly int[][][] GroupCounts = [[[0, 0, 0, 60, 0, 0, 0, 0]], [[0, 0, 0, 0, 0, 0, 0, 32], [0, 0, 0, 0, 20, 40, 0, 0]], [[0, 0, 0, 0, 0, 0, 0, 32], [0, 0, 0, 0, 20, 40, 0, 0]], [[0, 0, 16, 32, 0, 0, 0, 16], [0, 0, 0, 0, 0, 0, 16, 48], [0, 0, 0, 0, 0, 0, 0, 32], [0, 0, 0, 20, 0, 0, 0, 0], [0, 0, 0, 16, 0, 0, 0, 0], [0, 0, 32, 96, 0, 0, 0, 0], [0, 0, 32, 96, 0, 0, 0, 0], [0, 0, 0, 0, 20, 40, 0, 0], [0, 0, 0, 0, 0, 0, 0, 15], [0, 0, 0, 10, 0, 0, 0, 14], [0, 0, 0, 5, 0, 0, 0, 0], [0, 16, 0, 0, 0, 32, 0, 0], [0, 0, 0, 60, 0, 0, 0, 0], [0, 0, 0, 0, 20, 40, 0, 0], [0, 0, 0, 16, 0, 0, 0, 0]], [[0, 0, 16, 32, 0, 0, 0, 16], [0, 0, 0, 0, 0, 0, 16, 48], [0, 0, 0, 60, 0, 0, 0, 0]]];

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag069-259.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Acts.IsDefault || Acts.Length != 5 || PuffSprites.IsDefault || PuffSprites.Length != 2 || Sounds is null || Sounds.Count != 3 || Files is null || Files.Count != 2)
        {
            throw new InvalidDataException("Effect70 resources require their original exact addressed fronts.");
        }
        ValidateTim(MainTim.Data, 9, [320, 240, 256, 9, 384, 256, 448, 256]);
        int[] nodes = [2, 3, 3, 55, 5];
        int[] frames = [22, 200, 30, 182, 200];
        for (int index = 0; index < Acts.Length; index++)
        {
            NativeBattleGfAnimationSource source = Acts[index];
            if (source is null || source.Address != ActAddresses[index] || source.Data.IsDefault || source.Data.Length != ActLengths[index] || Dword(source.Data, 0) != VertexCounts[index].Length)
            {
                throw new InvalidDataException("Effect70 ACT source changed its selected address or dimensions.");
            }
            int table = checked((int)Dword(source.Data, 4));
            if (Dword(source.Data, table) != nodes[index] || Dword(source.Data, table + 4) != frames[index])
            {
                throw new InvalidDataException("Effect70 ACT source changed its complete node/frame directory.");
            }
            NativeBattleGfAnimationWorkspace workspace = new(new byte[WorkspaceClearLengths[index]].ToImmutableArray());
            _ = workspace.Initialize7016B0(source, WorkspaceClearLengths[index]);
            for (int geometry = 0; geometry < VertexCounts[index].Length; geometry++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
            }
        }
        if (DefaultDispatch.IsDefault || !DefaultDispatch.AsSpan().SequenceEqual("0000000000000000"u8) || MainDispatch.IsDefault || !MainDispatch.AsSpan().SequenceEqual("000000100111000"u8))
        {
            throw new InvalidDataException("Effect70 requires its exact default and specialized ASCII dispatch tables.");
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleModuleResource sprite = PuffSprites[index];
            if (sprite is null || sprite.Address != PuffAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != PuffLengths[index] || Word(sprite.Bytes, 8) != (index == 0 ? 16 : 20) || sprite.Kind != (index == 0 ? 7 : 10))
            {
                throw new InvalidDataException("Effect70 projected sprite changed its selected front.");
            }
            for (int phase = 0; phase < (index == 0 ? 16 : 20); phase++)
            {
                _ = sprite.ProjectedFrame571C80(phase);
            }
        }
        if (ParticleMesh is null || ParticleMesh.Address != ParticleMeshAddress || ParticleMesh.Bytes.IsDefault || ParticleMesh.Bytes.Length != ParticleMeshLength || Dword(ParticleMesh.Bytes, 0) != 104 || Dword(ParticleMesh.Bytes, 4) != 12)
        {
            throw new InvalidDataException("Effect70 particle mesh requires its original twelve eight-byte rows.");
        }
        ValidateFaces(ParticleMesh.Bytes, 104, 12, [0, 0, 0, 0, 0, 6, 0, 0]);
        if (Vibration is null || Vibration.Bytes.IsDefault || Vibration.Bytes.Length != VibrationLength || Vibration.Offsets.IsDefault || !Vibration.Offsets.SequenceEqual(new[] { 0, 256 }))
        {
            throw new InvalidDataException("Effect70 requires its complete vibration front.");
        }
        Vibration.Validate();
        if (Vibration.ReadWord(256) != 28 || Vibration.ReadWord(258) != 0)
        {
            throw new InvalidDataException("Effect70 vibration channel dimensions changed.");
        }
        uint[] values = [0x642F5, 0x642F8, 0x642FB];
        for (int index = 0; index < 3; index++)
        {
            if (!Sounds.TryGetValue(SoundAddresses[index], out uint value) || value != values[index])
            {
                throw new InvalidDataException("Effect70 is missing a selected sound cell.");
            }
        }
        for (int index = 0; index < 2; index++)
        {
            short id = checked((short)(360 + index));
            if (!Files.TryGetValue(id, out NativeBattleEffectFileAsset? file) || file.FileId != id || file.Name != $"MA8DEF_{index}.TIM" || file.Data.IsDefault || file.Data.Length != 33824)
            {
                throw new InvalidDataException("Effect70 requires both original streamed TIM files.");
            }
            ValidateTim(file.Data, 8, [624, 224, 16, 32, checked((ushort)(704 + index * 64)), 256, 64, 256]);
        }
    }

    public NativeBattleGfAnimationSource Act(uint address) => Acts.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Effect70 selected an unsupported ACT source.");
    public NativeBattleModuleResource Puff(uint address) => PuffSprites.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Effect70 selected an unsupported puff sprite.");
    public NativeBattleEffectFileAsset ReadFile(int id) => id is 360 or 361 && Files.TryGetValue(checked((short)id), out NativeBattleEffectFileAsset? file) ? file : throw new NotSupportedException("Effect70 selected an unsupported streamed file.");

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        if (address == ParticleMeshAddress)
        {
            return ParticleMesh;
        }
        for (int act = 0; act < Acts.Length; act++)
        {
            NativeBattleGfAnimationSource source = Acts[act];
            for (int geometry = 0; geometry < VertexCounts[act].Length; geometry++)
            {
                int offset = checked((int)Dword(source.Data, 8 + geometry * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                int vertices = VertexCounts[act][geometry];
                int shapes = ShapeCounts[act][geometry];
                int polygon = checked(12 + vertices * shapes * 8);
                if (Dword(source.Data, offset) != polygon || Dword(source.Data, offset + 4) != vertices || Dword(source.Data, offset + 8) != shapes)
                {
                    throw new InvalidDataException("Effect70 ACT geometry changed its selected shape dimensions.");
                }
                int end = ValidateFaces(source.Data, checked(offset + polygon), vertices, GroupCounts[act][geometry]);
                return new(0, address, source.Data.AsSpan().Slice(offset, end - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect70 selected an unsupported geometry front.");
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect70 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect70 static read leaves its selected literal fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Bytes)> StaticFronts()
    {
        foreach (NativeBattleGfAnimationSource source in Acts)
        {
            yield return (source.Address, source.Data);
        }
        foreach (NativeBattleModuleResource source in PuffSprites)
        {
            yield return (source.Address, source.Bytes);
        }
        yield return (ParticleMesh.Address, ParticleMesh.Bytes);
        yield return (DefaultDispatchAddress, DefaultDispatch);
        yield return (MainDispatchAddress, MainDispatch);
        yield return (VibrationAddress, Vibration.Bytes);
        foreach ((uint address, uint value) in Sounds)
        {
            yield return (address, BitConverter.GetBytes(value).ToImmutableArray());
        }
    }

    private static int ValidateFaces(ImmutableArray<byte> data, int cursor, int vertices, int[] groups)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = groups[group];
            if (Dword(data, cursor) != count)
            {
                throw new InvalidDataException("Effect70 geometry changed its selected primitive directory.");
            }
            for (int face = 0; face < count; face++)
            {
                int first = checked(cursor + 4 + face * strides[group]);
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int offset = Word(data, first + 4 + corner * 2) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect70 polygon leaves its selected physical vertex rows.");
                    }
                }
            }
            cursor = checked(cursor + 4 + count * strides[group]);
        }
        return cursor;
    }

    private static void ValidateTim(ImmutableArray<byte> data, uint flags, ushort[] rectangles)
    {
        if (Dword(data, 0) != 16 || Dword(data, 4) != flags)
        {
            throw new InvalidDataException("Effect70 requires its original indexed TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            for (int index = 0; index < 4; index++)
            {
                if (Word(data, cursor + 4 + index * 2) != rectangles[block * 4 + index])
                {
                    throw new InvalidDataException("Effect70 TIM rectangle differs from its original placement.");
                }
            }
            if (length != checked(12 + rectangles[block * 4 + 2] * rectangles[block * 4 + 3] * 2) || length > data.Length - cursor)
            {
                throw new InvalidDataException("Effect70 TIM block leaves its selected original resource.");
            }
            cursor = checked(cursor + length);
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect70 TIM extends beyond its two original blocks.");
        }
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Read(data, offset, 4));
    private static ushort Word(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(data, offset, 2));
    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> data, int offset, int length)
    {
        if (data.IsDefault || offset < 0 || offset > data.Length - length)
        {
            throw new InvalidDataException("Effect70 literal read leaves its complete addressed front.");
        }
        return data.AsSpan().Slice(offset, length);
    }
}
