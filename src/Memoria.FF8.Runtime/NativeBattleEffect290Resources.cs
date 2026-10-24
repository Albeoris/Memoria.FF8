using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect290Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Geometry, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, NativeBattleModuleResource> ProjectedQuads, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public static readonly ImmutableArray<uint> ActAddresses = [0x1067CB8, 0x106971C, 0x10688C4];
    public static readonly ImmutableArray<int> ActLengths = [3084, 3008, 3672];
    public static readonly ImmutableArray<int> ActWorkspaceLengths = [0x110, 0x22C, 0xC8];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x1067CC8, 0x10682BC, 0x106972C, 0x10699A0, 0x10688D0];
    public static readonly ImmutableArray<int> GeometryLengths = [1524, 628, 628, 540, 3004];
    public static readonly ImmutableArray<uint> MeshAddresses = [0x1067400, 0x10674B8, 0x1067570, 0x1067628];
    public static readonly ImmutableArray<int> MeshLengths = [184, 184, 184, 184];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x10676E0, 0x1067B58, 0x106784C];
    public static readonly ImmutableArray<int> SpriteLengths = [364, 352, 428];
    public static readonly ImmutableArray<uint> ScriptCountAddresses = [0x106E64E, 0x106E66A, 0x106E684, 0x106E6AA, 0x106E6C4, 0x106E6FA, 0x106E71E, 0x106E756, 0x106E766, 0x106E776, 0x106E786, 0x106E796, 0x106E7A6, 0x106E7B6, 0x106E7D2, 0x106E7F6, 0x106E82E, 0x106E83E, 0x106E84E, 0x106E85E, 0x106E86E, 0x106E87E, 0x106E88E];
    public static readonly ImmutableArray<(uint Address, int Length)> ScriptRanges = [(0x106E648, 26), (0x106E664, 62), (0x106E6A4, 62), (0x106E6E4, 102), (0x106E74C, 16), (0x106E75C, 16), (0x106E76C, 16), (0x106E77C, 16), (0x106E78C, 16), (0x106E79C, 16), (0x106E7AC, 16), (0x106E7BC, 102), (0x106E824, 16), (0x106E834, 16), (0x106E844, 16), (0x106E854, 16), (0x106E864, 16), (0x106E874, 16), (0x106E884, 16)];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = new (uint Address, int Length)[] { (SoundAddress, 4), (TrigonometryAddress, TrigonometryLength), (NormalizationAddress, NormalizationLength) }.Concat(ScriptRanges).ToImmutableArray();
    [JsonIgnore]
    public NativeVibrationBank VibrationBank => new(Convert.ToHexString(SHA256.HashData(Vibration.Data.AsSpan())), [checked((int)Dword(Vibration.Data, 0)), checked((int)Dword(Vibration.Data, 4))], Vibration.Data);
    public NativeBattleGfAnimationSource Act(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect290 ACT 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleStaticMeshResource ReadGeometry(uint address) => Geometry.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect290 geometry 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect290 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect290 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public int WorkspaceLength(uint address)
    {
        int index = ActAddresses.IndexOf(address);
        return index >= 0 ? ActWorkspaceLengths[index] : throw new NotSupportedException("Effect290 ACT workspace is unsupported.");
    }

    public NativeBattleModuleResource ReadProjectedQuads(uint address) => ProjectedQuads.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect290 projected quad source 0x{address:X8} is unsupported.");
    public NativeBattleEffectFileAsset ReadFile(int fileId) => Files.TryGetValue(fileId, out NativeBattleEffectFileAsset? source) ? source : throw new NotSupportedException("Effect290 file input is unsupported.");

    public byte[] OriginalScriptCount(uint address)
    {
        if (!ScriptCountAddresses.Contains(address))
        {
            throw new InvalidDataException("Effect290 script counter leaves its exact physical source cells.");
        }
        return ReadOriginal(address, 2);
    }

    public byte[] ReadScript(NativeBattleFileStaging staging, uint address, int length)
    {
        if (length < 0 || !ScriptRanges.Any(range => address >= range.Address && (ulong)address + (uint)length <= (ulong)range.Address + (uint)range.Length))
        {
            throw new NotSupportedException("Effect290 script read leaves its selected instruction source.");
        }
        byte[] data = ReadOriginal(address, length);
        foreach (uint countAddress in ScriptCountAddresses)
        {
            ulong start = Math.Max((ulong)address, countAddress);
            ulong end = Math.Min((ulong)address + (uint)length, (ulong)countAddress + 2);
            if (start < end)
            {
                staging.Read(checked((uint)start), checked((int)(end - start))).CopyTo(data, checked((int)(start - address)));
            }
        }
        return data;
    }

    public short ReadScriptWord(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(ReadScript(staging, address, 2));

    public byte[] ReadStatic(uint address, int length)
    {
        if (length < 0 || ScriptCountAddresses.Any(count => address < (ulong)count + 2 && count < (ulong)address + (uint)length))
        {
            throw new NotSupportedException("Effect290 current script counters require their shared staging owner.");
        }
        return ReadOriginal(address, length);
    }

    private byte[] ReadOriginal(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect290 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag289.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 199200 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 192 || Programmes is null || Programmes.Count != 3 || Geometry is null || Geometry.Count != 5 || Meshes is null || Meshes.Count != 4 || Sprites is null || Sprites.Count != 3 || StaticFronts is null || Vibration is null || Vibration.Data.Length != 296 || ProjectedQuads is null || ProjectedQuads.Count != 1 || Files is null || Files.Count != 1)
        {
            throw new InvalidDataException("Effect290 requires its complete resource inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = Act(ActAddresses[index]);
            if (source.Address != ActAddresses[index] || source.Data.Length != ActLengths[index])
            {
                throw new InvalidDataException("Effect290 ACT source extent is invalid.");
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ActWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ActWorkspaceLengths[index]);
            int count = checked((int)Dword(source.Data, 0));
            for (int geometry = 0; geometry < count; geometry++)
            {
                NativeBattleStaticMeshResource mesh = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                if (ValidateGeometry(mesh.Bytes) != mesh.Bytes.Length)
                {
                    throw new InvalidDataException("Effect290 geometry contains trailing bytes.");
                }
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect290 mesh extent is invalid.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.Length != SpriteLengths[index])
            {
                throw new InvalidDataException("Effect290 sprite extent is invalid.");
            }
            for (int frame = 0; frame < Word(sprite.Bytes, 8); frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != 192)
        {
            throw new InvalidDataException("Effect290 camera header is invalid.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadOriginal(address, length);
        }
        foreach (uint address in ScriptCountAddresses)
        {
            if (BinaryPrimitives.ReadInt16LittleEndian(OriginalScriptCount(address)) <= 0)
            {
                throw new InvalidDataException("Effect290 original loop count is outside its selected positive domain.");
            }
        }
        foreach ((uint address, int length) in ScriptRanges)
        {
            byte[] bytes = ReadOriginal(address, length);
            if (BinaryPrimitives.ReadInt16LittleEndian(bytes) is < 0 or >= 28 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(length - 2)) != 0x1100)
            {
                throw new InvalidDataException("Effect290 selected script has an invalid target or termination boundary.");
            }
        }
        NativeBattleModuleResource quads = ReadProjectedQuads(SmokeSourceAddress);
        if (quads.Address != SmokeSourceAddress || quads.Bytes.Length != 328)
        {
            throw new InvalidDataException("Effect290 projected quad extent is invalid.");
        }
        for (int life = 12; life > 0; life--)
        {
            int offset = Word(quads.Bytes, 2 * (17 - life));
            int count = checked((int)Dword(quads.Bytes, offset));
            if (count != 1 || offset > quads.Bytes.Length - 4 - count * 20)
            {
                throw new InvalidDataException("Effect290 projected quad directory leaves its selected records.");
            }
        }
        NativeBattleEffectFileAsset file = ReadFile(360);
        if (file.FileId != 360 || file.Name != "MA8DEF_0.TIM" || file.Data.Length != 33824)
        {
            throw new InvalidDataException("Effect290 completion file is invalid.");
        }
        ValidateTim(file.Data, 8);
        VibrationBank.Validate();
    }

    private static int ValidateGeometry(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapes = checked((int)Dword(data, 8));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || shapes <= 0 || cursor != checked(12 + vertices * shapes * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect290 ACT shape rows leave their source.");
        }
        return ValidateGroups(data, cursor, vertices);
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || cursor != checked(8 + vertices * 8) || cursor > data.Length - 32)
        {
            throw new InvalidDataException("Effect290 mesh shape rows leave their source.");
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
                throw new InvalidDataException("Effect290 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect290 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect290 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect290 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect290 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect290 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect290 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
