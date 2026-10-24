using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect322Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public static readonly ImmutableArray<uint> ActAddresses = [0xFE5CC0];
    public static readonly ImmutableArray<int> ActLengths = [4928];
    public static readonly ImmutableArray<int> ActWorkspaceLengths = [0x210];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0xFE5CD8, 0xFE5F4C, 0xFE61C0, 0xFE65C4];
    public static readonly ImmutableArray<int> GeometryLengths = [628, 628, 1028, 1028];
    public static readonly ImmutableArray<uint> MeshAddresses = [0xFE44C8, 0xFE5A50];
    public static readonly ImmutableArray<int> MeshLengths = [5512, 624];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xFE42A8, 0xFE4384];
    public static readonly ImmutableArray<int> SpriteLengths = [220, 324];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0xFE7000, 4)];
    public NativeBattleGfAnimationSource Act(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect322 ACT 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect322 geometry 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect322 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect322 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public NativeBattleEffectFileAsset ReadFile(int fileId) => Files.TryGetValue(fileId, out NativeBattleEffectFileAsset? source) ? source : throw new NotSupportedException($"Effect322 file {fileId} is unsupported.");

    public int WorkspaceLength(uint address)
    {
        int index = ActAddresses.IndexOf(address);
        return index >= 0 ? ActWorkspaceLengths[index] : throw new NotSupportedException("Effect322 ACT workspace is unsupported.");
    }

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect322 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag321.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 202272 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 644 || Programmes is null || Programmes.Count != 1 || Geometry is null || Geometry.Count != 4 || Meshes is null || Meshes.Count != 2 || Sprites is null || Sprites.Count != 2 || Files is null || Files.Count != 1 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect322 requires its complete resource inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        NativeBattleEffectFileAsset file = ReadFile(360);
        if (file.FileId != 360 || file.Name != "MA8DEF_0.TIM" || file.Data.IsDefault || file.Data.Length != 33824)
        {
            throw new InvalidDataException("Effect322 requires its original completion file input.");
        }
        ValidateTim(file.Data, 8);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = Act(ActAddresses[index]);
            if (source.Address != ActAddresses[index] || source.Data.Length != ActLengths[index])
            {
                throw new InvalidDataException("Effect322 ACT source extent is invalid.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ActWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ActWorkspaceLengths[index]);
            int count = checked((int)Dword(source.Data, 0));
            for (int geometry = 0; geometry < count; geometry++)
            {
                NativeBattleStaticMeshResource mesh = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                if (ValidateGeometry(mesh.Bytes) != mesh.Bytes.Length)
                {
                    throw new InvalidDataException("Effect322 geometry contains trailing bytes.");
                }
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect322 mesh extent is invalid.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.Length != SpriteLengths[index])
            {
                throw new InvalidDataException("Effect322 sprite extent is invalid.");
            }
            for (int frame = 0; frame < Word(sprite.Bytes, 8); frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 52 || Word(Camera.Data, 6) != 644)
        {
            throw new InvalidDataException("Effect322 camera header is invalid.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(52).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }

    private static int ValidateGeometry(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapes = checked((int)Dword(data, 8));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || shapes <= 0 || cursor != checked(12 + vertices * shapes * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect322 ACT shape rows leave their source.");
        }
        return ValidateGroups(data, cursor, vertices);
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || cursor != checked(8 + vertices * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect322 mesh shape rows leave their source.");
        }
        return ValidateGroups(data, cursor, vertices);
    }

    private static int ValidateGroups(ImmutableArray<byte> data, int cursor, int vertices)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect322 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect322 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect322 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect322 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect322 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect322 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect322 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
