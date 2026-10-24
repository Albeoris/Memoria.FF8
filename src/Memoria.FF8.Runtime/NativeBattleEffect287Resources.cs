using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect287Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectInitializerAsset SecondaryTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public static readonly ImmutableArray<uint> ActAddresses = [0x13EF924, 0x13F87E8, 0x13F8F5C];
    public static readonly ImmutableArray<int> ActLengths = [36548, 1908, 7328];
    public static readonly ImmutableArray<int> ActWorkspaceLengths = [2244, 184, 444];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x13EF954, 0x13F1B00, 0x13F36AC, 0x13F4018, 0x13F477C, 0x13F4C30, 0x13F4F54, 0x13F57A0, 0x13F5F04, 0x13F66E0, 0x13F87F4, 0x13F8F74, 0x13F9A18, 0x13FA17C, 0x13FA370];
    public static readonly ImmutableArray<int> GeometryLengths = [8620, 7084, 2412, 1892, 1204, 804, 2124, 1892, 2012, 500, 1196, 2724, 1892, 500, 572];
    public static readonly ImmutableArray<uint> MeshAddresses = [0x13EF72C];
    public static readonly ImmutableArray<int> MeshLengths = [504];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x13EF448, 0x13EF5E8];
    public static readonly ImmutableArray<int> SpriteLengths = [416, 324];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (SourcePointAddress, 8), (TargetOrderAddress, 3), (TriangleDescriptorsAddress, 312), (DistanceAddress, DistanceLength), (NormalizationAddress, NormalizationLength)];
    public NativeBattleGfAnimationSource Act(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect287 ACT 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect287 geometry 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect287 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect287 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public NativeBattleEffectFileAsset ReadFile(int fileId) => Files.TryGetValue(fileId, out NativeBattleEffectFileAsset? source) ? source : throw new NotSupportedException($"Effect287 file {fileId} is unsupported.");

    public ushort ReadNormalizationWord(int index)
    {
        if (index is < 64 or > 255)
        {
            throw new NotSupportedException("Effect287 normalization index leaves its consumed original domain.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(ReadStatic(checked(NormalizationAddress + (uint)((index - 64) * 2)), 2));
    }

    public int WorkspaceLength(uint address)
    {
        int index = ActAddresses.IndexOf(address);
        return index >= 0 ? ActWorkspaceLengths[index] : throw new NotSupportedException("Effect287 ACT workspace is unsupported.");
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
        throw new NotSupportedException($"Effect287 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Data.IsDefault || MainTim.Data.Length != 168480 || SecondaryTim is null || SecondaryTim.Data.IsDefault || SecondaryTim.Data.Length != 16928 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 668 || Programmes is null || Programmes.Count != 3 || Geometry is null || Geometry.Count != 15 || Meshes is null || Meshes.Count != 1 || Sprites is null || Sprites.Count != 2 || Files is null || Files.Count != 1 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect287 requires its complete resource inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        ValidateTim(SecondaryTim.Data, 9);
        NativeBattleEffectFileAsset file = ReadFile(360);
        if (file.FileId != 360 || file.Name != "MA8DEF_0.TIM" || file.Data.IsDefault || file.Data.Length != 33824)
        {
            throw new InvalidDataException("Effect287 requires its original completion file input.");
        }
        ValidateTim(file.Data, 8);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = Act(ActAddresses[index]);
            if (source.Address != ActAddresses[index] || source.Data.Length != ActLengths[index])
            {
                throw new InvalidDataException("Effect287 ACT source extent is invalid.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ActWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ActWorkspaceLengths[index]);
            int count = checked((int)Dword(source.Data, 0));
            for (int geometry = 0; geometry < count; geometry++)
            {
                NativeBattleStaticMeshResource mesh = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                if (ValidateGeometry(mesh.Bytes) != mesh.Bytes.Length)
                {
                    throw new InvalidDataException("Effect287 geometry contains trailing bytes.");
                }
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect287 mesh extent is invalid.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.Length != SpriteLengths[index])
            {
                throw new InvalidDataException("Effect287 sprite extent is invalid.");
            }
            for (int frame = 0; frame < Word(sprite.Bytes, 8); frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 32 || Word(Camera.Data, 6) != 668)
        {
            throw new InvalidDataException("Effect287 camera header is invalid.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(32).ToArray().ToImmutableArray());
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
            throw new InvalidDataException("Effect287 ACT shape rows leave their source.");
        }
        return ValidateGroups(data, cursor, vertices);
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || cursor != checked(8 + vertices * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect287 mesh shape rows leave their source.");
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
                throw new InvalidDataException("Effect287 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect287 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect287 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect287 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect287 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect287 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect287 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
