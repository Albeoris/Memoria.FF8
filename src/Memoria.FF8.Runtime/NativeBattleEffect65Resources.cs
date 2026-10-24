using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect65Resources(NativeBattleEffectInitializerAsset MainTim, NativeVibrationBank Vibration, NativeBattleModuleResource PuffSprite, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts, ImmutableDictionary<short, NativeBattleEffectFileAsset> Files)
{
    public NativeBattleEffectFileAsset ReadFile(short file) => Files.TryGetValue(file, out NativeBattleEffectFileAsset? source) ? source : throw new NotSupportedException("Effect65 streamed file is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => address == SpriteAddress ? PuffSprite : throw new NotSupportedException("Effect65 sprite address is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException("Effect65 raw mesh address is unsupported.");
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => ReadMesh(address);

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect65 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException($"Effect65 static read at {address:X8} for {length} bytes leaves its readonly resource fronts.");
    }

    public uint ReadSound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(address, 4).AsSpan());

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag064.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Meshes is null || Meshes.Count != 4 || StaticFronts is null || Files is null || Files.Count != FileIds.Length)
        {
            throw new InvalidDataException("Effect65 requires its complete selected resource fronts.");
        }
        ValidateTim(MainTim.Data, 9);
        Vibration.Validate();
        if (Vibration.Bytes.Length != VibrationLength || !Vibration.Offsets.SequenceEqual(new int[] { 0, 256 }))
        {
            throw new InvalidDataException("Effect65 vibration channels changed.");
        }
        if (PuffSprite.Address != SpriteAddress || PuffSprite.Bytes.Length != SpriteLength || Word(PuffSprite.Bytes, 8) != 8)
        {
            throw new InvalidDataException("Effect65 sprite directory changed.");
        }
        for (int frame = 0; frame < 8; frame++)
        {
            _ = PuffSprite.ProjectedFrame571C80(frame);
        }
        int[] vertices = [144, 220, 306, 104];
        int[][] groups = [[0, 0, 0, 0, 0, 0, 64, 96], [0, 0, 0, 24, 0, 96, 0, 48], [0, 0, 0, 0, 0, 0, 0, 150], [0, 0, 0, 0, 0, 0, 0, 50]];
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Bytes.Length != MeshLengths[index] || Dword(mesh.Bytes, 4) != vertices[index])
            {
                throw new InvalidDataException("Effect65 raw mesh bounds changed.");
            }
            ValidateMesh(mesh.Bytes, false, groups[index]);
        }
        if (!ReadStatic(ScaleAddress, 12).AsSpan().SequenceEqual(new int[] { 2048, 2048, 2048 }.SelectMany(BitConverter.GetBytes).ToArray()) || ReadStatic(DistanceAddress, 16384).Length != 16384 || ReadStatic(TrigonometryAddress, 16384).Length != 16384)
        {
            throw new InvalidDataException("Effect65 lookup or scale fronts changed.");
        }
        for (int index = 0; index < 8; index++)
        {
            _ = ReadStatic(checked(0x133ABC0u + (uint)(index * 16)), 8);
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            foreach ((uint mutable, int length) in LiteralRanges)
            {
                if ((ulong)start < (ulong)mutable + (uint)length && (ulong)mutable < (ulong)start + (uint)bytes.Length)
                {
                    throw new InvalidDataException("Effect65 resources cannot own mutable descriptor fields.");
                }
            }
        }
        int[] lengths = [33824, 33824, 33824, 66592, 66592, 94152, 81568];
        for (int index = 0; index < FileIds.Length; index++)
        {
            NativeBattleEffectFileAsset file = ReadFile(FileIds[index]);
            string name = file.FileId <= 362 ? $"MA8DEF_{file.FileId - 360}.TIM" : $"MAG064_H.0{file.FileId - 391}";
            if (file.Name != name || file.Data.IsDefault || file.Data.Length != lengths[index])
            {
                throw new InvalidDataException("Effect65 streamed file identity or extent changed.");
            }
            if (file.FileId < 393)
            {
                ValidateTim(file.Data, file.FileId <= 362 ? 8u : 9u);
            }
        }
        foreach ((short file, ImmutableArray<int> offsets) in BundleProgrammeOffsets)
        {
            ImmutableArray<byte> data = ReadFile(file).Data;
            if (Dword(data, 0) != offsets.Length)
            {
                throw new InvalidDataException("Effect65 loaded programme directory changed.");
            }
            for (int index = 0; index < offsets.Length; index++)
            {
                if (Dword(data, 4 + index * 4) != offsets[index])
                {
                    throw new InvalidDataException("Effect65 loaded programme offset changed.");
                }
                NativeBattleGfAnimationSource source = new(checked(ArenaAddress + FileOffset + (uint)offsets[index]), data.AsSpan().Slice(offsets[index]).ToArray().ToImmutableArray());
                int[] caches = file == 393 ? [940, 428, 640, 640, 640, 2500] : [288, 288, 288, 2140, 120, 416, 200];
                _ = new NativeBattleGfAnimationWorkspace(new byte[caches[index]].ToImmutableArray()).Initialize7016B0(source, caches[index]);
                int count = checked((int)Dword(source.Data, 0));
                if (count <= 0 || count > 256)
                {
                    throw new InvalidDataException("Effect65 loaded geometry count is invalid.");
                }
                for (int geometry = 0; geometry < count; geometry++)
                {
                    _ = ReadLoadedGeometry(source, checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                }
            }
        }
        ImmutableArray<byte> loaded = ReadFile(394).Data;
        foreach ((int address, int length) in CurrentMeshLengths)
        {
            ValidateMesh(loaded.AsSpan(address - FileOffset, length).ToArray().ToImmutableArray(), false, address == 0x393C8 ? [0, 0, 0, 0, 0, 0, 80, 0] : [0, 0, 32, 96, 0, 0, 0, 0]);
        }
        for (int index = 0; index < 9; index++)
        {
            int offset = 0x39E00 + index * 0x2B8 - FileOffset;
            int length = index == 8 ? 368 : 696;
            ValidateMesh(loaded.AsSpan(offset, length).ToArray().ToImmutableArray(), false, index == 8 ? [0, 0, 0, 0, 0, 0, 0, 6] : [0, 0, 0, 0, 0, 0, 20, 0]);
        }
    }

    public static NativeBattleStaticMeshResource ReadLoadedGeometry(NativeBattleGfAnimationSource current, uint address)
    {
        int offset = checked((int)(address - current.Address));
        if (offset < 0 || offset > current.Data.Length - 12)
        {
            throw new InvalidDataException("Effect65 current geometry leaves its loaded file.");
        }
        ImmutableArray<byte> suffix = current.Data.AsSpan().Slice(offset).ToArray().ToImmutableArray();
        int length = ValidateMesh(suffix, true, null);
        return new(0, address, suffix.AsSpan(0, length).ToArray().ToImmutableArray());
    }

    public static NativeBattleStaticMeshResource ReadCurrentRawMesh(uint address, ImmutableArray<byte> current)
    {
        int length = ValidateMesh(current, false, null);
        if (length != current.Length)
        {
            throw new InvalidDataException("Effect65 current raw mesh has bytes outside its selected extent.");
        }
        return new(0, address, current);
    }

    private static int ValidateMesh(ImmutableArray<byte> data, bool shapes, int[]? expected)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapeCount = shapes ? checked((int)Dword(data, 8)) : 1;
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > 306 || shapeCount <= 0 || shapeCount > 4 || cursor != checked((shapes ? 12 : 8) + vertices * shapeCount * 8))
        {
            throw new InvalidDataException("Effect65 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group] || expected is not null && count != expected[group])
            {
                throw new InvalidDataException("Effect65 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect65 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect65 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect65 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect65 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect65 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect65 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
