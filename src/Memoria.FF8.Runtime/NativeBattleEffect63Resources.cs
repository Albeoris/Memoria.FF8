using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect63Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public static readonly ImmutableArray<uint> ActAddresses = [0x134f87c, 0x134e154, 0x1350048];
    public static readonly ImmutableArray<int> ActLengths = [1996, 5928, 18860];
    public static readonly ImmutableArray<int> ActWorkspaceLengths = [0xe8, 0x6a0, 0x580];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x134f88c, 0x134fb00, 0x134e164, 0x134e3d8, 0x1350068, 0x135271c, 0x1352990, 0x1353064, 0x1353190, 0x1353404];
    public static readonly ImmutableArray<int> GeometryLengths = [628, 628, 628, 628, 9908, 628, 1748, 300, 628, 628];
    public static readonly ImmutableArray<uint> MeshAddresses = [BeamAddress];
    public static readonly ImmutableArray<int> MeshLengths = [7768];
    public static readonly ImmutableArray<uint> SpriteAddresses = [SpriteAddress];
    public static readonly ImmutableArray<int> SpriteLengths = [324];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (FirstPointAddress, 8), (SecondPointAddress, 8), (FirstSelectorsAddress, 2), (SecondSelectorsAddress, 6)];
    public NativeBattleGfAnimationSource Act(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect63 ACT 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect63 geometry 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect63 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect63 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public int WorkspaceLength(uint address)
    {
        int index = ActAddresses.IndexOf(address);
        return index >= 0 ? ActWorkspaceLengths[index] : throw new NotSupportedException("Effect63 ACT workspace is unsupported.");
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
        throw new NotSupportedException($"Effect63 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag062.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 168480 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 536 || Programmes is null || Programmes.Count != 3 || Geometry is null || Geometry.Count != 10 || Meshes is null || Meshes.Count != 1 || Sprites is null || Sprites.Count != 1 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect63 requires its complete resource inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = Act(ActAddresses[index]);
            if (source.Address != ActAddresses[index] || source.Data.Length != ActLengths[index])
            {
                throw new InvalidDataException("Effect63 ACT source extent is invalid.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ActWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ActWorkspaceLengths[index]);
            int count = checked((int)Dword(source.Data, 0));
            for (int geometry = 0; geometry < count; geometry++)
            {
                NativeBattleStaticMeshResource mesh = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                if (ValidateGeometry(mesh.Bytes) != mesh.Bytes.Length)
                {
                    throw new InvalidDataException("Effect63 geometry contains trailing bytes.");
                }
            }
        }
        for (int index = 0; index < GeometryAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource geometry = ReadGeometry(GeometryAddresses[index]);
            if (geometry.Address != GeometryAddresses[index] || geometry.Bytes.Length != GeometryLengths[index])
            {
                throw new InvalidDataException("Effect63 geometry leaves its exact physical source.");
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect63 mesh extent is invalid.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.Length != SpriteLengths[index])
            {
                throw new InvalidDataException("Effect63 sprite extent is invalid.");
            }
            if (Word(sprite.Bytes, 8) != 12)
            {
                throw new InvalidDataException("Effect63 sprite frame domain is invalid.");
            }
            for (int frame = 0; frame < Word(sprite.Bytes, 8); frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 52 || Word(Camera.Data, 6) != 536)
        {
            throw new InvalidDataException("Effect63 camera header is invalid.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(52).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        for (int index = 0; index < 2; index++)
        {
            if (ReadStatic(FirstSelectorsAddress + (uint)index, 1)[0] != 0x30)
            {
                throw new InvalidDataException("Effect63 early ACT callers cannot consume their unwritten phase field.");
            }
        }
        for (int index = 0; index < 6; index++)
        {
            if (ReadStatic(SecondSelectorsAddress + (uint)index, 1)[0] != (index == 0 ? 0x31 : 0x30))
            {
                throw new InvalidDataException("Effect63 late ACT selector domain is invalid.");
            }
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
            throw new InvalidDataException("Effect63 ACT shape rows leave their source.");
        }
        return ValidateGroups(data, cursor, vertices);
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || cursor != checked(8 + vertices * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect63 mesh shape rows leave their source.");
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
                throw new InvalidDataException("Effect63 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect63 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect63 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect63 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect63 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect63 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect63 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
