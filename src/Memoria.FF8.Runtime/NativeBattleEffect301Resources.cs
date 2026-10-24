using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect301Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, NativeBattleEffectHeapAllocation Vibration, ImmutableArray<byte> OriginalScriptCursor4, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public static readonly ImmutableArray<uint> ActAddresses = [0x103D46C];
    public static readonly ImmutableArray<int> ActLengths = [1776];
    public static readonly ImmutableArray<int> ActWorkspaceLengths = [0x90];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x103D478];
    public static readonly ImmutableArray<int> GeometryLengths = [1332];
    public static readonly ImmutableArray<uint> MeshAddresses = [0x103D314];
    public static readonly ImmutableArray<int> MeshLengths = [344];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x103D118];
    public static readonly ImmutableArray<int> SpriteLengths = [508];
    public const uint ProjectedQuadsAddress = 0x103CFB8;
    public const int ProjectedQuadsLength = 352;
    public const uint ScriptSourceAddress = 0x103BD00;
    public const int ScriptSourceLength = 4792;
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0x103DC98, 4), (ScriptSourceAddress, ScriptSourceLength)];
    [JsonIgnore]
    public NativeVibrationBank VibrationBank => new(Convert.ToHexString(SHA256.HashData(Vibration.Data.AsSpan())), [checked((int)Dword(Vibration.Data, 0)), checked((int)Dword(Vibration.Data, 4))], Vibration.Data);
    public NativeBattleGfAnimationSource Act(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect301 ACT 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect301 geometry 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect301 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => address == 0x103D118 && Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect301 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public NativeBattleModuleResource ReadProjectedQuads(uint address) => address == ProjectedQuadsAddress && Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException("Effect301 requires its selected custom projected-quad source.");
    public short ReadScriptWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(ReadStatic(address, 2));

    public int WorkspaceLength(uint address)
    {
        int index = ActAddresses.IndexOf(address);
        return index >= 0 ? ActWorkspaceLengths[index] : throw new NotSupportedException("Effect301 ACT workspace is unsupported.");
    }

    public byte[] ReadStatic(uint address, int length)
    {
        if (length < 0 || address <= NativeBattleEffect301State.ScriptPointerAddress && (ulong)address + (uint)length > NativeBattleEffect301State.ScriptPointerAddress || address >= NativeBattleEffect301State.ScriptPointerAddress && address < NativeBattleEffect301State.ScriptPointerAddress + 4)
        {
            throw new NotSupportedException("Effect301 immutable sources exclude its current mutable script cursor.");
        }
        byte[] result = new byte[length];
        uint cursor = address;
        int copied = 0;
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts.OrderBy(item => item.Key))
        {
            if (cursor < start || cursor >= (ulong)start + (uint)bytes.Length)
            {
                continue;
            }
            int offset = checked((int)(cursor - start));
            int count = Math.Min(length - copied, bytes.Length - offset);
            bytes.AsSpan(offset, count).CopyTo(result.AsSpan(copied));
            copied += count;
            cursor += (uint)count;
            if (copied == length)
            {
                return result;
            }
        }
        throw new NotSupportedException($"Effect301 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag300.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 132128 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 316 || Programmes is null || Programmes.Count != 1 || Geometry is null || Geometry.Count != 1 || Meshes is null || Meshes.Count != 1 || Sprites is null || Sprites.Count != 2 || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != 294 || OriginalScriptCursor4.IsDefault || OriginalScriptCursor4.Length != 4 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect301 requires its complete resource inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = Act(ActAddresses[index]);
            if (source.Address != ActAddresses[index] || source.Data.Length != ActLengths[index])
            {
                throw new InvalidDataException("Effect301 ACT source extent is invalid.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ActWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ActWorkspaceLengths[index]);
            int count = checked((int)Dword(source.Data, 0));
            for (int geometry = 0; geometry < count; geometry++)
            {
                NativeBattleStaticMeshResource mesh = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                if (ValidateGeometry(mesh.Bytes) != mesh.Bytes.Length)
                {
                    throw new InvalidDataException("Effect301 geometry contains trailing bytes.");
                }
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect301 mesh extent is invalid.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.Length != SpriteLengths[index])
            {
                throw new InvalidDataException("Effect301 sprite extent is invalid.");
            }
            for (int frame = 0; frame < Word(sprite.Bytes, 8); frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != 316)
        {
            throw new InvalidDataException("Effect301 camera header is invalid.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        VibrationBank.Validate();
        if (Dword(OriginalScriptCursor4, 0) != ScriptSourceAddress)
        {
            throw new InvalidDataException("Effect301 cold script input does not identify its original stream.");
        }
        NativeBattleModuleResource custom = ReadProjectedQuads(ProjectedQuadsAddress);
        if (custom.Address != ProjectedQuadsAddress || custom.Bytes.Length != ProjectedQuadsLength || Word(custom.Bytes, 8) != 13)
        {
            throw new InvalidDataException("Effect301 custom projected input extent is invalid.");
        }
        for (int index = 1; index <= 12; index++)
        {
            int offset = Word(custom.Bytes, 8 + index * 2);
            int count = unchecked((int)Dword(custom.Bytes, offset));
            if (count < 0 || offset + 4 > custom.Bytes.Length || count > (custom.Bytes.Length - offset - 4) / 20)
            {
                throw new InvalidDataException("Effect301 custom projected records leave their source.");
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
            throw new InvalidDataException("Effect301 ACT shape rows leave their source.");
        }
        return ValidateGroups(data, cursor, vertices);
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || cursor != checked(8 + vertices * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect301 mesh shape rows leave their source.");
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
                throw new InvalidDataException("Effect301 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect301 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect301 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect301 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect301 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect301 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect301 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
